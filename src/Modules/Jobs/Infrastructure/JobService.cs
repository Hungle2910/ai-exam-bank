using System.Security.Cryptography;
using System.Text;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Hiện thực dịch vụ quản lý tác vụ ngầm (Job Service) tích hợp cả IJobPublisher và IJobTracker.
/// Chịu trách nhiệm băm Fingerprint, điều phối Enqueue qua IJobStore và bảo vệ phân quyền khi đọc/hủy Job.
/// </summary>
public sealed class JobService : IJobPublisher, IJobTracker
{
    private readonly IJobStore _store;

    public JobService(IJobStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task<JobRef> EnqueueAsync(JobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Sinh mã băm Fingerprint từ nội dung yêu cầu để chống xung đột dữ liệu
        var rawInput = $"{request.JobType}:{request.PayloadReference}:{request.Scope.Kind}:{request.Scope.ResourceId}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawInput));
        var fingerprint = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var job = await _store.EnqueueAsync(request, fingerprint, cancellationToken);
        return JobRef.Create(job.Id);
    }

    /// <inheritdoc />
    public async Task<JobStatusDto> GetJobStatusAsync(JobRef jobRef, string requesterId, string? requesterScopeId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobRef);
        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID is required.", nameof(requesterId));

        var job = await _store.GetByIdAsync(jobRef.Value, cancellationToken);
        if (job is null)
            throw new KeyNotFoundException($"Job with ID '{jobRef.Value}' was not found.");

        // Kiểm tra phân quyền phạm vi: Không cho phép truy cập tác vụ chéo trường
        if (!string.IsNullOrWhiteSpace(requesterScopeId) &&
            !string.Equals(requesterScopeId, job.Scope.ResourceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Requester from scope '{requesterScopeId}' is not authorized to access job in scope '{job.Scope.ResourceId}'.");
        }

        return MapToDto(job);
    }

    /// <inheritdoc />
    public async Task<bool> CancelJobAsync(JobRef jobRef, string requesterId, string? requesterScopeId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobRef);
        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID is required.", nameof(requesterId));

        var job = await _store.GetByIdAsync(jobRef.Value, cancellationToken);
        if (job is null)
            return false;

        // Kiểm tra phân quyền phạm vi: Không cho phép hủy tác vụ chéo trường
        if (!string.IsNullOrWhiteSpace(requesterScopeId) &&
            !string.Equals(requesterScopeId, job.Scope.ResourceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Requester from scope '{requesterScopeId}' is not authorized to cancel job in scope '{job.Scope.ResourceId}'.");
        }

        return await _store.RequestCancellationAsync(jobRef.Value, requesterId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobStatusDto>> ListJobsAsync(JobScope scope, string requesterId, JobStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID is required.", nameof(requesterId));

        var jobs = await _store.ListByScopeAsync(scope, statusFilter, cancellationToken);
        return jobs.Select(MapToDto).ToList();
    }

    private static JobStatusDto MapToDto(BackgroundJob job) =>
        new(
            JobRef.Create(job.Id),
            job.Status,
            job.ProgressPercentage,
            job.ResultId,
            job.ErrorCode,
            job.SafeErrorMessage);
}
