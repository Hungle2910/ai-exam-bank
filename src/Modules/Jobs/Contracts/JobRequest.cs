namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>Metadata for a durable job. PayloadReference points to access-controlled data, not raw exam content.</summary>
public sealed record JobRequest(
    string JobType,
    string PayloadReference,
    string RequesterId,
    JobScope Scope,
    string IdempotencyKey,
    string CorrelationId);
