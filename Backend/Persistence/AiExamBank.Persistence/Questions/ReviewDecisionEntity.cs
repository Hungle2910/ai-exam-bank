namespace AiExamBank.Persistence.Questions;

/// <summary>Durable evidence of the one final decision on a question revision.</summary>
public sealed class ReviewDecisionEntity
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public int Revision { get; set; }
    public Guid ReviewerUserId { get; set; }
    public required string Decision { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset DecidedAtUtc { get; set; }
}
