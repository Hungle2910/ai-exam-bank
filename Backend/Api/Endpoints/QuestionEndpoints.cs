using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using AiExamBank.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace AiExamBank.Api.Endpoints;

/// <summary>
/// Question bank API. The school always comes from the signed-in user, never from the request, and every
/// status the client sees is produced by the server: a write cannot set a status.
/// </summary>
internal static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();
        api.MapGet("/catalog", GetCatalogAsync);
        api.MapGet("/questions", SearchAsync);
        api.MapGet("/questions/{id:guid}", GetAsync);
        api.MapPost("/questions", CreateAsync);
        api.MapPut("/questions/{id:guid}", UpdateAsync);
    }

    private static async Task<IResult> GetCatalogAsync(
        HttpContext context, UserManager<AppUser> userManager, IQuestionCatalog catalog, CancellationToken ct)
    {
        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (actor.SchoolId is not Guid schoolId) return Results.Forbid();

        var subjects = await catalog.ListAsync(actor.Id, schoolId, ct);
        return Results.Ok(subjects.Select(subject => new
        {
            subject.Id,
            subject.Code,
            subject.Name,
            canWrite = subject.CanWrite,
            topics = subject.Topics.Select(topic => new { topic.Id, topic.Code, topic.Name })
        }));
    }

    private static async Task<IResult> SearchAsync(
        HttpContext context,
        UserManager<AppUser> userManager,
        IQuestionQueries queries,
        CancellationToken ct,
        Guid? subjectId = null,
        Guid? topicId = null,
        string? difficulty = null,
        string? status = null,
        string? q = null,
        int page = 1,
        int pageSize = QuestionSearchQuery.DefaultPageSize)
    {
        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (actor.SchoolId is not Guid schoolId) return Results.Forbid();

        var parsedDifficulty = ApiNames.ParseDifficulty(difficulty);
        var parsedStatus = ApiNames.ParseStatus(status);
        if ((difficulty is not null && parsedDifficulty is null) || (status is not null && parsedStatus is null))
            return Problem(context, StatusCodes.Status400BadRequest, "QUESTION_FILTER_INVALID", "The difficulty or status filter is not valid.");

        QuestionSearchQuery query;
        try
        {
            query = QuestionSearchQuery.Create(actor.Id, schoolId, subjectId, topicId, parsedDifficulty, parsedStatus, q, page, pageSize);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Problem(context, StatusCodes.Status400BadRequest, "QUESTION_FILTER_INVALID", "A paging or search value is out of range.");
        }

        var result = await queries.SearchAsync(query, ct);
        return Results.Ok(new
        {
            items = result.Items.Select(item => new
            {
                id = item.QuestionId,
                item.Revision,
                status = ApiNames.ToApi(item.Status),
                stem = item.Stem,
                subject = new { id = item.SubjectId, name = item.SubjectName },
                topic = new { id = item.TopicId, name = item.TopicName },
                difficulty = ApiNames.ToApi(item.Difficulty),
                ownerUserId = item.OwnerUserId,
                updatedAt = item.UpdatedAtUtc
            }),
            result.Page,
            result.PageSize,
            result.Total
        });
    }

    private static async Task<IResult> GetAsync(
        Guid id, HttpContext context, UserManager<AppUser> userManager, IQuestionQueries queries, CancellationToken ct)
    {
        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();

        var detail = await queries.GetAsync(actor.Id, id, ct);
        return detail is null
            ? Problem(context, StatusCodes.Status404NotFound, "QUESTION_NOT_FOUND", "The question was not found.")
            : Results.Ok(ToResponse(detail));
    }

    private static async Task<IResult> CreateAsync(
        QuestionRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AppUser> userManager,
        IQuestionWriter writer,
        IQuestionQueries queries,
        CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context)) return Results.BadRequest();
        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (actor.SchoolId is not Guid schoolId) return Results.Forbid();

        try
        {
            var command = CreateQuestionCommand.Create(actor.Id, schoolId, request.ToInput(), QuestionProvenance.Manual());
            var written = await writer.CreateDraftAsync(command, ct);
            var detail = await queries.GetAsync(actor.Id, written.Revision.QuestionId, ct);
            return Results.Created(
                $"/api/v1/questions/{written.Revision.QuestionId}",
                detail is null ? null : ToResponse(detail));
        }
        catch (Exception exception) when (TryMap(context, exception, out var problem))
        {
            return problem;
        }
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        QuestionRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AppUser> userManager,
        IQuestionWriter writer,
        IQuestionQueries queries,
        CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context)) return Results.BadRequest();
        var actor = await userManager.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (request.BaseRevision is not int baseRevision || baseRevision < 1)
            return Problem(context, StatusCodes.Status400BadRequest, "QUESTION_BASE_REVISION_REQUIRED", "The revision being edited is required.");

        try
        {
            var command = CreateRevisionCommand.Create(actor.Id, id, baseRevision, request.ToInput(), QuestionProvenance.Manual());
            await writer.CreateRevisionAsync(command, ct);
            var detail = await queries.GetAsync(actor.Id, id, ct);
            return detail is null
                ? Problem(context, StatusCodes.Status404NotFound, "QUESTION_NOT_FOUND", "The question was not found.")
                : Results.Ok(ToResponse(detail));
        }
        catch (Exception exception) when (TryMap(context, exception, out var problem))
        {
            return problem;
        }
    }

    /// <summary>Maps the module's business exceptions to Problem Details with a stable code and the trace ID.</summary>
    private static bool TryMap(HttpContext context, Exception exception, out IResult problem)
    {
        switch (exception)
        {
            case QuestionValidationException validation:
                problem = Results.Problem(
                    title: "The question is not valid.",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "QUESTION_INVALID",
                        ["traceId"] = context.TraceIdentifier,
                        ["errors"] = validation.Errors.Select(error => new { error.Code, error.Field, error.Message })
                    });
                return true;
            case QuestionAccessDeniedException:
                problem = Problem(context, StatusCodes.Status403Forbidden, "QUESTION_FORBIDDEN", "You may not do this to this question.");
                return true;
            case QuestionNotFoundException:
                problem = Problem(context, StatusCodes.Status404NotFound, "QUESTION_NOT_FOUND", "The question was not found.");
                return true;
            case QuestionConflictException conflict:
                problem = Problem(context, StatusCodes.Status409Conflict, "QUESTION_CONFLICT", conflict.Message);
                return true;
            default:
                problem = Results.StatusCode(StatusCodes.Status500InternalServerError);
                return false;
        }
    }

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(
            title: title,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = context.TraceIdentifier });

    private static object ToResponse(QuestionDetail detail) => new
    {
        id = detail.QuestionId,
        detail.Revision,
        status = ApiNames.ToApi(detail.Status),
        origin = ApiNames.ToApi(detail.Origin),
        stem = detail.Stem,
        options = detail.Options,
        correctOption = detail.CorrectOption.ToString(),
        subject = new { id = detail.SubjectId, name = detail.SubjectName },
        topic = new { id = detail.TopicId, name = detail.TopicName },
        difficulty = ApiNames.ToApi(detail.Difficulty),
        source = detail.SourceText,
        ownerUserId = detail.OwnerUserId,
        updatedAt = detail.UpdatedAtUtc,
        revisions = detail.Revisions.Select(revision => new
        {
            revision.Revision,
            status = ApiNames.ToApi(revision.Status),
            origin = ApiNames.ToApi(revision.Origin),
            createdByUserId = revision.CreatedByUserId,
            createdAt = revision.CreatedAtUtc
        })
    };
}

