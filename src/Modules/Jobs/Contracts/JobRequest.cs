namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>Metadata for a durable job. PayloadReference points to access-controlled data, not raw exam content.</summary>
public sealed record JobRequest
{
    private JobRequest(string jobType, string payloadReference, string requesterId, JobScope scope, string idempotencyKey, string correlationId)
        => (JobType, PayloadReference, RequesterId, Scope, IdempotencyKey, CorrelationId)
            = (jobType, payloadReference, requesterId, scope, idempotencyKey, correlationId);

    public string JobType { get; }
    public string PayloadReference { get; }
    public string RequesterId { get; }
    public JobScope Scope { get; }
    public string IdempotencyKey { get; }
    public string CorrelationId { get; }

    public static JobRequest Create(string jobType, string payloadReference, string requesterId, JobScope scope, string idempotencyKey, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(jobType)) throw new ArgumentException("A job type is required.", nameof(jobType));
        if (string.IsNullOrWhiteSpace(payloadReference)) throw new ArgumentException("A payload reference is required.", nameof(payloadReference));
        if (string.IsNullOrWhiteSpace(requesterId)) throw new ArgumentException("A requester ID is required.", nameof(requesterId));
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("A correlation ID is required.", nameof(correlationId));
        return new JobRequest(jobType, payloadReference, requesterId, scope, idempotencyKey, correlationId);
    }
}
