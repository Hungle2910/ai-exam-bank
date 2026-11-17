using System.Data;
using System.Text.Json;
using AiExamBank.Modules.Exams.Application;
using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Exams;

public sealed class ExamGenerationStore(AiExamBankDbContext db, BlueprintStore blueprints, ExamSelectionService selection)
{
    public IQueryable<ExamGenerationEntity> Accessible(Guid actor, Guid school) => db.Set<ExamGenerationEntity>()
        .Where(e => e.OwnerUserId == actor && e.SchoolId == school && db.RoleAssignments.Any(r =>
            r.UserId == actor && r.SchoolId == school && r.DepartmentId == e.DepartmentId && r.Role == AppRole.Teacher));

    public async Task<ExamGenerationEntity> RequestAsync(Guid actor, Guid school, Guid blueprintId,
        string version, int seed, string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128) throw new ArgumentException("Idempotency key must have 1–128 characters.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Seek the unique (OwnerUserId, IdempotencyKey) index only. Joining roles here widens the serializable
        // range lock, so two teachers generating at once could deadlock on insert.
        var old = await db.Set<ExamGenerationEntity>().SingleOrDefaultAsync(e => e.OwnerUserId == actor && e.IdempotencyKey == key, ct);
        if (old is not null)
        {
            if (old.SchoolId != school || !await Accessible(actor, school).AnyAsync(e => e.Id == old.Id, ct) ||
                old.BlueprintId != blueprintId || old.BlueprintVersion != version || old.Seed != seed)
                throw new DbUpdateConcurrencyException("The idempotency key belongs to a different request.");
            await tx.CommitAsync(ct);
            return old;
        }
        var blueprint = await blueprints.Accessible(actor, school).Include(x => x.Slots).SingleOrDefaultAsync(x => x.Id == blueprintId, ct)
            ?? throw new KeyNotFoundException();
        if (blueprint.ArchivedAtUtc is not null || Convert.ToBase64String(blueprint.RowVersion) != version)
            throw new DbUpdateConcurrencyException("The blueprint changed or was archived.");
        var matrix = new BlueprintMatrix(blueprint.Name, blueprint.SubjectId, blueprint.DurationMinutes, blueprint.TotalQuestions,
            blueprint.TotalPoints, blueprint.Slots.OrderBy(x => x.Position).Select(x => new MatrixSlot(x.TopicId, x.Difficulty, x.QuestionCount, x.PointsPerQuestion)).ToArray());
        var exam = new ExamGenerationEntity
        {
            Id = Guid.NewGuid(), BlueprintId = blueprint.Id, BlueprintVersion = version, MatrixJson = JsonSerializer.Serialize(matrix),
            OwnerUserId = actor, SchoolId = school, DepartmentId = blueprint.DepartmentId, Seed = seed,
            IdempotencyKey = key, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Add(exam);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return exam;
    }

    public async Task<bool> ExecuteAsync(Guid examId, Guid actor, Guid school, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var exam = await Accessible(actor, school).SingleOrDefaultAsync(x => x.Id == examId, ct);
        if (exam is null) return false;
        // A rerun after a saved result is a completed job, never a second selection. The unique (ExamId, QuestionId)
        // index would reject duplicate items, so check the stored rows as well as the status.
        var hasItems = await db.Set<ExamItemEntity>().AnyAsync(x => x.ExamId == examId, ct);
        var hasGaps = await db.Set<ExamGapEntity>().AnyAsync(x => x.ExamId == examId, ct);
        if (exam.Status != ExamGenerationStatus.Queued || hasItems || hasGaps)
        {
            if (exam.Status == ExamGenerationStatus.Queued)
            {
                // Rows exist but the status was never set: repair the status from the stored result.
                exam.Status = hasGaps ? ExamGenerationStatus.Partial : ExamGenerationStatus.NeedsReview;
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return true;
        }
        var blueprint = await blueprints.Accessible(actor, school).SingleOrDefaultAsync(x => x.Id == exam.BlueprintId, ct);
        if (blueprint is null || blueprint.ArchivedAtUtc is not null || Convert.ToBase64String(blueprint.RowVersion) != exam.BlueprintVersion) return false;
        var matrix = JsonSerializer.Deserialize<BlueprintMatrix>(exam.MatrixJson) ?? throw new InvalidOperationException("Missing stored matrix.");
        var result = await selection.SelectAsync(matrix, school, exam.DepartmentId, exam.Seed, ct);
        exam.Status = result.Status;
        exam.Items = result.Items.Select((item, index) => new ExamItemEntity
        {
            ExamId = exam.Id, Position = index, SlotIndex = item.SlotIndex, QuestionId = item.Question.Revision.QuestionId,
            Revision = item.Question.Revision.Revision, Points = item.Points, ContentJson = JsonSerializer.Serialize(item.Question)
        }).ToList();
        exam.Gaps = result.Gaps.Select(gap => new ExamGapEntity
        {
            ExamId = exam.Id, SlotIndex = gap.SlotIndex, TopicId = gap.TopicId, Difficulty = gap.Difficulty,
            MissingCount = gap.MissingCount, MissingPoints = gap.MissingPoints
        }).ToList();
        db.AddRange(exam.Items); db.AddRange(exam.Gaps);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
