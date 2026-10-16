using AiExamBank.Modules.Identity.Contracts;

namespace AiExamBank.Persistence;

public sealed class RoleAssignmentAuditEntity
{
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid TargetUserId { get; set; }
    public required string Action { get; set; }
    public AppRole Role { get; set; }
    public Guid? SchoolId { get; set; }
    public Guid? DepartmentId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
