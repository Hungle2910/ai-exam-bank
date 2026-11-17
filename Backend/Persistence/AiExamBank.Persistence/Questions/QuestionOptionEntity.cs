namespace AiExamBank.Persistence.Questions;

public sealed class QuestionOptionEntity
{
    public Guid QuestionId { get; set; }
    public int Revision { get; set; }
    public required string Label { get; set; }
    public required string Text { get; set; }
}
