namespace AiExamBank.Modules.Jobs.Domain;

public enum AttemptStatus
{
    Started,
    Completed,
    Failed,
    Abandoned
}

/// <summary>Draft attempt shape; a future atomic lease claim must guard these fields.</summary>
public sealed class JobAttempt
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int AttemptNumber { get; set; }
    public AttemptStatus Status { get; set; }
    public required string WorkerId { get; set; }
    public Guid LeaseToken { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset HeartbeatAt { get; set; }
    public DateTimeOffset LeaseExpiresAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? SafeErrorMessage { get; set; }
}
