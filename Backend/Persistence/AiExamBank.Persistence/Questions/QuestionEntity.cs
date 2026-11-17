namespace AiExamBank.Persistence.Questions;

/// <summary>The stable identity of a question. Its content lives in immutable revisions.</summary>
public sealed class QuestionEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int LatestRevision { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
