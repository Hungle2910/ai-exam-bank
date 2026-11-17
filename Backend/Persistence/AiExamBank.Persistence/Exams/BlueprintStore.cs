using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Exams;

public sealed class BlueprintStore(AiExamBankDbContext db, IQuestionCatalog catalog)
{
    public IQueryable<BlueprintEntity> List(Guid actor, Guid school, bool archived = false) =>
        Accessible(actor, school).Where(b => (b.ArchivedAtUtc != null) == archived);

    public async Task ArchiveAsync(BlueprintEntity entity, byte[] expectedVersion, CancellationToken ct)
    {
        db.Entry(entity).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        entity.ArchivedAtUtc = DateTimeOffset.UtcNow;
        entity.UpdatedAtUtc = entity.ArchivedAtUtc.Value;
        await db.SaveChangesAsync(ct);
    }

    public IQueryable<BlueprintEntity> Accessible(Guid actor, Guid school) =>
        db.Set<BlueprintEntity>().Where(b => b.OwnerUserId == actor && b.SchoolId == school &&
            db.RoleAssignments.Any(r => r.UserId == actor && r.SchoolId == school &&
                r.DepartmentId == b.DepartmentId && r.Role == AppRole.Teacher));

    public async Task<(BlueprintEntity? Value, IReadOnlyDictionary<string, string[]> Errors)> SaveAsync(
        Guid actor, Guid school, BlueprintMatrix matrix, BlueprintEntity? existing, byte[]? expectedVersion, CancellationToken ct)
    {
        if (existing?.ArchivedAtUtc is not null)
            throw new InvalidOperationException("Archived blueprints are read-only.");
        if (!await db.RoleAssignments.AnyAsync(r => r.UserId == actor && r.SchoolId == school &&
                r.Role == AppRole.Teacher && r.DepartmentId != null, ct))
            throw new UnauthorizedAccessException();
        var errors = new Dictionary<string, string[]>(matrix.Validate());
        var subjects = await catalog.ListAsync(actor, school, ct);
        var subject = subjects.FirstOrDefault(s => s.Id == matrix.SubjectId && s.CanWrite);
        if (subject is null) errors["subjectId"] = ["Môn học không thuộc phạm vi được phép soạn."];
        else if (matrix.Slots is not null)
            for (var i = 0; i < matrix.Slots.Count; i++)
                if (matrix.Slots[i] is { } slot && !subject.Topics.Any(t => t.Id == slot.TopicId))
                    errors[$"slots[{i}].topicId"] = ["Chủ đề không thuộc môn học đã chọn."];
        if (existing is not null && matrix.SubjectId != existing.SubjectId)
            errors["subjectId"] = ["Không thể đổi môn học của ma trận đã lưu."];
        if (errors.Count > 0) return (null, errors);
        var entity = existing ?? new BlueprintEntity
        {
            Id = Guid.NewGuid(), OwnerUserId = actor, SchoolId = school,
            SubjectId = subject!.Id, DepartmentId = subject.DepartmentId
        };
        if (existing is null) db.Add(entity);
        else
        {
            db.Entry(entity).Property(x => x.RowVersion).OriginalValue = expectedVersion!;
            db.RemoveRange(entity.Slots);
        }
        entity.Name = matrix.Name.Trim();
        entity.DurationMinutes = matrix.DurationMinutes;
        entity.TotalQuestions = matrix.TotalQuestions;
        entity.TotalPoints = matrix.TotalPoints;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        entity.Slots = matrix.Slots!.Select((s, i) => new BlueprintSlotEntity
        {
            Id = Guid.NewGuid(), BlueprintId = entity.Id, TopicId = s.TopicId, Position = i,
            Difficulty = s.Difficulty, QuestionCount = s.QuestionCount, PointsPerQuestion = s.PointsPerQuestion
        }).ToList();
        db.AddRange(entity.Slots);
        await db.SaveChangesAsync(ct);
        return (entity, errors);
    }
}
