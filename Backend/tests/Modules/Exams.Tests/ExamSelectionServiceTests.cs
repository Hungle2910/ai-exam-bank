using AiExamBank.Modules.Exams.Application;
using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Exams.Tests;

[TestClass]
public sealed class ExamSelectionServiceTests
{
    private static readonly Guid School = Guid.NewGuid(), Department = Guid.NewGuid(), OldDepartment = Guid.NewGuid(),
        Subject = Guid.NewGuid(), Topic = Guid.NewGuid();

    // Mirrors the SQL reader: filters by school, topic and difficulty, but not by department.
    private sealed class SchoolScopedReader(IReadOnlyList<ApprovedQuestionRead> bank) : IApprovedQuestionReader
    {
        public Task<IReadOnlyList<ApprovedQuestionRead>> FindApprovedAsync(ApprovedQuestionQuery query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovedQuestionRead>>(bank.Where(q => q.SchoolId == query.SchoolId && q.TopicId == query.TopicId &&
                q.Difficulty == query.Difficulty && !query.ExcludedQuestionIds.Contains(q.Revision.QuestionId)).Take(query.Limit).ToArray());
        public Task<ApprovedQuestionRead?> GetApprovedAsync(QuestionRevisionRef revision, CancellationToken ct = default) =>
            Task.FromResult(bank.FirstOrDefault(q => q.Revision == revision));
        public Task<IReadOnlyList<ApprovedQuestionCount>> CountApprovedAsync(Guid schoolId, Guid subjectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovedQuestionCount>>([]);
    }

    private static ApprovedQuestionRead Question(Guid department) => new(QuestionRevisionRef.Create(Guid.NewGuid(), 1),
        School, department, Subject, Topic, Difficulty.Easy, "Stem", ["A", "B"], 'A');

    [TestMethod]
    public async Task Questions_from_another_department_of_the_same_school_are_skipped_and_reported_as_a_gap()
    {
        var own = Question(Department);
        var reader = new SchoolScopedReader([Question(OldDepartment), own, Question(OldDepartment)]);
        var matrix = new BlueprintMatrix("Test", Subject, 45, 2, 0.5m, [new(Topic, "EASY", 2, 0.25m)]);

        var result = await new ExamSelectionService(reader).SelectAsync(matrix, School, Department, seed: 3);

        Assert.HasCount(1, result.Items);
        Assert.AreEqual(own.Revision, result.Items[0].Question.Revision);
        Assert.AreEqual("PARTIAL", result.Status);
        Assert.HasCount(1, result.Gaps);
        Assert.AreEqual(1, result.Gaps[0].MissingCount);
    }
}