/// <summary>The body of POST and PUT. It carries content only; scope, status and origin are decided by the server.</summary>
internal sealed record QuestionRequest(
    string? Stem,
    string?[]? Options,
    string? CorrectOption,
    string? Difficulty,
    Guid SubjectId,
    Guid TopicId,
    string? Source,
    int? BaseRevision)
{
    public McqInput ToInput() => new(
        Stem,
        Options,
        CorrectOption,
        Difficulty,
        SubjectId,
        SubjectId != Guid.Empty && TopicId != Guid.Empty ? QuestionTopic.Create(TopicId, SubjectId) : null,
        Source);
}

/// <summary>The upper-case names the web client already uses for enums.</summary>
internal static class ApiNames
{
    public static string ToApi(QuestionStatus status) => status switch
    {
        QuestionStatus.Draft => "DRAFT",
        QuestionStatus.ReviewRequired => "REVIEW_REQUIRED",
        QuestionStatus.Approved => "APPROVED",
        QuestionStatus.Rejected => "REJECTED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToApi(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "EASY",
        Difficulty.Medium => "MEDIUM",
        Difficulty.Hard => "HARD",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    public static string ToApi(QuestionOrigin origin) => origin switch
    {
        QuestionOrigin.Manual => "MANUAL",
        QuestionOrigin.Import => "IMPORT",
        QuestionOrigin.AiDraft => "AI_DRAFT",
        QuestionOrigin.Seed => "SEED",
        _ => throw new ArgumentOutOfRangeException(nameof(origin))
    };

    public static QuestionStatus? ParseStatus(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "DRAFT" => QuestionStatus.Draft,
        "REVIEW_REQUIRED" => QuestionStatus.ReviewRequired,
        "APPROVED" => QuestionStatus.Approved,
        "REJECTED" => QuestionStatus.Rejected,
        _ => null
    };

    public static Difficulty? ParseDifficulty(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "EASY" => Difficulty.Easy,
        "MEDIUM" => Difficulty.Medium,
        "HARD" => Difficulty.Hard,
        _ => null
    };
}
