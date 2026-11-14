using System.Security.Cryptography;
using System.Text;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Hiện thực dịch vụ quản lý tác vụ ngầm (Job Service) tích hợp cả IJobPublisher và IJobTracker.
/// Chịu trách nhiệm băm Fingerprint, điều phối Enqueue qua IJobStore và bảo vệ phân quyền nghiêm ngặt khi đọc/hủy Job.
/// </summary>
public sealed class JobService : IJobPublisher, IJobTracker
{
    private readonly IJobStore _store;
    private readonly IJobMetrics? _metrics;
    private readonly ISnsAlertPublisher? _alertPublisher;

    /// <summary>
    /// Khởi tạo JobService với một kho lưu trữ tác vụ hợp lệ (IJobStore).
    /// </summary>
    /// <param name="store">Kho lưu trữ tác vụ (InMemoryJobStore hoặc FileJobStore).</param>
    /// <param name="metrics">Hệ thống đo lường metrics tùy chọn.</param>
    /// <param name="alertPublisher">Dịch vụ phát cảnh báo sự cố SNS tùy chọn.</param>
    /// <exception cref="ArgumentNullException">Ném ra khi store là null.</exception>
    public JobService(
        IJobStore store,
        IJobMetrics? metrics = null,
        ISnsAlertPublisher? alertPublisher = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _metrics = metrics;
        _alertPublisher = alertPublisher;
    }

    /// <inheritdoc />
    public async Task<JobRef> EnqueueAsync(JobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Sinh mã băm Fingerprint từ nội dung yêu cầu để chống xung đột dữ liệu (chuẩn hóa ResourceId theo kiểu không phân biệt hoa thường)
        var fingerprint = JobIdempotency.ComputeRequestFingerprint(request.JobType, request.PayloadReference, request.Scope.Kind, request.Scope.ResourceId);

        var job = await _store.EnqueueAsync(request, fingerprint, cancellationToken);
        return JobRef.Create(job.Id);
    }

    /// <inheritdoc />
    public async Task<JobStatusDto> GetJobStatusAsync(JobRef jobRef, string requesterId, JobScope requesterScope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobRef);
        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID is required.", nameof(requesterId));

        if (requesterScope is null)
        {
            throw new UnauthorizedAccessException("Requester scope is required for scoped job access.");
        }

        var job = await _store.GetByIdAsync(jobRef.Value, cancellationToken);
        if (job is null)
            throw new KeyNotFoundException($"Job with ID '{jobRef.Value}' was not found.");

