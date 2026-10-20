using AiExamBank.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api;

internal static class SchoolAdministrationEndpoints
{
    public static void MapSchoolAdministrationEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization();
        admin.MapGet("/schools", ListSchoolsAsync);
        admin.MapPost("/schools", CreateSchoolAsync);
        admin.MapGet("/schools/{schoolId:guid}/departments", ListDepartmentsAsync);
        admin.MapPost("/schools/{schoolId:guid}/departments", CreateDepartmentAsync);
    }

    private static async Task<IResult> ListSchoolsAsync(
        HttpContext context,
        UserManager<AppUser> userManager,
        AiExamBankDbContext db)
    {
        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null)
            return Results.Unauthorized();

        if (await AdminAuthorization.IsMinistryAdminAsync(db, actor.Id))
        {
            var schools = await db.Schools.AsNoTracking()
                .OrderBy(school => school.Name)
                .Select(school => new { school.Id, school.Name })
                .ToListAsync();
            return Results.Ok(schools);
        }

        if (actor.SchoolId is Guid schoolId &&
            await AdminAuthorization.IsSchoolAdminAsync(db, actor, schoolId))
        {
            var school = await db.Schools.AsNoTracking()
                .Where(item => item.Id == schoolId)
                .Select(item => new { item.Id, item.Name })
                .ToListAsync();
            return Results.Ok(school);
        }

        return Results.Forbid();
    }

    private static async Task<IResult> CreateSchoolAsync(
        SchoolRequest request,
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
        if (!await AdminAuthorization.IsMinistryAdminAsync(db, actor.Id))
            return Results.Forbid();

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Results.BadRequest();

        var school = new SchoolEntity { Id = Guid.NewGuid(), Name = name };
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        return Results.Json(new { school.Id, school.Name }, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListDepartmentsAsync(
        Guid schoolId,
        HttpContext context,
        UserManager<AppUser> userManager,
        AiExamBankDbContext db)
    {
        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null)
            return Results.Unauthorized();
        if (!await CanManageSchoolAsync(db, actor, schoolId))
            return Results.Forbid();

        var departments = await db.Departments.AsNoTracking()
            .Where(department => department.SchoolId == schoolId)
            .OrderBy(department => department.Name)
            .Select(department => new { department.Id, department.SchoolId, department.Name })
            .ToListAsync();
        return Results.Ok(departments);
    }

    private static async Task<IResult> CreateDepartmentAsync(
        Guid schoolId,
        SchoolRequest request,
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
        if (!await CanManageSchoolAsync(db, actor, schoolId))
            return Results.Forbid();

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Results.BadRequest();
        if (!await db.Schools.AsNoTracking().AnyAsync(school => school.Id == schoolId))
            return Results.NotFound();

        var department = new DepartmentEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            Name = name
        };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        return Results.Json(
            new { department.Id, department.SchoolId, department.Name },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<bool> CanManageSchoolAsync(
        AiExamBankDbContext db, AppUser actor, Guid schoolId) =>
        schoolId != Guid.Empty &&
        (await AdminAuthorization.IsMinistryAdminAsync(db, actor.Id) ||
         await AdminAuthorization.IsSchoolAdminAsync(db, actor, schoolId));
}

internal sealed record SchoolRequest(string Name);
