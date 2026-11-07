using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using AiExamBank.Modules.Exams.Application;
using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Exams;

/// <summary>Trusted worker persistence boundary. Use a fresh scoped context and a writer sharing that context.</summary>
public sealed class ExamDraftStore(AiExamBankDbContext db, IQuestionWriter writer)
{
    public async Task<IReadOnlyList<ExamDraftLinkEntity>> SaveAsync(Guid actor, Guid school, Guid examId,
        Guid jobId, IReadOnlyList<DraftSlotResult> results, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (jobId == Guid.Empty || results.Count is < 1 or > 200 ||
            results.Select(x => x.SlotIndex).Distinct().Count() != results.Count)
            throw new ArgumentException("A job and distinct slot results are required.");
        var ordered = results.OrderBy(x => x.SlotIndex).ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(ordered)));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var exam = await db.Set<ExamGenerationEntity>().AsNoTracking().Include(x => x.Gaps)
                .SingleOrDefaultAsync(x => x.Id == examId && x.OwnerUserId == actor && x.SchoolId == school &&
                    db.RoleAssignments.Any(r => r.UserId == actor && r.SchoolId == school &&
                        r.DepartmentId == x.DepartmentId && r.Role == AppRole.Teacher), ct)
                ?? throw new KeyNotFoundException();
            var previous = await db.Set<ExamDraftBatchEntity>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.ExamId == examId && x.JobId == jobId, ct);
            if (previous is not null)
            {
                if (previous.Fingerprint != fingerprint) throw new DbUpdateConcurrencyException("Job output changed.");
                var existing = await Links(examId, jobId).ToArrayAsync(ct);
                await tx.CommitAsync(ct);
                return existing;
            }
            var blueprint = await db.Set<BlueprintEntity>().AsNoTracking().SingleAsync(x => x.Id == exam.BlueprintId, ct);
            if (exam.Status != "PARTIAL" || blueprint.ArchivedAtUtc is not null ||
                Convert.ToBase64String(blueprint.RowVersion) != exam.BlueprintVersion)
                throw new DbUpdateConcurrencyException("Exam or blueprint is no longer eligible.");
            var matrix = JsonSerializer.Deserialize<BlueprintMatrix>(exam.MatrixJson)
                ?? throw new InvalidOperationException("Missing matrix snapshot.");
            foreach (var result in ordered)
            {
                var gap = exam.Gaps.SingleOrDefault(x => x.SlotIndex == result.SlotIndex);
                if (gap is null || string.IsNullOrWhiteSpace(result.CorrelationId) || result.CorrelationId.Length > 128 ||
                    result.Status is not ("REVIEW_REQUIRED" or "FAILED") ||
                    (result.Status == "FAILED" && (result.Drafts.Count != 0 || string.IsNullOrWhiteSpace(result.ErrorCode) ||
                        result.ErrorCode.Length > 128 || result.ErrorCode.Any(c => !(char.IsAsciiLetterUpper(c) || c == '_')))) ||
                    (result.Status == "REVIEW_REQUIRED" && (result.ErrorCode is not null || result.Drafts.Count != gap.MissingCount)))
                    throw new ArgumentException("Invalid slot output.");
                foreach (var draft in result.Drafts)
                {
                    if (draft.Content.SubjectId != matrix.SubjectId || draft.Content.TopicId != gap.TopicId ||
                        draft.Content.Difficulty.ToString().ToUpperInvariant() != gap.Difficulty ||
                        draft.Citation.SourceId.Length > 1000 || draft.Citation.Locator.Length > 1000 ||
                        draft.Content.SourceText != $"{draft.Citation.SourceId} | {draft.Citation.Locator}")
                        throw new ArgumentException("Draft does not match its gap or citation.");
                }
                if (result.Drafts.Select(x => x.Content.Stem).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Drafts.Count)
                    throw new ArgumentException("Duplicate drafts in a slot.");
                if (await db.Set<ExamDraftLinkEntity>().AnyAsync(x => x.ExamId == examId && x.SlotIndex == result.SlotIndex, ct))
                    throw new DbUpdateConcurrencyException("This slot already has drafts.");
            }
            db.Add(new ExamDraftBatchEntity { ExamId = examId, JobId = jobId, Fingerprint = fingerprint, CreatedAtUtc = DateTimeOffset.UtcNow });
            foreach (var result in ordered)
            {
                db.Add(new ExamDraftSlotEntity { ExamId = examId, JobId = jobId, SlotIndex = result.SlotIndex,
                    CorrelationId = result.CorrelationId, Status = result.Status, ErrorCode = result.ErrorCode });
                for (var ordinal = 0; ordinal < result.Drafts.Count; ordinal++)
                {
                    var draft = result.Drafts[ordinal];
                    var content = draft.Content;
                    var written = await writer.CreateDraftAsync(CreateQuestionCommand.Create(actor, school,
                        new McqInput(content.Stem, content.Options, content.CorrectOption.ToString(),
                            content.Difficulty.ToString(), content.SubjectId, QuestionTopic.Create(content.TopicId, content.SubjectId),
                            content.SourceText), QuestionProvenance.AiDraft()), ct);
                    if (written.Status != QuestionStatus.Draft) throw new InvalidOperationException("Writer must produce DRAFT.");
                    db.Add(new ExamDraftLinkEntity { ExamId = examId, JobId = jobId, SlotIndex = result.SlotIndex,
                        Ordinal = ordinal, QuestionId = written.Revision.QuestionId, Revision = written.Revision.Revision,
                        SourceId = draft.Citation.SourceId, SourceLocator = draft.Citation.Locator });
                }
            }
            await db.SaveChangesAsync(ct);
            var links = await Links(examId, jobId).ToArrayAsync(ct);
            await tx.CommitAsync(ct);
            return links;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            // A failed transaction may contain successful writer saves; never reuse their tracked state.
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private IQueryable<ExamDraftLinkEntity> Links(Guid exam, Guid job) => db.Set<ExamDraftLinkEntity>()
        .AsNoTracking().Where(x => x.ExamId == exam && x.JobId == job).OrderBy(x => x.SlotIndex).ThenBy(x => x.Ordinal);
}
