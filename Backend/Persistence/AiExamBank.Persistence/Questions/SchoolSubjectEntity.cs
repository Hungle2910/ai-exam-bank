namespace AiExamBank.Persistence.Questions;

/// <summary>A subject offered by a school and the one department that owns it there.</summary>
public sealed class SchoolSubjectEntity
{
    public Guid SchoolId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid DepartmentId { get; set; }
}
