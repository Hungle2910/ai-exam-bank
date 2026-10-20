namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>Persists a scoped request and returns its stable job ID.</summary>
public interface IJobPublisher
{
    /// <summary>The implementation must enforce idempotency within the requester's scope.</summary>
    Task<JobRef> EnqueueAsync(JobRequest request, CancellationToken cancellationToken);
}
