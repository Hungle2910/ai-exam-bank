namespace AiExamBank.Persistence.Questions;

public sealed class TopicEntity
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
}
