using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Exams.Application;

public sealed record SelectedExamQuestion(int SlotIndex, ApprovedQuestionRead Question, decimal Points);
public sealed record SlotDeficit(int SlotIndex, Guid TopicId, string Difficulty, int MissingCount, decimal MissingPoints);
public sealed record ExamSelectionResult(IReadOnlyList<SelectedExamQuestion> Items, IReadOnlyList<SlotDeficit> Gaps)
{
    public string Status => Gaps.Count == 0 ? "NEEDS_REVIEW" : "PARTIAL";
    public decimal SelectedPoints => Items.Sum(item => item.Points);
}

/// <summary>Consumes approved-only reads after the caller has authorized the persisted blueprint.</summary>
public sealed class ExamSelectionService(IApprovedQuestionReader reader)
{
    public async Task<ExamSelectionResult> SelectAsync(BlueprintMatrix matrix, Guid schoolId, Guid departmentId,
        int seed, CancellationToken ct = default)
    {
        if (schoolId == Guid.Empty || departmentId == Guid.Empty) throw new ArgumentException("A persisted school and department scope is required.");
        if (matrix.Validate().Count != 0) throw new ArgumentException("The matrix is invalid.", nameof(matrix));
        var selected = new List<SelectedExamQuestion>();
        var gaps = new List<SlotDeficit>();
        var selectedIds = new HashSet<Guid>();
        for (var index = 0; index < matrix.Slots.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var slot = matrix.Slots[index];
            var difficulty = slot.Difficulty switch { "EASY" => Difficulty.Easy, "MEDIUM" => Difficulty.Medium, _ => Difficulty.Hard };
            var excluded = new HashSet<Guid>(selectedIds);
            var count = 0;
            while (count < slot.QuestionCount)
            {
                var batch = await reader.FindApprovedAsync(ApprovedQuestionQuery.Create(schoolId, slot.TopicId,
                    difficulty, ApprovedQuestionQuery.MaxLimit, excluded.ToArray()), ct);
                if (batch.Count == 0) break;
                if (batch.Count > ApprovedQuestionQuery.MaxLimit) throw new InvalidOperationException("Approved reader exceeded the requested limit.");
                // Fail closed on a broken exclusion contract rather than spinning forever or returning duplicate identities.
                if (batch.Any(q => excluded.Contains(q.Revision.QuestionId)) || batch.Select(q => q.Revision.QuestionId).Distinct().Count() != batch.Count)
                    throw new InvalidOperationException("Approved reader returned duplicate or excluded question identities.");
                foreach (var candidate in batch) excluded.Add(candidate.Revision.QuestionId);
                foreach (var candidate in batch.OrderBy(q => Rank(seed, q.Revision.QuestionId), StringComparer.Ordinal))
                {
                    if (candidate.SchoolId != schoolId || candidate.DepartmentId != departmentId ||
                        candidate.SubjectId != matrix.SubjectId || candidate.TopicId != slot.TopicId || candidate.Difficulty != difficulty)
                        throw new InvalidOperationException("Approved reader returned a question outside the authorized matrix scope.");
                    // Pin and revalidate the exact revision, not the mutable question head.
                    var current = await reader.GetApprovedAsync(candidate.Revision, ct);
                    if (current is null) continue;
                    if (current != candidate && (current.SchoolId != schoolId || current.DepartmentId != departmentId ||
                        current.SubjectId != matrix.SubjectId || current.TopicId != slot.TopicId || current.Difficulty != difficulty ||
                        current.Revision != candidate.Revision))
                        throw new InvalidOperationException("Pinned question scope changed during selection.");
                    selected.Add(new(index, current, slot.PointsPerQuestion));
                    selectedIds.Add(current.Revision.QuestionId);
                    if (++count == slot.QuestionCount) break;
                }
                if (batch.Count < ApprovedQuestionQuery.MaxLimit) break;
            }
            if (count < slot.QuestionCount)
                gaps.Add(new(index, slot.TopicId, slot.Difficulty, slot.QuestionCount - count,
                    (slot.QuestionCount - count) * slot.PointsPerQuestion));
        }
        return new(selected, gaps);
    }

    private static string Rank(int seed, Guid questionId) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(seed.ToString(CultureInfo.InvariantCulture) + ":" + questionId.ToString("D"))));
}
