using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Domain;

/// <summary>Draft persistence shape; EF mapping and transition logic are not implemented yet.</summary>
public sealed class BackgroundJob
{
    public Guid Id { get; set; }
    public required string Type { get; set; }
    public JobStatus Status { get; set; }
    public required string PayloadReference { get; set; }
    public string? ResultId { get; set; }
    public required string CreatedBy { get; set; }
    public required JobScope Scope { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
