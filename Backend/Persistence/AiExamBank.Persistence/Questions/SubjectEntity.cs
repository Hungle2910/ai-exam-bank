namespace AiExamBank.Persistence.Questions;

public sealed class SubjectEntity
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
}
