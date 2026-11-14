using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Exams.Application;

public sealed record FinalExamCandidate(int SlotIndex, QuestionRevisionRef Revision, decimal Points);

public sealed record FinalExamSnapshotItem(int SlotIndex, QuestionRevisionRef Revision, decimal Points,
    string Stem, IReadOnlyList<string> Options, char CorrectOption);

public sealed record FinalExamSnapshotContent(Guid SchoolId, Guid DepartmentId, Guid SubjectId,
    string Name, int DurationMinutes, decimal TotalPoints, IReadOnlyList<FinalExamSnapshotItem> Items);

/// <summary>
/// Builds snapshot content for a school exam whose selected revision has already been authorized.
/// The caller must read the decision, build the snapshot and persist it within the same transaction.
/// </summary>
public sealed class FinalExamSnapshotBuilder(IApprovedQuestionReader questions)
{
    public async Task<FinalExamSnapshotContent> BuildAsync(BlueprintMatrix matrix, Guid schoolId, Guid departmentId,
        IReadOnlyList<FinalExamCandidate> candidates, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(candidates);
        if (schoolId == Guid.Empty || departmentId == Guid.Empty)
            throw new ArgumentException("A school and department are required.");
        if (matrix.Validate().Count != 0)
            throw new ArgumentException("The stored matrix is invalid.", nameof(matrix));

        ValidateCandidates(matrix, candidates);
        var items = new List<FinalExamSnapshotItem>(candidates.Count);
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var slot = matrix.Slots[candidate.SlotIndex];
            var question = await questions.GetApprovedAsync(candidate.Revision, ct);
            if (question is null || question.Revision != candidate.Revision || question.SchoolId != schoolId ||
                question.DepartmentId != departmentId || question.SubjectId != matrix.SubjectId ||
                question.TopicId != slot.TopicId || !string.Equals(question.Difficulty.ToString(), slot.Difficulty, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A pinned revision is no longer eligible for its exam slot.");

            var content = McqContent.Create(new(question.Stem, question.Options, question.CorrectOption.ToString(),
                slot.Difficulty, matrix.SubjectId, QuestionTopic.Create(slot.TopicId, matrix.SubjectId)));
            items.Add(new(candidate.SlotIndex, candidate.Revision, candidate.Points, content.Stem,
                Array.AsReadOnly(content.Options.ToArray()), content.CorrectOption));
        }

        return new(schoolId, departmentId, matrix.SubjectId, matrix.Name, matrix.DurationMinutes,
            matrix.TotalPoints, items.AsReadOnly());
    }

    private static void ValidateCandidates(BlueprintMatrix matrix, IReadOnlyList<FinalExamCandidate> candidates)
    {
        if (candidates.Count != matrix.TotalQuestions)
            throw new InvalidOperationException("The exam does not contain the required number of questions.");

        var usedQuestions = new HashSet<Guid>();
        var counts = new int[matrix.Slots.Count];
        foreach (var candidate in candidates)
        {
            if (candidate is null || candidate.Revision is null || candidate.SlotIndex < 0 || candidate.SlotIndex >= counts.Length)
                throw new InvalidOperationException("An exam item does not reference a valid slot and revision.");
            if (!usedQuestions.Add(candidate.Revision.QuestionId))
                throw new InvalidOperationException("The same question appears more than once in the exam.");
            var slot = matrix.Slots[candidate.SlotIndex];
            if (candidate.Points != slot.PointsPerQuestion || ++counts[candidate.SlotIndex] > slot.QuestionCount)
                throw new InvalidOperationException("The exam count or score does not match its matrix slot.");
        }
        // Exact total count plus per-slot upper bounds ensures each slot is full.
        // Per-item points and the validated matrix then ensure the total score.
    }
}
