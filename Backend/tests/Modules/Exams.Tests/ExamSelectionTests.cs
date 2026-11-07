using AiExamBank.Modules.Exams.Application;
using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Exams.Tests;

[TestClass]
public sealed class ExamSelectionTests
{
    private static readonly Guid School = Guid.NewGuid(), Department = Guid.NewGuid(), Subject = Guid.NewGuid(), Topic = Guid.NewGuid();
    private static BlueprintMatrix Matrix(int count = 2) => new("Exam", Subject, 45, count, count * 0.25m, [new(Topic, "EASY", count, 0.25m)]);
    private static ApprovedQuestionRead Question() => new(QuestionRevisionRef.Create(Guid.NewGuid(), 2), School, Department,
        Subject, Topic, Difficulty.Easy, "Question", ["A", "B", "C", "D"], 'A');

    [TestMethod]
    public async Task Enough_approved_questions_pin_revisions_and_points_without_claiming_finalization()
    {
        var result = await new ExamSelectionService(new Reader([Question(), Question(), Question()])).SelectAsync(Matrix(), School, Department, 42);
        Assert.AreEqual("NEEDS_REVIEW", result.Status);
        Assert.HasCount(2, result.Items);
        Assert.IsEmpty(result.Gaps);
        Assert.AreEqual(0.5m, result.SelectedPoints);
        Assert.IsTrue(result.Items.All(x => x.Question.Revision.Revision == 2));
        Assert.HasCount(2, result.Items.Select(x => x.Question.Revision.QuestionId).Distinct());
    }

    [TestMethod]
    public async Task Missing_questions_report_exact_deficit_and_points()
    {
        var result = await new ExamSelectionService(new Reader([Question()])).SelectAsync(Matrix(3), School, Department, 42);
        Assert.AreEqual("PARTIAL", result.Status);
        Assert.HasCount(1, result.Gaps);
        Assert.AreEqual(2, result.Gaps[0].MissingCount);
        Assert.AreEqual(0.5m, result.Gaps[0].MissingPoints);
    }

    [TestMethod]
    public async Task Empty_bank_produces_a_gap_for_each_slot()
    {
        var matrix = new BlueprintMatrix("Exam", Subject, 45, 3, 3,
            [new(Topic, "EASY", 1, 1), new(Topic, "HARD", 2, 1)]);
        var result = await new ExamSelectionService(new Reader([])).SelectAsync(matrix, School, Department, 1);
        Assert.HasCount(2, result.Gaps);
        Assert.AreEqual(3, result.Gaps.Sum(x => x.MissingCount));
        Assert.IsEmpty(result.Items);
    }

    [TestMethod]
    public async Task Same_seed_and_candidates_are_reproducible()
    {
        var bank = new[] { Question(), Question(), Question(), Question() };
        var first = await new ExamSelectionService(new Reader(bank)).SelectAsync(Matrix(), School, Department, 42);
        var second = await new ExamSelectionService(new Reader(bank.Reverse().ToArray())).SelectAsync(Matrix(), School, Department, 42);
        CollectionAssert.AreEqual(first.Items.Select(x => x.Question.Revision.QuestionId).ToArray(), second.Items.Select(x => x.Question.Revision.QuestionId).ToArray());
    }

    [TestMethod]
    public async Task Cross_department_reader_data_fails_closed()
    {
        var wrong = Question() with { DepartmentId = Guid.NewGuid() };
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new ExamSelectionService(new Reader([wrong]))
            .SelectAsync(Matrix(), School, Department, 1));
    }

    [TestMethod]
    public async Task Approval_revoked_during_selection_becomes_a_gap()
    {
        var result = await new ExamSelectionService(new Reader([Question()]) { Revoked = true }).SelectAsync(Matrix(1), School, Department, 1);
        Assert.IsEmpty(result.Items);
        Assert.AreEqual(1, result.Gaps[0].MissingCount);
    }

    private sealed class Reader(IReadOnlyList<ApprovedQuestionRead> bank) : IApprovedQuestionReader
    {
        public bool Revoked { get; init; }
        public Task<IReadOnlyList<ApprovedQuestionRead>> FindApprovedAsync(ApprovedQuestionQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ApprovedQuestionRead>>(bank.Where(q => !query.ExcludedQuestionIds.Contains(q.Revision.QuestionId)).Take(query.Limit).ToArray());
        public Task<ApprovedQuestionRead?> GetApprovedAsync(QuestionRevisionRef revision, CancellationToken cancellationToken = default) =>
            Task.FromResult(Revoked ? null : bank.SingleOrDefault(q => q.Revision == revision));
        public Task<IReadOnlyList<ApprovedQuestionCount>> CountApprovedAsync(Guid schoolId, Guid subjectId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
