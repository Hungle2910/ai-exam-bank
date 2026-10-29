using System.Data.Common;
using AiExamBank.Persistence;
using AiExamBank.Persistence.Questions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api.Endpoints;

/// <summary>
/// Assigns each subject a school offers to the one department that owns it. Without this mapping a school has
/// no subjects to write questions for. A subject can move to another department only while it has no questions,
/// because a question's scope is tied to the department that owned the subject when it was written.
/// </summary>
internal static class SchoolSubjectEndpoints
{
    public static void MapSchoolSubjectEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/schools/{schoolId:guid}/subjects").RequireAuthorization();
        admin.MapGet("", ListAsync);
        admin.MapPut("/{subjectId:guid}", AssignAsync);
    }

    private static async Task<IResult> ListAsync(
        Guid schoolId, HttpContext context, UserManager<AppUser> userManager, AiExamBankDbContext db, CancellationToken ct)
    {
        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (!await CanManageAsync(db, actor, schoolId)) return Results.Forbid();

        var mappings = await db.SchoolSubjects.AsNoTracking()
            .Where(item => item.SchoolId == schoolId)
            .Join(db.Subjects, item => item.SubjectId, subject => subject.Id,
                (item, subject) => new { subjectId = subject.Id, subject.Code, subject.Name, item.DepartmentId })
            .OrderBy(item => item.Name)
            .ToListAsync(ct);
        return Results.Ok(mappings);
    }

    private static async Task<IResult> AssignAsync(
        Guid schoolId,
        Guid subjectId,
        SchoolSubjectRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AppUser> userManager,
        AiExamBankDbContext db,
        CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context)) return Results.BadRequest();
        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (!await CanManageAsync(db, actor, schoolId)) return Results.Forbid();

        if (request.DepartmentId == Guid.Empty) return Results.BadRequest();
        if (!await db.Subjects.AnyAsync(item => item.Id == subjectId, ct)) return Results.NotFound();
        if (!await db.Departments.AnyAsync(item => item.Id == request.DepartmentId && item.SchoolId == schoolId, ct))
            return Results.BadRequest();

        var current = await CurrentDepartmentAsync(db, schoolId, subjectId, ct);
        if (current == request.DepartmentId) return Results.Ok(Mapping(subjectId, request.DepartmentId));
        if (current is not null) return await ReassignAsync(context, db, schoolId, subjectId, request.DepartmentId, ct);

        db.SchoolSubjects.Add(new SchoolSubjectEntity
        {
            SchoolId = schoolId,
            SubjectId = subjectId,
            DepartmentId = request.DepartmentId
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent or retried request assigned the subject first; answer from what is stored now.
            db.ChangeTracker.Clear();
            var stored = await CurrentDepartmentAsync(db, schoolId, subjectId, ct);
            if (stored == request.DepartmentId) return Results.Ok(Mapping(subjectId, request.DepartmentId));
            if (stored is null) throw;
            return Conflict(context, "SCHOOL_SUBJECT_ALREADY_ASSIGNED", "The subject was assigned to another department first.");
        }

        return Results.Json(Mapping(subjectId, request.DepartmentId), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ReassignAsync(
        HttpContext context, AiExamBankDbContext db, Guid schoolId, Guid subjectId, Guid departmentId, CancellationToken ct)
    {
        if (await db.Questions.AnyAsync(item => item.SchoolId == schoolId && item.SubjectId == subjectId, ct))
            return HasQuestions(context);

        try
        {
            // The department is part of a key that questions reference, so it is changed in the database
            // directly; a question written in the meantime makes the foreign key reject the change.
            await db.SchoolSubjects
                .Where(item => item.SchoolId == schoolId && item.SubjectId == subjectId)
                .ExecuteUpdateAsync(set => set.SetProperty(item => item.DepartmentId, departmentId), ct);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            return HasQuestions(context);
        }

        return Results.Ok(Mapping(subjectId, departmentId));
    }

    private static Task<Guid?> CurrentDepartmentAsync(AiExamBankDbContext db, Guid schoolId, Guid subjectId, CancellationToken ct) =>
        db.SchoolSubjects.AsNoTracking()
            .Where(item => item.SchoolId == schoolId && item.SubjectId == subjectId)
            .Select(item => (Guid?)item.DepartmentId)
            .SingleOrDefaultAsync(ct);

    private static object Mapping(Guid subjectId, Guid departmentId) => new { subjectId, departmentId };

    private static IResult HasQuestions(HttpContext context) =>
        Conflict(context, "SCHOOL_SUBJECT_HAS_QUESTIONS", "The subject already has questions in its current department.");

    private static IResult Conflict(HttpContext context, string code, string title) =>
        Results.Problem(
            title: title,
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = context.TraceIdentifier });

    private static async Task<bool> CanManageAsync(AiExamBankDbContext db, AppUser actor, Guid schoolId) =>
        await AdminAuthorization.IsMinistryAdminAsync(db, actor.Id) ||
        await AdminAuthorization.IsSchoolAdminAsync(db, actor, schoolId);
}

internal sealed record SchoolSubjectRequest(Guid DepartmentId);
