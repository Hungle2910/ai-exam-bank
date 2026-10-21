using AiExamBank.Modules.Identity.Authorization;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api;

/// <summary>Checks current database assignments against scope loaded from the actual resource.</summary>
public sealed class ResourceAuthorizationService(AiExamBankDbContext db)
{
    public async Task<bool> CanAccessAsync(
        Guid actorUserId,
        SchoolResource persistedResource,
        ResourceAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persistedResource);

        var entities = await db.RoleAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == actorUserId &&
                assignment.SchoolId == persistedResource.SchoolId)
            .ToListAsync(cancellationToken);

        var assignments = entities.Select(assignment => RoleAssignment.Create(
            assignment.UserId,
            assignment.Role,
            assignment.SchoolId,
            assignment.DepartmentId)).ToArray();

        return ResourceAccessPolicy.CanAccess(
            actorUserId, persistedResource, action, assignments);
    }
}
