using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api;

internal static class RoleAuditEndpoints
{
    public static void MapRoleAuditEndpoints(this WebApplication app) =>
        app.MapGet("/api/v1/audit/role-assignments", ListAsync).RequireAuthorization();

    private static async Task<IResult> ListAsync(
        HttpContext context,
        UserManager<AppUser> users,
        AiExamBankDbContext db,
        CancellationToken ct,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? action = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int page = 1,
        int pageSize = 50)
    {
        if (page is < 1 or > 100_000 || pageSize is < 1 or > 100 ||
            (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc) ||
            (action is not null && action is not ("ROLE_GRANTED" or "ROLE_REVOKED")))
            return Results.BadRequest();

        var actor = await users.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();

        var ministryAdmin = await AdminAuthorization.IsMinistryAdminAsync(db, actor.Id);
        var schoolAdmin = actor.SchoolId is Guid schoolId &&
            await AdminAuthorization.IsSchoolAdminAsync(db, actor, schoolId);
        if (!ministryAdmin && !schoolAdmin) return Results.Forbid();

        var query = db.RoleAssignmentAudits.AsNoTracking().AsQueryable();
        if (ministryAdmin)
        {
            // Ministry administrators can manage school administrators, not teacher roles.
            query = query.Where(entry => entry.Role == AppRole.SchoolAdmin);
        }
        else
        {
            query = query.Where(entry => entry.SchoolId == actor.SchoolId &&
                (entry.Role == AppRole.Teacher || entry.Role == AppRole.DepartmentHead));
        }

        if (actorUserId is Guid requestedActor)
            query = query.Where(entry => entry.ActorUserId == requestedActor);
        if (targetUserId is Guid requestedTarget)
            query = query.Where(entry => entry.TargetUserId == requestedTarget);
        if (action is not null)
        {
            var storedAction = action == "ROLE_GRANTED" ? "Granted" : "Revoked";
            query = query.Where(entry => entry.Action == storedAction);
        }
        if (fromUtc is DateTimeOffset start)
            query = query.Where(entry => entry.OccurredAtUtc >= start);
        if (toUtc is DateTimeOffset end)
            query = query.Where(entry => entry.OccurredAtUtc <= end);

        var rows = await (
            from entry in query
            join actorUser in db.Users.AsNoTracking() on entry.ActorUserId equals actorUser.Id
            join target in db.Users.AsNoTracking() on entry.TargetUserId equals target.Id
            join school in db.Schools.AsNoTracking() on entry.SchoolId equals (Guid?)school.Id
            orderby entry.OccurredAtUtc descending, entry.Id descending
            select new { entry.Id, entry.OccurredAtUtc, entry.Action, entry.Role,
                Actor = actorUser.UserName, Target = target.UserName, School = school.Name })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Results.Ok(rows.Select(row => new
        {
            row.Id,
            OccurredAt = row.OccurredAtUtc,
            Actor = row.Actor ?? "",
            Action = row.Action == "Granted" ? "ROLE_GRANTED" : "ROLE_REVOKED",
            Target = row.Target ?? "",
            Detail = $"{row.Action} {row.Role}",
            SchoolName = row.School
        }));
    }
}
