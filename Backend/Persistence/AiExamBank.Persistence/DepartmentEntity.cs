namespace AiExamBank.Persistence;

public sealed class DepartmentEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public required string Name { get; set; }
}