        // Kiểm tra phân quyền phạm vi: Tuyệt đối không cho phép truy cập tác vụ chéo phạm vi (cả Kind và ResourceId)
        if (requesterScope.Kind != job.Scope.Kind ||
            !string.Equals(requesterScope.ResourceId, job.Scope.ResourceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Requester is not authorized to access this job.");
        }

        // Kiểm tra quyền sở hữu tác vụ: Chỉ chủ sở hữu mới được truy cập
        if (!string.Equals(job.CreatedBy, requesterId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Requester is not authorized to access this job.");
        }

        return MapToDto(job);
    }

    /// <inheritdoc />
    public async Task<bool> CancelJobAsync(JobRef jobRef, string requesterId, JobScope requesterScope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobRef);
        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID is required.", nameof(requesterId));

        if (requesterScope is null)
        {
            throw new UnauthorizedAccessException("Requester scope is required for scoped job cancellation.");
        }

        var job = await _store.GetByIdAsync(jobRef.Value, cancellationToken);
        if (job is null)
            throw new KeyNotFoundException($"Job with ID '{jobRef.Value}' was not found.");

        // Kiểm tra phân quyền phạm vi: Tuyệt đối không cho phép hủy tác vụ chéo phạm vi (cả Kind và ResourceId)
        if (requesterScope.Kind != job.Scope.Kind ||
            !string.Equals(requesterScope.ResourceId, job.Scope.ResourceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Requester is not authorized to cancel this job.");
        }

        // Kiểm tra quyền sở hữu tác vụ: Chỉ chủ sở hữu mới được hủy tác vụ
        if (!string.Equals(job.CreatedBy, requesterId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Requester is not authorized to cancel this job.");
        }

        return await _store.RequestCancellationAsync(jobRef.Value, requesterId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobStatusDto>> ListJobsAsync(JobScope scope, string requesterId, JobScope requesterScope, JobStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID is required.", nameof(requesterId));

        if (requesterScope is null)
        {
            throw new UnauthorizedAccessException("Requester scope is required to list scoped jobs.");
        }

        // Kiểm tra phân quyền phạm vi: Chỉ được liệt kê các tác vụ trong phạm vi của chính người yêu cầu (cả Kind và ResourceId)
        if (requesterScope.Kind != scope.Kind ||
            !string.Equals(requesterScope.ResourceId, scope.ResourceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Requester from scope '{requesterScope.Kind}:{requesterScope.ResourceId}' is not authorized to list jobs in scope '{scope.Kind}:{scope.ResourceId}'.");
        }

        var jobs = await _store.ListByScopeAsync(scope, statusFilter, cancellationToken);
        return jobs
            .Where(job => string.Equals(job.CreatedBy, requesterId, StringComparison.Ordinal))
            .Select(MapToDto)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<JobStatusDto> RetryJobAsync(JobRef jobRef, string requesterId, JobScope requesterScope, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobRef);
        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID is required.", nameof(requesterId));

        if (requesterScope is null)
        {
            throw new UnauthorizedAccessException("Requester scope is required for scoped job retry.");
        }

        var job = await _store.GetByIdAsync(jobRef.Value, cancellationToken);
        if (job is null)
            throw new KeyNotFoundException($"Job with ID '{jobRef.Value}' was not found.");

        // Kiểm tra phân quyền phạm vi: Tuyệt đối không cho phép retry tác vụ chéo phạm vi (cả Kind và ResourceId)
        if (requesterScope.Kind != job.Scope.Kind ||
            !string.Equals(requesterScope.ResourceId, job.Scope.ResourceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Requester is not authorized to retry this job.");
        }

        // Kiểm tra trạng thái tác vụ: chỉ cho phép retry khi tác vụ đã kết thúc (Failed hoặc Cancelled)
        if (job.Status == JobStatus.Running || job.Status == JobStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot retry a job that is currently {job.Status.ToString().ToLowerInvariant()}.");
        }

        if (job.Status == JobStatus.Completed)
        {
            throw new InvalidOperationException("Cannot retry a job that has already completed successfully.");
        }

        // Chặn retry đối với các tác vụ lỗi vĩnh viễn (Permanent)
        if (!IsEligibleForRetry(job))
        {
            throw new JobNotRetryEligibleException($"Job '{jobRef.Value}' is not eligible for retry because it failed with permanent error '{job.ErrorCode}'.");
        }

        var retriedJob = await _store.RetryJobAsync(jobRef.Value, requesterId, reason, cancellationToken);
        return MapToDto(retriedJob);
    }

    /// <summary>
    /// Đánh giá điều kiện cho phép thử lại (Retry Eligibility).
    /// Áp dụng nhất quán giữa DTO hiển thị và kiểm tra nghiệp vụ khi gọi API Retry.
    /// </summary>
    public static bool IsEligibleForRetry(BackgroundJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.Status != JobStatus.Failed && job.Status != JobStatus.Cancelled)
        {
            return false;
        }

        // Tác vụ Cancelled không có mã lỗi hoặc mã lỗi không phải Permanent được phép thử lại
        if (job.Status == JobStatus.Cancelled)
        {
            return string.IsNullOrWhiteSpace(job.ErrorCode) ||
                   ErrorClassifier.Classify(job.ErrorCode) != ErrorCategory.Permanent;
        }

        // Với tác vụ Failed: chỉ cho phép thử lại nếu mã lỗi KHÔNG phải lỗi vĩnh viễn (Permanent).
        // Cho phép thử lại tác vụ cạn ngân sách retry tự động (RETRY_BUDGET_EXHAUSTED) hoặc lỗi tạm thời.
        // Các mã lỗi không xác định sẽ mặc định bị coi là Permanent theo ErrorClassifier và bị từ chối.
        if (string.IsNullOrWhiteSpace(job.ErrorCode))
        {
            return false;
        }

        return ErrorClassifier.Classify(job.ErrorCode) != ErrorCategory.Permanent;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobAttempt>> GetJobAttemptsAsync(JobRef jobRef, string requesterId, JobScope requesterScope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobRef);
        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID is required.", nameof(requesterId));

        if (requesterScope is null)
        {
            throw new UnauthorizedAccessException("Requester scope is required to view job attempts.");
        }

        var job = await _store.GetByIdAsync(jobRef.Value, cancellationToken);
        if (job is null)
            throw new KeyNotFoundException($"Job with ID '{jobRef.Value}' was not found.");

        if (requesterScope.Kind != job.Scope.Kind ||
            !string.Equals(requesterScope.ResourceId, job.Scope.ResourceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Requester is not authorized to view attempts for this job.");
        }

        return await _store.GetAttemptsAsync(jobRef.Value, cancellationToken);
    }

    /// <summary>
    /// Lấy bản tổng hợp chỉ số độ tin cậy và hiệu năng hệ thống (Reliability Summary).
    /// </summary>
    public Task<ReliabilitySummaryDto> GetReliabilitySummaryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = _metrics?.GetSnapshot() ?? new JobMetricsSnapshot();
        var recentAlerts = _alertPublisher?.GetRecentAlerts() ?? Array.Empty<IncidentAlert>();

        var summary = new ReliabilitySummaryDto
        {
            TotalEnqueued = snapshot.TotalEnqueued,
            TotalCompleted = snapshot.TotalCompleted,
            TotalFailed = snapshot.TotalFailed,
            TotalRetries = snapshot.TotalRetries,
            ActivePending = snapshot.ActivePending,
            ActiveRunning = snapshot.ActiveRunning,
            StaleLeaseRecoveries = snapshot.StaleLeaseRecoveries,
            SuccessRatePercentage = snapshot.SuccessRatePercentage,
            FailureRatePercentage = snapshot.FailureRatePercentage,
            AverageDurationMs = snapshot.AverageDurationMs,
            P50DurationMs = snapshot.P50DurationMs,
            P90DurationMs = snapshot.P90DurationMs,
            P95DurationMs = snapshot.P95DurationMs,
            CircuitBreakerStates = snapshot.CircuitBreakerStates,
            ProviderErrors = snapshot.ProviderErrors,
            RecentAlerts = recentAlerts,
            CalculatedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(summary);
    }

    /// <summary>
    /// Kích hoạt một cảnh báo sự cố diễn tập (Drill Test Alert) để kiểm tra khả năng định tuyến và kiểm định payload SNS.
    /// </summary>
    public async Task<PublishAlertResult> TriggerTestAlertAsync(
        string requesterId,
        string? title = null,
        string? details = null,
        string? jobType = null,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_alertPublisher is null)
        {
            return PublishAlertResult.Failed("SNS Alert Publisher is not configured.");
        }

        var alert = new IncidentAlert
        {
            IncidentType = IncidentTypes.DrillTestAlert,
            Severity = IncidentSeverity.Info,
            Title = string.IsNullOrWhiteSpace(title) ? "Controlled Failure Drill Test Alert" : title.Trim(),
            Details = string.IsNullOrWhiteSpace(details)
                ? $"Drill initiated by {requesterId} to verify Amazon SNS alert routing and notification payload integrity."
                : details.Trim(),
            JobType = jobType,
            Provider = provider ?? "AWS_BEDROCK",
            RecommendedAction = "No action required. This is an operational drill to confirm notification channel delivery.",
            RunbookUrl = "https://github.com/Hungle2910/ai-exam-bank/blob/dev/Backend/docs/M5_04/M5_04_FAILURE_DRILL_RUNBOOK.md",
            TimestampUtc = DateTimeOffset.UtcNow
        };

        return await _alertPublisher.PublishIncidentAlertAsync(alert, cancellationToken);
    }

    private static JobStatusDto MapToDto(BackgroundJob job)
    {
        var isEligible = IsEligibleForRetry(job);

        var actionableReason = string.IsNullOrWhiteSpace(job.ErrorCode) && string.IsNullOrWhiteSpace(job.SafeErrorMessage)
            ? null
            : AwsErrorMapping.GetActionableMessage(job.ErrorCode, job.SafeErrorMessage);

        return new(
            JobRef.Create(job.Id),
            job.Type,
            job.Status,
            job.ProgressPercentage,
            job.ResultId,
            job.ErrorCode,
            job.SafeErrorMessage,
            isEligible,
            actionableReason);
    }
}
