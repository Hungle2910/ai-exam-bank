using AiExamBank.Modules.Identity.Contracts;

namespace AiExamBank.Modules.Identity.Authorization;

public enum ResourceKind
{
    Question,
    Blueprint,
    Job
}

public enum ResourceAction
{
    Read,
    Edit,
    Review
}

/// <summary>
/// The caller must load these values from the persisted resource, never from a client-supplied scope.
/// Ministry exam access requires a separate event assignment and is denied by this school policy.
/// </summary>
public sealed record SchoolResource(
    ResourceKind Kind,
    Guid SchoolId,
    Guid? DepartmentId,
    Guid OwnerUserId,
    bool IsSubmittedForReview = false);

public static class ResourceAccessPolicy
{
    public static bool CanAccess(
        Guid actorUserId,
        SchoolResource resource,
        ResourceAction action,
        IReadOnlyCollection<RoleAssignment> currentAssignments)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(currentAssignments);

        if (actorUserId == Guid.Empty || resource.SchoolId == Guid.Empty ||
            resource.OwnerUserId == Guid.Empty || !Enum.IsDefined(resource.Kind) ||
            !Enum.IsDefined(action))
            return false;

        var scoped = currentAssignments.Where(assignment =>
            assignment.UserId == actorUserId && assignment.SchoolId == resource.SchoolId);

        if (resource.Kind == ResourceKind.Job)
            return action == ResourceAction.Read &&
                   actorUserId == resource.OwnerUserId &&
                   scoped.Any(assignment => assignment.Role is
                       AppRole.Teacher or AppRole.DepartmentHead or AppRole.SchoolAdmin);

        if (resource.DepartmentId is not Guid departmentId || departmentId == Guid.Empty)
            return false;

        var isTeacher = scoped.Any(assignment =>
            assignment.Role == AppRole.Teacher && assignment.DepartmentId == departmentId);
        var isDepartmentHead = scoped.Any(assignment =>
            assignment.Role == AppRole.DepartmentHead && assignment.DepartmentId == departmentId);

        return action switch
        {
            ResourceAction.Read =>
                (isTeacher && actorUserId == resource.OwnerUserId) ||
                (isDepartmentHead && resource.IsSubmittedForReview),
            ResourceAction.Edit => isTeacher && actorUserId == resource.OwnerUserId,
            ResourceAction.Review => isDepartmentHead && resource.IsSubmittedForReview &&
                                     actorUserId != resource.OwnerUserId,
            _ => false
        };
    }
}
