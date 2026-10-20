using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api;

internal static class AdminAuthorization
{
    public static Task<bool> IsMinistryAdminAsync(AiExamBankDbContext db, Guid actorUserId) =>
        db.RoleAssignments.AsNoTracking().AnyAsync(assignment =>
            assignment.UserId == actorUserId &&
            assignment.Role == AppRole.MinistryAdmin);

    public static Task<bool> IsSchoolAdminAsync(
        AiExamBankDbContext db, AppUser actor, Guid schoolId) =>
        actor.SchoolId == schoolId
            ? db.RoleAssignments.AsNoTracking().AnyAsync(assignment =>
                assignment.UserId == actor.Id &&
                assignment.Role == AppRole.SchoolAdmin &&
                assignment.SchoolId == schoolId)
            : Task.FromResult(false);

    public static Task<bool> CanManageRoleAsync(
        AiExamBankDbContext db, AppUser actor, AppRole role, Guid? schoolId)
    {
        if (schoolId is null)
            return Task.FromResult(false);

        return role switch
        {
            AppRole.Teacher or AppRole.DepartmentHead =>
                IsSchoolAdminAsync(db, actor, schoolId.Value),
            AppRole.SchoolAdmin => IsMinistryAdminAsync(db, actor.Id),
            _ => Task.FromResult(false)
        };
    }
}
