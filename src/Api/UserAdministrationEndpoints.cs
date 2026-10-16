using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api;

internal static class UserAdministrationEndpoints
{
    public static void MapUserAdministrationEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization();
        admin.MapGet("/schools/{schoolId:guid}/users", ListUsersAsync);
        admin.MapPost("/schools/{schoolId:guid}/users", CreateUserAsync);
    }

    private static async Task<IResult> ListUsersAsync(
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

        var usersQuery = db.Users.AsNoTracking()
            .Where(user => user.SchoolId == schoolId);
        if (isMinistryAdmin)
            usersQuery = usersQuery.Where(user => db.RoleAssignments.Any(assignment =>
                assignment.UserId == user.Id && assignment.Role == AppRole.SchoolAdmin));

        var users = await usersQuery
            .OrderBy(user => user.UserName)
            .Select(user => new { user.Id, user.UserName, user.SchoolId })
            .ToListAsync();

        var userIds = users.Select(user => user.Id).ToArray();
        var assignments = await db.RoleAssignments.AsNoTracking()
            .Where(assignment => userIds.Contains(assignment.UserId) &&
                (!isMinistryAdmin || assignment.Role == AppRole.SchoolAdmin))
            .ToListAsync();

        return Results.Ok(users.Select(item => new
        {
            item.Id,
            item.UserName,
            item.SchoolId,
            roles = assignments.Where(assignment => assignment.UserId == item.Id)
                .Select(assignment => new
                {
                    role = assignment.Role.ToString(),
                    assignment.DepartmentId
                })
        }));
    }

    private static async Task<IResult> CreateUserAsync(
        Guid schoolId,
        CreateUserRequest request,
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

        if (schoolId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Role) ||
            !Enum.TryParse<AppRole>(request.Role, out var role) ||
            Enum.GetName(role) != request.Role ||
            role == AppRole.MinistryAdmin)
        {
            return Results.BadRequest();
        }

        if (!await AdminAuthorization.CanManageRoleAsync(db, actor, role, schoolId))
            return Results.Forbid();

        var userName = request.UserName?.Trim();
        if (string.IsNullOrWhiteSpace(userName) || userName.Length is < 3 or > 256 ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 128)
        {
            return Results.BadRequest();
        }

        var user = new AppUser { Id = Guid.NewGuid(), UserName = userName, SchoolId = schoolId };
        RoleAssignment validated;
        try
        {
            validated = RoleAssignment.Create(user.Id, role, schoolId, request.DepartmentId);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest();
        }

        if (!await db.Schools.AsNoTracking().AnyAsync(school => school.Id == schoolId))
            return Results.NotFound();
        if (validated.DepartmentId is Guid departmentId &&
            !await db.Departments.AsNoTracking().AnyAsync(department =>
                department.Id == departmentId && department.SchoolId == schoolId))
        {
            return Results.BadRequest();
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                if (result.Errors.Any(error => error.Code == "DuplicateUserName"))
                    return Results.Conflict();

                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["account"] = result.Errors.Select(error => error.Code).ToArray()
                });
            }

            var assignment = new RoleAssignmentEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Role = role,
                SchoolId = schoolId,
                DepartmentId = validated.DepartmentId
            };
            db.RoleAssignments.Add(assignment);
            db.RoleAssignmentAudits.Add(new RoleAssignmentAuditEntity
            {
                Id = Guid.NewGuid(),
                ActorUserId = actor.Id,
                TargetUserId = user.Id,
                Action = "Granted",
                Role = role,
                SchoolId = schoolId,
                DepartmentId = validated.DepartmentId,
                OccurredAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Results.Json(new { user.Id, user.UserName, user.SchoolId, assignmentId = assignment.Id },
                statusCode: StatusCodes.Status201Created);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Results.Conflict();
        }
    }
}

internal sealed record CreateUserRequest(
    string UserName,
    string Password,
    string Role,
    Guid? DepartmentId);
