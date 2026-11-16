using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Persistence.Questions;

public sealed class QuestionRevisionEntity
{
    public Guid QuestionId { get; set; }
    public int Revision { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TopicId { get; set; }
    public Difficulty Difficulty { get; set; }
    public required string Stem { get; set; }
    public required string CorrectOption { get; set; }
    public QuestionStatus Status { get; set; }
    // Mỗi lần gửi/duyệt tăng phiên bản; EF dùng nó để chặn quyết định cũ của người duyệt thứ hai.
    public long ReviewVersion { get; set; } = 1;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public QuestionOrigin Origin { get; set; }
    public string? SourceText { get; set; }
    public Guid? ImportBatchId { get; set; }
    public int? ImportRowNumber { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
