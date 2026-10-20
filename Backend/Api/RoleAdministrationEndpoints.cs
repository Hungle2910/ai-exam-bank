using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api;

internal static class RoleAdministrationEndpoints
{
    public static void MapRoleAdministrationEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization();

        admin.MapGet("/schools/{schoolId:guid}/role-assignments", ListSchoolAssignmentsAsync);
        admin.MapPost("/users/{userId:guid}/role-assignments", GrantRoleAsync);
        admin.MapDelete("/role-assignments/{assignmentId:guid}", RevokeRoleAsync);
    }

    private static async Task<IResult> ListSchoolAssignmentsAsync(
        Guid schoolId,
        HttpContext context,
        UserManager<AppUser> userManager,
        AiExamBankDbContext db)
    {
        if (schoolId == Guid.Empty)
            return Results.BadRequest();

        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null)
            return Results.Unauthorized();

        var isMinistryAdmin = await AdminAuthorization.IsMinistryAdminAsync(db, actor.Id);
        if (!isMinistryAdmin &&
            !await AdminAuthorization.IsSchoolAdminAsync(db, actor, schoolId))
            return Results.Forbid();

        var query = db.RoleAssignments.AsNoTracking()
            .Where(assignment => assignment.SchoolId == schoolId);
        if (isMinistryAdmin)
            query = query.Where(assignment => assignment.Role == AppRole.SchoolAdmin);

        var assignments = await query
            .ToListAsync();

        return Results.Ok(assignments.Select(assignment => new
        {
            assignment.Id,
            assignment.UserId,
            role = assignment.Role.ToString(),
            assignment.SchoolId,
            assignment.DepartmentId
        }));
    }

    private static async Task<IResult> GrantRoleAsync(
        Guid userId,
        RoleAssignmentRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AppUser> userManager,
        AiExamBankDbContext db)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest();

        if (userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Role) ||
            !Enum.TryParse<AppRole>(request.Role, out var role) ||
            Enum.GetName(role) != request.Role)
        {
            return Results.BadRequest();
        }

        RoleAssignment validated;
        try
        {
            validated = RoleAssignment.Create(userId, role, request.SchoolId, request.DepartmentId);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest();
        }

        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null)
            return Results.Unauthorized();
        if (actor.Id == userId || role == AppRole.MinistryAdmin)
            return Results.Forbid();

        if (!await AdminAuthorization.CanManageRoleAsync(db, actor, role, validated.SchoolId))
            return Results.Forbid();

        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId);
        if (target is null)
            return Results.NotFound();
        if (target.SchoolId != validated.SchoolId)
            return Results.Forbid();

        if (validated.DepartmentId is Guid departmentId &&
            !await db.Departments.AsNoTracking().AnyAsync(department =>
                department.Id == departmentId && department.SchoolId == validated.SchoolId))
        {
            return Results.BadRequest();
        }

        var exists = await db.RoleAssignments.AsNoTracking().AnyAsync(assignment =>
            assignment.UserId == userId &&
            assignment.Role == role &&
            assignment.SchoolId == validated.SchoolId &&
            assignment.DepartmentId == validated.DepartmentId);
        if (exists)
            return Results.Conflict();

        var assignment = new RoleAssignmentEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Role = role,
            SchoolId = validated.SchoolId,
            DepartmentId = validated.DepartmentId
        };
        db.RoleAssignments.Add(assignment);
        db.RoleAssignmentAudits.Add(CreateAudit(actor.Id, assignment, "Granted"));

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Results.Conflict();
        }

        return Results.Json(new { assignment.Id }, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> RevokeRoleAsync(
        Guid assignmentId,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AppUser> userManager,
        AiExamBankDbContext db)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest();

        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null)
            return Results.Unauthorized();

        var isAdmin = await db.RoleAssignments.AsNoTracking().AnyAsync(assignment =>
            assignment.UserId == actor.Id &&
            (assignment.Role == AppRole.SchoolAdmin || assignment.Role == AppRole.MinistryAdmin));
        if (!isAdmin)
            return Results.Forbid();

        var assignmentToRemove = await db.RoleAssignments.FindAsync(assignmentId);
        if (assignmentToRemove is null)
            return Results.NotFound();
        if (assignmentToRemove.UserId == actor.Id || assignmentToRemove.Role == AppRole.MinistryAdmin)
            return Results.Forbid();

        if (!await AdminAuthorization.CanManageRoleAsync(
            db, actor, assignmentToRemove.Role, assignmentToRemove.SchoolId))
            return Results.Forbid();

        var target = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == assignmentToRemove.UserId);
        if (target?.SchoolId != assignmentToRemove.SchoolId)
            return Results.Forbid();

        db.RoleAssignments.Remove(assignmentToRemove);
        db.RoleAssignmentAudits.Add(CreateAudit(actor.Id, assignmentToRemove, "Revoked"));

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict();
        }

        return Results.NoContent();
    }

    private static RoleAssignmentAuditEntity CreateAudit(
        Guid actorUserId,
        RoleAssignmentEntity assignment,
        string action) => new()
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            TargetUserId = assignment.UserId,
            Action = action,
            Role = assignment.Role,
            SchoolId = assignment.SchoolId,
            DepartmentId = assignment.DepartmentId,
            OccurredAtUtc = DateTimeOffset.UtcNow
        };
}

internal sealed record RoleAssignmentRequest(string Role, Guid? SchoolId, Guid? DepartmentId);
