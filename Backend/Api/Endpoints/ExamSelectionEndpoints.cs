using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Persistence;
using AiExamBank.Persistence.Exams;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace AiExamBank.Api.Endpoints;

internal static class ExamSelectionEndpoints
{
    public static void MapExamSelection(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/exams").RequireAuthorization();
        group.MapPost("/generate", GenerateAsync);
        group.MapGet("/{id:guid}/selection", ResultAsync);
    }

    private static async Task<IResult> GenerateAsync(GenerateExamRequest request, HttpContext context,
        UserManager<AppUser> users, IAntiforgery csrf, ExamGenerationStore store, CancellationToken ct)
    {
        if (!await csrf.IsRequestValidAsync(context)) return Results.BadRequest();
        var actor = await users.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (actor.SchoolId is not Guid school) return Results.Forbid();
        try
        {
            var exam = await store.RequestAsync(actor.Id, school, request.BlueprintId, request.BlueprintVersion,
                request.Seed, context.Request.Headers["Idempotency-Key"].ToString(), ct);
            return Results.Accepted($"/api/v1/exams/{exam.Id}/selection", new { exam.Id, exam.Status });
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (DbUpdateConcurrencyException) { return Results.Problem(statusCode: 409, title: "Ma trận hoặc nội dung yêu cầu đã thay đổi."); }
        catch (ArgumentException) { return Results.Problem(statusCode: 400, title: "Cần Idempotency-Key hợp lệ (1–128 ký tự)."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { return Results.Problem(statusCode: 409, title: "Khóa yêu cầu đã được sử dụng. Hãy kiểm tra yêu cầu trước đó."); }
        catch (SqlException exception) when (exception.Number == 1205)
        { return Results.Problem(statusCode: 503, title: "Đang có yêu cầu đồng thời. Thử lại với cùng Idempotency-Key."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        { return Results.Problem(statusCode: 503, title: "Đang có yêu cầu đồng thời. Thử lại với cùng Idempotency-Key."); }
    }

    private static async Task<IResult> ResultAsync(Guid id, HttpContext context, UserManager<AppUser> users,
        ExamGenerationStore store, IJobTracker jobs, CancellationToken ct)
    {
        var actor = await users.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (actor.SchoolId is not Guid school) return Results.Forbid();
        var exam = await store.Accessible(actor.Id, school).AsNoTracking().AsSplitQuery().Include(x => x.Items).Include(x => x.Gaps)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (exam is null) return Results.NotFound();
        object? job = null;
        if (exam.JobId is Guid jobId)
        {
            try
            {
                var state = await jobs.GetJobStatusAsync(JobRef.Create(jobId), actor.Id.ToString("D"),
                    JobScope.Create(JobScopeKind.School, school.ToString("D")), ct);
                job = new { status = state.Status.ToString(), state.ErrorCode, state.SafeMessage };
            }
            catch (KeyNotFoundException) { return Results.Problem(statusCode: 503, title: "Không tìm thấy trạng thái job đã lưu. Cần kiểm tra kho Jobs."); }
        }
        return Results.Ok(new
        {
            exam.Id, exam.BlueprintId, exam.Status, exam.Seed, job,
            items = exam.Items.OrderBy(x => x.Position).Select(x => new { x.SlotIndex, x.QuestionId, x.Revision, x.Points }),
            gaps = exam.Gaps.OrderBy(x => x.SlotIndex).Select(x => new { x.SlotIndex, x.TopicId, x.Difficulty, x.MissingCount, x.MissingPoints })
        });
    }

    internal sealed record GenerateExamRequest(Guid BlueprintId, string BlueprintVersion, int Seed);
}
