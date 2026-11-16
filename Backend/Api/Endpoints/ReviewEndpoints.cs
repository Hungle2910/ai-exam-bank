using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Review.Contracts;
using AiExamBank.Modules.Review.Domain;
using AiExamBank.Persistence;
using AiExamBank.Persistence.Questions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api.Endpoints;

internal static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Review");
        api.MapPost("/questions/{questionId:guid}/submit-review", SubmitAsync);
        api.MapGet("/reviews/pending", PendingAsync);
        api.MapPost("/reviews/{questionId:guid}/revisions/{revision:int}/decision", DecideAsync);
    }

    private static async Task<IResult> SubmitAsync(Guid questionId, SubmitReviewRequest request,
        HttpContext context, IAntiforgery antiforgery, UserManager<AppUser> users,
        AiExamBankDbContext db, CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context)) return Results.BadRequest();
        var actor = await users.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (request.Revision < 1 || request.ExpectedVersion < 1) return Problem(context, 400, "REVIEW_INVALID");

        var question = await db.Questions.SingleOrDefaultAsync(q => q.Id == questionId, ct);
        if (question is null || question.SchoolId != actor.SchoolId) return Problem(context, 404, "QUESTION_NOT_FOUND");
        var item = await db.QuestionRevisions.SingleOrDefaultAsync(r =>
            r.QuestionId == questionId && r.Revision == request.Revision, ct);
        if (item is null) return Problem(context, 404, "QUESTION_NOT_FOUND");
        if (question.LatestRevision != request.Revision) return Problem(context, 409, "REVIEW_CONFLICT");

        var result = ReviewWorkflow.Submit(Snapshot(question, item), actor.Id,
            request.ExpectedVersion, await AssignmentsAsync(db, actor.Id, question.SchoolId, ct));
        if (!result.Succeeded) return Failure(context, result.Failure);

        // ReviewVersion là khóa chống ghi đè: request dùng phiên bản cũ sẽ nhận 409.
        item.Status = QuestionStatus.PendingReview;
        item.ReviewVersion = result.Updated!.Version;
        item.SubmittedAtUtc = DateTimeOffset.UtcNow;
        // Chạm cả câu hỏi gốc để RowVersion chặn việc sửa câu đồng thời với gửi duyệt.
        question.UpdatedAtUtc = item.SubmittedAtUtc.Value;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem(context, 409, "REVIEW_CONFLICT"); }
        return Results.Ok(new ReviewTransitionResponse(questionId, item.Revision, "PENDING_REVIEW", item.ReviewVersion));
    }

    private static async Task<IResult> DecideAsync(Guid questionId, int revision, ReviewDecisionApiRequest request,
        HttpContext context, IAntiforgery antiforgery, UserManager<AppUser> users,
        AiExamBankDbContext db, CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context)) return Results.BadRequest();
        var actor = await users.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (revision < 1 || request.GetValidationError() is not null) return Problem(context, 400, "REVIEW_INVALID");

        var question = await db.Questions.SingleOrDefaultAsync(q => q.Id == questionId, ct);
        if (question is null || question.SchoolId != actor.SchoolId) return Problem(context, 404, "QUESTION_NOT_FOUND");
        var item = await db.QuestionRevisions.SingleOrDefaultAsync(r =>
            r.QuestionId == questionId && r.Revision == revision, ct);
        if (item is null) return Problem(context, 404, "QUESTION_NOT_FOUND");
        if (question.LatestRevision != revision) return Problem(context, 409, "REVIEW_CONFLICT");

        var decision = new ReviewDecisionRequest(QuestionRevisionRef.Create(questionId, revision),
            actor.Id, request.Decision, request.Reason?.Trim() ?? "", request.ExpectedVersion);
        var result = ReviewWorkflow.Decide(Snapshot(question, item), decision,
            await AssignmentsAsync(db, actor.Id, question.SchoolId, ct));
        if (!result.Succeeded) return Failure(context, result.Failure);

        item.Status = request.Decision == ReviewDecision.Approve ? QuestionStatus.Approved : QuestionStatus.Rejected;
        item.ReviewVersion = result.Updated!.Version;
        db.ReviewDecisions.Add(new ReviewDecisionEntity
        {
            Id = Guid.NewGuid(), QuestionId = questionId, Revision = revision, ReviewerUserId = actor.Id,
            Decision = request.Decision.ToString(), Reason = request.Decision == ReviewDecision.Reject ? request.Reason!.Trim() : null,
            DecidedAtUtc = DateTimeOffset.UtcNow
        });
        try
        {
            // SaveChanges lưu trạng thái và lịch sử trong một giao dịch.
            // Nếu người duyệt khác ghi trước thì cả hai thay đổi của request này đều bị hủy.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Problem(context, 409, "REVIEW_CONFLICT"); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { return Problem(context, 409, "REVIEW_CONFLICT"); }
        return Results.Ok(new ReviewTransitionResponse(questionId, revision, ApiNames.ToApi(item.Status), item.ReviewVersion));
    }

    private static async Task<IResult> PendingAsync(HttpContext context, UserManager<AppUser> users,
        AiExamBankDbContext db, CancellationToken ct, int page = 1, int pageSize = 20)
    {
        var actor = await users.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (actor.SchoolId is not Guid schoolId) return Results.Forbid();
        if (page < 1 || pageSize is < 1 or > 100) return Problem(context, 400, "REVIEW_FILTER_INVALID");

        var departments = await db.RoleAssignments.AsNoTracking()
            .Where(a => a.UserId == actor.Id && a.SchoolId == schoolId &&
                a.Role == AppRole.DepartmentHead && a.DepartmentId != null)
            .Select(a => a.DepartmentId!.Value).Distinct().ToArrayAsync(ct);
        if (departments.Length == 0) return Results.Forbid();

        // Lọc quyền và tự duyệt ngay trong SQL trước khi phân trang; trình duyệt không thấy câu ngoài phạm vi.
        var rows = await (
            from q in db.Questions.AsNoTracking()
            join r in db.QuestionRevisions.AsNoTracking()
                on new { q.Id, Revision = q.LatestRevision } equals new { Id = r.QuestionId, r.Revision }
            join subject in db.Subjects.AsNoTracking() on q.SubjectId equals subject.Id
            join topic in db.Topics.AsNoTracking() on r.TopicId equals topic.Id
            join author in db.Users.AsNoTracking() on r.CreatedByUserId equals author.Id
            where q.SchoolId == schoolId && departments.Contains(q.DepartmentId) &&
                q.OwnerUserId != actor.Id && r.CreatedByUserId != actor.Id &&
                r.Status == QuestionStatus.PendingReview
            orderby r.SubmittedAtUtc, q.Id
            select new { Question = q, Revision = r, Subject = subject.Name,
                Topic = topic.Name, Author = author.UserName })
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var keys = rows.Select(x => x.Question.Id).ToArray();
        var options = await db.QuestionOptions.AsNoTracking()
            .Where(o => keys.Contains(o.QuestionId))
            .OrderBy(o => o.Label).ToListAsync(ct);
        return Results.Ok(rows.Select(row => new PendingReviewResponse(
            row.Question.Id, row.Revision.Revision, row.Revision.ReviewVersion,
            row.Revision.Stem,
            options.Where(o => o.QuestionId == row.Question.Id && o.Revision == row.Revision.Revision)
                .Select(o => o.Text).ToArray(),
            row.Revision.CorrectOption[0] - 'A', row.Subject, row.Topic,
            ApiNames.ToApi(row.Revision.Difficulty), row.Author ?? "",
            row.Revision.SubmittedAtUtc ?? row.Revision.CreatedAtUtc,
            "REVIEW_REQUIRED", ApiNames.ToApi(row.Revision.Origin))).ToArray());
    }

    private static ReviewRevisionSnapshot Snapshot(QuestionEntity q, QuestionRevisionEntity r) => new(
        QuestionRevisionRef.Create(q.Id, r.Revision), q.SchoolId, q.DepartmentId,
        q.OwnerUserId, r.CreatedByUserId, r.Status switch
        {
            QuestionStatus.Draft => RevisionReviewState.Draft,
            QuestionStatus.PendingReview => RevisionReviewState.ReviewRequired,
            QuestionStatus.Approved => RevisionReviewState.Approved,
            QuestionStatus.Rejected => RevisionReviewState.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(r.Status))
        }, r.ReviewVersion);

    private static async Task<IReadOnlyCollection<RoleAssignment>> AssignmentsAsync(
        AiExamBankDbContext db, Guid actorId, Guid schoolId, CancellationToken ct) =>
        (await db.RoleAssignments.AsNoTracking()
            .Where(a => a.UserId == actorId && a.SchoolId == schoolId).ToListAsync(ct))
        .Select(a => RoleAssignment.Create(a.UserId, a.Role, a.SchoolId, a.DepartmentId)).ToArray();

    private static IResult Failure(HttpContext context, ReviewTransitionFailure? failure) => failure switch
    {
        ReviewTransitionFailure.InvalidRequest => Problem(context, 400, "REVIEW_INVALID"),
        ReviewTransitionFailure.Forbidden => Problem(context, 403, "REVIEW_FORBIDDEN"),
        _ => Problem(context, 409, "REVIEW_CONFLICT")
    };

    private static IResult Problem(HttpContext context, int status, string code) => Results.Problem(
        statusCode: status, title: code, extensions: new Dictionary<string, object?>
        { ["code"] = code, ["traceId"] = context.TraceIdentifier });
}

internal sealed record SubmitReviewRequest(int Revision, long ExpectedVersion);
internal sealed record ReviewTransitionResponse(Guid QuestionId, int Revision, string Status, long Version);
internal sealed record PendingReviewResponse(Guid Id, int Revision, long Version, string Content,
    IReadOnlyList<string> Options, int CorrectIndex, string Subject, string Topic, string Difficulty,
    string Author, DateTimeOffset SubmittedAt, string Status, string Source);
