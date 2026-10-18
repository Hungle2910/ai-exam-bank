using System.Collections.Concurrent;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Kho lưu trữ tác vụ nền trong bộ nhớ (In-Memory Job Store) an toàn đa luồng.
/// Hiện thực đầy đủ hợp đồng IJobStore phục vụ chạy độc lập, kiểm thử đơn vị và tích hợp
/// mà không bắt buộc phải có hệ quản trị cơ sở dữ liệu bên ngoài.
/// </summary>
public sealed class InMemoryJobStore : IJobStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, BackgroundJob> _jobs = new();
    private readonly Dictionary<string, Guid> _idempotencyIndex = new();
    private readonly Dictionary<Guid, List<JobAttempt>> _attempts = new();
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>
    /// Khởi tạo In-Memory Job Store với đồng hồ thời gian (mặc định dùng DateTimeOffset.UtcNow).
    /// Cho phép truyền mock clock để kiểm thử chính xác các tình huống quá hạn lease và retry backoff.
    /// </summary>
    /// <param name="clock">Hàm lấy thời gian hiện tại.</param>
    public InMemoryJobStore(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public Task<BackgroundJob> EnqueueAsync(JobRequest request, string requestFingerprint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(requestFingerprint))
            throw new ArgumentException("Request fingerprint cannot be empty.", nameof(requestFingerprint));

        cancellationToken.ThrowIfCancellationRequested();

        var idempotencyKey = BuildIdempotencyKey(request);
        var now = _clock();

        lock (_syncRoot)
        {
            if (_idempotencyIndex.TryGetValue(idempotencyKey, out var existingId))
            {
                var existingJob = _jobs[existingId];
                if (existingJob.RequestFingerprint == requestFingerprint)
                {
                    // Cùng khóa idempotency + cùng fingerprint => Trả về Job đã có (Idempotent return)
                    return Task.FromResult(CloneJob(existingJob));
                }

                // Cùng khóa idempotency + khác fingerprint => Xung đột dữ liệu yêu cầu
                throw new JobConflictException(
                    $"Job with idempotency key '{request.IdempotencyKey}' already exists with a different request fingerprint.");
            }

            var newJob = new BackgroundJob
            {
                Id = Guid.NewGuid(),
                Type = request.JobType,
                Status = JobStatus.Pending,
                PayloadReference = request.PayloadReference,
                ResultId = null,
                CreatedBy = request.RequesterId,
                Scope = request.Scope,
                IdempotencyKey = request.IdempotencyKey,
                RequestFingerprint = requestFingerprint,
                CorrelationId = request.CorrelationId,
                AvailableAt = now,
                CancellationRequestedAt = null,
                AttemptCount = 0,
                ProgressPercentage = null,
                ErrorCode = null,
                SafeErrorMessage = null,
                CreatedAt = now,
                UpdatedAt = now
            };

            _jobs[newJob.Id] = newJob;
            _idempotencyIndex[idempotencyKey] = newJob.Id;
            _attempts[newJob.Id] = new List<JobAttempt>();

            return Task.FromResult(CloneJob(newJob));
        }
    }

    /// <inheritdoc />
    public Task<BackgroundJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                return Task.FromResult<BackgroundJob?>(CloneJob(job));
            }

            return Task.FromResult<BackgroundJob?>(null);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BackgroundJob>> ListByScopeAsync(JobScope scope, JobStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var results = _jobs.Values
                .Where(j => j.Scope.Kind == scope.Kind &&
                            string.Equals(j.Scope.ResourceId, scope.ResourceId, StringComparison.OrdinalIgnoreCase))
                .Where(j => !statusFilter.HasValue || j.Status == statusFilter.Value)
                .OrderByDescending(j => j.CreatedAt)
                .Select(CloneJob)
                .ToList();

            return Task.FromResult<IReadOnlyList<BackgroundJob>>(results);
        }
    }

    /// <inheritdoc />
    public Task<JobClaimResult?> ClaimNextJobAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker ID cannot be empty.", nameof(workerId));

        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        lock (_syncRoot)
        {
            // 1. Tìm tác vụ Pending đã đến hạn chạy (AvailableAt <= now)
            var eligibleJob = _jobs.Values
                .Where(j => j.Status == JobStatus.Pending && (j.AvailableAt == null || j.AvailableAt <= now))
                .OrderBy(j => j.AvailableAt ?? j.CreatedAt)
                .FirstOrDefault();

            // 2. Nếu không có Pending, tìm tác vụ Running nhưng đã bị bỏ hoang (Lease quá hạn)
            if (eligibleJob is null)
            {
                foreach (var runningJob in _jobs.Values.Where(j => j.Status == JobStatus.Running))
                {
                    if (_attempts.TryGetValue(runningJob.Id, out var jobAttempts))
                    {
                        var lastAttempt = jobAttempts.LastOrDefault();
                        if (lastAttempt != null && lastAttempt.Status == AttemptStatus.Started && lastAttempt.LeaseExpiresAt < now)
                        {
                            // Đánh dấu lượt cũ là Bị bỏ hoang (Abandoned)
                            lastAttempt.Status = AttemptStatus.Abandoned;
                            lastAttempt.FinishedAt = now;

                            if (runningJob.AttemptCount >= 3)
                            {
                                // Hết ngân sách retry
                                runningJob.Status = JobStatus.Failed;
                                runningJob.ErrorCode = "RETRY_BUDGET_EXHAUSTED";
                                runningJob.SafeErrorMessage = "Job exceeded maximum of 3 attempts after lease expiration.";
                                runningJob.UpdatedAt = now;
                                continue;
                            }

                            var retryDelayMinutes = runningJob.AttemptCount == 1 ? 1 : 5;
                            runningJob.Status = JobStatus.Pending;
                            runningJob.AvailableAt = now.AddMinutes(retryDelayMinutes);
                            runningJob.UpdatedAt = now;
                            continue;
                        }
                    }
                }
            }

            if (eligibleJob is null)
            {
                return Task.FromResult<JobClaimResult?>(null);
            }

            // Tiến hành Claim tác vụ
            var attemptNumber = eligibleJob.AttemptCount + 1;
            var leaseToken = Guid.NewGuid();
            var leaseExpiresAt = now.Add(leaseDuration);

            var attempt = new JobAttempt
            {
                Id = Guid.NewGuid(),
                JobId = eligibleJob.Id,
                AttemptNumber = attemptNumber,
                Status = AttemptStatus.Started,
                WorkerId = workerId,
                LeaseToken = leaseToken,
                StartedAt = now,
                HeartbeatAt = now,
                LeaseExpiresAt = leaseExpiresAt,
                FinishedAt = null,
                ErrorCode = null,
                SafeErrorMessage = null
            };

            eligibleJob.Status = JobStatus.Running;
            eligibleJob.AttemptCount = attemptNumber;
            eligibleJob.UpdatedAt = now;

            if (!_attempts.TryGetValue(eligibleJob.Id, out var list))
            {
                list = new List<JobAttempt>();
                _attempts[eligibleJob.Id] = list;
            }
            list.Add(attempt);

            return Task.FromResult<JobClaimResult?>(new JobClaimResult(
                CloneJob(eligibleJob),
                CloneAttempt(attempt),
                leaseToken));
        }
    }

    /// <inheritdoc />
    public Task<bool> HeartbeatAsync(Guid jobId, Guid leaseToken, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        lock (_syncRoot)
        {
            if (!_jobs.TryGetValue(jobId, out var job) || job.Status != JobStatus.Running)
                return Task.FromResult(false);

            if (!_attempts.TryGetValue(jobId, out var attempts))
                return Task.FromResult(false);

            var currentAttempt = attempts.LastOrDefault();
            if (currentAttempt is null || currentAttempt.LeaseToken != leaseToken || currentAttempt.Status != AttemptStatus.Started)
                return Task.FromResult(false);

            // Gia hạn Lease
            currentAttempt.HeartbeatAt = now;
            currentAttempt.LeaseExpiresAt = now.Add(leaseDuration);
            job.UpdatedAt = now;

            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> UpdateProgressAsync(Guid jobId, Guid leaseToken, int progressPercentage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        lock (_syncRoot)
        {
            if (!_jobs.TryGetValue(jobId, out var job) || job.Status != JobStatus.Running)
                return Task.FromResult(false);

            if (!_attempts.TryGetValue(jobId, out var attempts))
                return Task.FromResult(false);

            var currentAttempt = attempts.LastOrDefault();
            if (currentAttempt is null || currentAttempt.LeaseToken != leaseToken || currentAttempt.Status != AttemptStatus.Started)
                return Task.FromResult(false);

            job.ProgressPercentage = Math.Clamp(progressPercentage, 0, 100);
            job.UpdatedAt = now;

            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> CompleteJobAsync(Guid jobId, Guid leaseToken, string? resultId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        lock (_syncRoot)
        {
            if (!_jobs.TryGetValue(jobId, out var job) || job.Status != JobStatus.Running)
                return Task.FromResult(false);

            if (!_attempts.TryGetValue(jobId, out var attempts))
                return Task.FromResult(false);

            var currentAttempt = attempts.LastOrDefault();
            if (currentAttempt is null || currentAttempt.LeaseToken != leaseToken || currentAttempt.Status != AttemptStatus.Started)
                return Task.FromResult(false);

            currentAttempt.Status = AttemptStatus.Completed;
            currentAttempt.FinishedAt = now;

            job.Status = JobStatus.Completed;
            job.ErrorCode = null;
            job.SafeErrorMessage = null;
            job.ResultId = resultId;
            job.ProgressPercentage = 100;
            job.UpdatedAt = now;

            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> FailJobAsync(Guid jobId, Guid leaseToken, string errorCode, string safeErrorMessage, bool isTransient, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        lock (_syncRoot)
        {
            if (!_jobs.TryGetValue(jobId, out var job) || job.Status != JobStatus.Running)
                return Task.FromResult(false);

            if (!_attempts.TryGetValue(jobId, out var attempts))
                return Task.FromResult(false);

            var currentAttempt = attempts.LastOrDefault();
            if (currentAttempt is null || currentAttempt.LeaseToken != leaseToken || currentAttempt.Status != AttemptStatus.Started)
                return Task.FromResult(false);

            currentAttempt.Status = AttemptStatus.Failed;
            currentAttempt.ErrorCode = errorCode;
            currentAttempt.SafeErrorMessage = safeErrorMessage;
            currentAttempt.FinishedAt = now;

            // Chính sách Retry M5-01:
            // Tối đa 3 lần chạy.
            // Lần 1 thất bại => retry sau 1 phút
            // Lần 2 thất bại => retry sau 5 phút
            // Lần 3 thất bại => Failed vĩnh viễn
            if (isTransient && job.AttemptCount < 3)
            {
                var retryDelayMinutes = job.AttemptCount == 1 ? 1 : 5;
                job.Status = JobStatus.Pending;
                job.AvailableAt = now.AddMinutes(retryDelayMinutes);
                job.ErrorCode = errorCode;
                job.SafeErrorMessage = safeErrorMessage;
                job.UpdatedAt = now;
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.ErrorCode = isTransient && job.AttemptCount >= 3 ? "RETRY_BUDGET_EXHAUSTED" : errorCode;
                job.SafeErrorMessage = safeErrorMessage;
                job.UpdatedAt = now;
            }

            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> RequestCancellationAsync(Guid jobId, string requesterId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        lock (_syncRoot)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return Task.FromResult(false);

            if (job.Status == JobStatus.Cancelled)
                return Task.FromResult(true); // Idempotent cancellation

            if (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed)
                return Task.FromResult(false); // Không thể hủy tác vụ đã kết thúc

            if (job.Status == JobStatus.Pending)
            {
                job.Status = JobStatus.Cancelled;
                job.CancellationRequestedAt = now;
                job.UpdatedAt = now;
                return Task.FromResult(true);
            }

            if (job.Status == JobStatus.Running)
            {
                job.Status = JobStatus.Cancelled;
                job.CancellationRequestedAt = now;
                job.UpdatedAt = now;

                if (_attempts.TryGetValue(jobId, out var attempts))
                {
                    var currentAttempt = attempts.LastOrDefault();
                    if (currentAttempt != null && currentAttempt.Status == AttemptStatus.Started)
                    {
                        currentAttempt.Status = AttemptStatus.Failed;
                        currentAttempt.ErrorCode = "CANCELLED";
                        currentAttempt.SafeErrorMessage = "Job execution was cancelled by user.";
                        currentAttempt.FinishedAt = now;
                    }
                }

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();
        var recoveredCount = 0;

        lock (_syncRoot)
        {
            foreach (var job in _jobs.Values.Where(j => j.Status == JobStatus.Running))
            {
                if (!_attempts.TryGetValue(job.Id, out var attempts))
                    continue;

                var lastAttempt = attempts.LastOrDefault();
                if (lastAttempt != null && lastAttempt.Status == AttemptStatus.Started && lastAttempt.LeaseExpiresAt < now)
                {
                    lastAttempt.Status = AttemptStatus.Abandoned;
                    lastAttempt.FinishedAt = now;
                    recoveredCount++;

                    if (job.AttemptCount >= 3)
                    {
                        job.Status = JobStatus.Failed;
                        job.ErrorCode = "RETRY_BUDGET_EXHAUSTED";
                        job.SafeErrorMessage = "Job attempt limit reached after lease expiration.";
                        job.UpdatedAt = now;
                    }
                    else
                    {
                        var retryDelayMinutes = job.AttemptCount == 1 ? 1 : 5;
                        job.Status = JobStatus.Pending;
                        job.AvailableAt = now.AddMinutes(retryDelayMinutes);
                        job.UpdatedAt = now;
                    }
                }
            }
        }

        return Task.FromResult(recoveredCount);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các Attempt của một Job (dùng cho kiểm thử và thanh tra).
    /// </summary>
    public IReadOnlyList<JobAttempt> GetAttempts(Guid jobId)
    {
        lock (_syncRoot)
        {
            if (_attempts.TryGetValue(jobId, out var list))
            {
                return list.Select(CloneAttempt).ToList();
            }

            return Array.Empty<JobAttempt>();
        }
    }

    private static string BuildIdempotencyKey(JobRequest request) =>
        $"{request.Scope.Kind}:{request.Scope.ResourceId.ToUpperInvariant()}:{request.RequesterId}:{request.JobType}:{request.IdempotencyKey}";

    private static BackgroundJob CloneJob(BackgroundJob job) => new()
    {
        Id = job.Id,
        Type = job.Type,
        Status = job.Status,
        PayloadReference = job.PayloadReference,
        ResultId = job.ResultId,
        CreatedBy = job.CreatedBy,
        Scope = job.Scope,
        IdempotencyKey = job.IdempotencyKey,
        RequestFingerprint = job.RequestFingerprint,
        CorrelationId = job.CorrelationId,
        AvailableAt = job.AvailableAt,
        CancellationRequestedAt = job.CancellationRequestedAt,
        AttemptCount = job.AttemptCount,
        ProgressPercentage = job.ProgressPercentage,
        ErrorCode = job.ErrorCode,
        SafeErrorMessage = job.SafeErrorMessage,
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt
    };

    private static JobAttempt CloneAttempt(JobAttempt attempt) => new()
    {
        Id = attempt.Id,
        JobId = attempt.JobId,
        AttemptNumber = attempt.AttemptNumber,
        Status = attempt.Status,
        WorkerId = attempt.WorkerId,
        LeaseToken = attempt.LeaseToken,
        StartedAt = attempt.StartedAt,
        HeartbeatAt = attempt.HeartbeatAt,
        LeaseExpiresAt = attempt.LeaseExpiresAt,
        FinishedAt = attempt.FinishedAt,
        ErrorCode = attempt.ErrorCode,
        SafeErrorMessage = attempt.SafeErrorMessage
    };
}
