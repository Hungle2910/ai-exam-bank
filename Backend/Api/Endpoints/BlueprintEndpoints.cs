using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Persistence;
using AiExamBank.Persistence.Exams;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api.Endpoints;

internal static class BlueprintEndpoints
{
    public static void MapBlueprintEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/blueprints").RequireAuthorization();
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/{id:guid}/archive", ArchiveAsync);
        group.MapPost("", (BlueprintRequest request, HttpContext context, UserManager<AppUser> users,
            IAntiforgery csrf, BlueprintStore store, CancellationToken ct) => SaveAsync(null, request, context, users, csrf, store, ct));
        group.MapPut("/{id:guid}", (Guid id, BlueprintRequest request, HttpContext context, UserManager<AppUser> users,
            IAntiforgery csrf, BlueprintStore store, CancellationToken ct) => SaveAsync(id, request, context, users, csrf, store, ct));
    }

    private static async Task<IResult> ListAsync(HttpContext context, UserManager<AppUser> users,
        BlueprintStore store, CancellationToken ct, int page = 1, int pageSize = 20, bool archived = false)
    {
        var actor = await users.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (actor.SchoolId is not Guid school) return Results.Forbid();
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) return Results.BadRequest();
        var query = store.List(actor.Id, school, archived).AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Include(x => x.Slots).ToListAsync(ct);
        return Results.Ok(new { items = items.Select(Response), total, page, pageSize });
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext context, UserManager<AppUser> users,
        BlueprintStore store, CancellationToken ct)
    {
        var actor = await users.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (actor.SchoolId is not Guid school) return Results.Forbid();
        var entity = await store.Accessible(actor.Id, school).AsNoTracking().Include(x => x.Slots)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? Results.NotFound() : Results.Ok(Response(entity));
    }

    private static async Task<IResult> SaveAsync(Guid? id, BlueprintRequest request, HttpContext context,
        UserManager<AppUser> users, IAntiforgery csrf, BlueprintStore store, CancellationToken ct)
    {
        if (!await csrf.IsRequestValidAsync(context)) return Results.BadRequest();
        var actor = await users.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (actor.SchoolId is not Guid school) return Results.Forbid();
        BlueprintEntity? existing = null;
        byte[]? version = null;
        if (id is not null)
        {
            existing = await store.Accessible(actor.Id, school).Include(x => x.Slots).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (existing is null) return Results.NotFound();
            if (existing.ArchivedAtUtc is not null) return Archived();
            try { version = Convert.FromBase64String(request.Version ?? ""); }
            catch (FormatException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["version"] = ["Version không hợp lệ."] }); }
            if (version.Length != 8) return Results.ValidationProblem(new Dictionary<string, string[]> { ["version"] = ["Version không hợp lệ."] });
            if (!version.SequenceEqual(existing.RowVersion)) return Conflict();
        }
        try
        {
            var result = await store.SaveAsync(actor.Id, school,
                new BlueprintMatrix(request.Name, request.SubjectId, request.DurationMinutes, request.TotalQuestions,
                    request.TotalPoints, request.Slots), existing, version, ct);
            if (result.Value is null) return Results.ValidationProblem(result.Errors.ToDictionary(x => x.Key, x => x.Value));
            return id is null ? Results.Created($"/api/v1/blueprints/{result.Value.Id}", Response(result.Value))
                : Results.Ok(Response(result.Value));
        }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
    }

    private static async Task<IResult> ArchiveAsync(Guid id, ArchiveRequest request, HttpContext context,
        UserManager<AppUser> users, IAntiforgery csrf, BlueprintStore store, CancellationToken ct)
    {
        if (!await csrf.IsRequestValidAsync(context)) return Results.BadRequest();
        var actor = await users.GetUserAsync(context.User);
        if (actor is null) return Results.Unauthorized();
        if (actor.SchoolId is not Guid school) return Results.Forbid();
        var entity = await store.Accessible(actor.Id, school).Include(x => x.Slots).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Results.NotFound();
        byte[] version;
        try { version = Convert.FromBase64String(request.Version ?? ""); }
        catch (FormatException) { return Results.BadRequest(); }
        if (version.Length != 8) return Results.BadRequest();
        if (!version.SequenceEqual(entity.RowVersion)) return Conflict();
        if (entity.ArchivedAtUtc is not null) return Results.Ok(Response(entity));
        try { await store.ArchiveAsync(entity, version, ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        return Results.Ok(Response(entity));
    }

    private static IResult Archived() => Results.Problem(statusCode: 409,
        title: "Ma trận đã được lưu trữ và chỉ có thể xem.",
        extensions: new Dictionary<string, object?> { ["code"] = "BLUEPRINT_ARCHIVED" });

    private static IResult Conflict() => Results.Problem(statusCode: 409,
        title: "Ma trận đã được thay đổi. Tải lại bản mới trước khi lưu.",
        extensions: new Dictionary<string, object?> { ["code"] = "BLUEPRINT_VERSION_CONFLICT" });

    private static object Response(BlueprintEntity b) => new
    {
        b.Id, b.Name, b.SubjectId, b.DurationMinutes, b.TotalQuestions, b.TotalPoints,
        updatedAt = b.UpdatedAtUtc, archivedAt = b.ArchivedAtUtc, version = Convert.ToBase64String(b.RowVersion),
        slots = b.Slots.OrderBy(s => s.Position).Select(s => new { s.TopicId, s.Difficulty, s.QuestionCount, s.PointsPerQuestion })
    };

    internal sealed record BlueprintRequest(string Name, Guid SubjectId, int DurationMinutes,
        int TotalQuestions, decimal TotalPoints, IReadOnlyList<MatrixSlot> Slots, string? Version);
    internal sealed record ArchiveRequest(string? Version);
}
