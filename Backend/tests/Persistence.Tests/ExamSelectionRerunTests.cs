using AiExamBank.Modules.Exams.Application;
using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using AiExamBank.Persistence.Exams;
using AiExamBank.Persistence.Questions;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ExamSelectionRerunTests
{
    private static AiExamBankDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("BLUEPRINT_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Inconclusive("Set BLUEPRINT_TEST_CONNECTION to a migrated isolated SQL Server test database.");
        return new(new DbContextOptionsBuilder<AiExamBankDbContext>().UseSqlServer(connection).Options);
    }

    private sealed class FixedReader(IReadOnlyList<ApprovedQuestionRead> bank) : IApprovedQuestionReader
    {
        public Task<IReadOnlyList<ApprovedQuestionRead>> FindApprovedAsync(ApprovedQuestionQuery query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovedQuestionRead>>(bank.Where(q => q.TopicId == query.TopicId && q.Difficulty == query.Difficulty &&
                !query.ExcludedQuestionIds.Contains(q.Revision.QuestionId)).Take(query.Limit).ToArray());
        public Task<ApprovedQuestionRead?> GetApprovedAsync(QuestionRevisionRef revision, CancellationToken ct = default) =>
            Task.FromResult(bank.FirstOrDefault(q => q.Revision == revision));
        public Task<IReadOnlyList<ApprovedQuestionCount>> CountApprovedAsync(Guid schoolId, Guid subjectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovedQuestionCount>>([]);
    }

    [TestMethod]
    [DataRow(2, "NEEDS_REVIEW", 0)]
    [DataRow(1, "PARTIAL", 1)]
    public async Task Rerun_after_saved_result_is_a_completed_job_and_keeps_the_stored_result(int available, string status, int missing)
    {
        await using var db = Open();
        var school = new SchoolEntity { Id = Guid.NewGuid(), Name = "Rerun test" };
        var department = new DepartmentEntity { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Math" };
        var actor = new AppUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString(), SchoolId = school.Id };
        db.AddRange(school, department, actor);
        db.Add(new RoleAssignmentEntity { Id = Guid.NewGuid(), UserId = actor.Id, SchoolId = school.Id, DepartmentId = department.Id, Role = AppRole.Teacher });
        db.Add(new SchoolSubjectEntity { SchoolId = school.Id, SubjectId = QuestionTaxonomySeed.MathId, DepartmentId = department.Id });
        await db.SaveChangesAsync();
        var topic = QuestionTaxonomySeed.Topics.First(t => t.SubjectId == QuestionTaxonomySeed.MathId);
        var bank = Enumerable.Range(0, available).Select(_ => new ApprovedQuestionRead(QuestionRevisionRef.Create(Guid.NewGuid(), 1),
            school.Id, department.Id, QuestionTaxonomySeed.MathId, topic.Id, Difficulty.Easy, "Stem", ["A", "B"], 'A')).ToArray();
        var blueprints = new BlueprintStore(db, new QuestionCatalog(db));
        var saved = await blueprints.SaveAsync(actor.Id, school.Id,
            new("Rerun", QuestionTaxonomySeed.MathId, 45, 2, 0.5m, [new(topic.Id, "EASY", 2, 0.25m)]), null, null, default);
        Assert.IsNotNull(saved.Value);
        var store = new ExamGenerationStore(db, blueprints, new ExamSelectionService(new FixedReader(bank)));
        var exam = await store.RequestAsync(actor.Id, school.Id, saved.Value.Id, Convert.ToBase64String(saved.Value.RowVersion), 7, Guid.NewGuid().ToString("N"), default);

        Assert.IsTrue(await store.ExecuteAsync(exam.Id, actor.Id, school.Id, default));
        // Simulate the worker losing the lease after the commit: a fresh context runs the same job again.
        await using var rerunDb = Open();
        var rerunBlueprints = new BlueprintStore(rerunDb, new QuestionCatalog(rerunDb));
        var rerun = new ExamGenerationStore(rerunDb, rerunBlueprints, new ExamSelectionService(new FixedReader(bank)));
        Assert.IsTrue(await rerun.ExecuteAsync(exam.Id, actor.Id, school.Id, default));
        Assert.IsTrue(await rerun.ExecuteAsync(exam.Id, actor.Id, school.Id, default));

        await using var verify = Open();
        var stored = await verify.Set<ExamGenerationEntity>().AsNoTracking().Include(x => x.Items).Include(x => x.Gaps).SingleAsync(x => x.Id == exam.Id);
        Assert.AreEqual(status, stored.Status);
        Assert.HasCount(available, stored.Items);
        Assert.AreEqual(available, stored.Items.Select(x => x.QuestionId).Distinct().Count());
        Assert.HasCount(missing == 0 ? 0 : 1, stored.Gaps);
        if (missing > 0) Assert.AreEqual(missing, stored.Gaps[0].MissingCount);
    }
}
