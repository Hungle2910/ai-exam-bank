using AiExamBank.Modules.Exams.Application;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Knowledge.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using AiExamBank.Persistence.Exams;
using AiExamBank.Persistence.Questions;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ExamDraftRerunTests
{
    private static AiExamBankDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("BLUEPRINT_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Inconclusive("Set BLUEPRINT_TEST_CONNECTION to a migrated isolated SQL Server test database.");
        return new(new DbContextOptionsBuilder<AiExamBankDbContext>().UseSqlServer(connection).Options);
    }

    private sealed class EmptyReader : IApprovedQuestionReader
    {
        public Task<IReadOnlyList<ApprovedQuestionRead>> FindApprovedAsync(ApprovedQuestionQuery query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovedQuestionRead>>([]);
        public Task<ApprovedQuestionRead?> GetApprovedAsync(QuestionRevisionRef revision, CancellationToken ct = default) =>
            Task.FromResult<ApprovedQuestionRead?>(null);
        public Task<IReadOnlyList<ApprovedQuestionCount>> CountApprovedAsync(Guid schoolId, Guid subjectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovedQuestionCount>>([]);
    }

    private static DraftSlotResult Output(Guid topic, string stem)
    {
        var citation = SourceCitation.Create("doc-1", "p.1");
        var content = McqContent.Validate(new(stem, ["A", "B", "C", "D"], "A", "EASY", QuestionTaxonomySeed.MathId,
            QuestionTopic.Create(topic, QuestionTaxonomySeed.MathId), "doc-1 | p.1")).Content!;
        return new(0, "corr-0", "REVIEW_REQUIRED", null, [new(content, citation)]);
    }

    private static ExamDraftStore Store(AiExamBankDbContext db)
    {
        var blueprints = new BlueprintStore(db, new QuestionCatalog(db));
        return new(db, new QuestionWriter(db, TimeProvider.System),
            new ExamGenerationStore(db, blueprints, new ExamSelectionService(new EmptyReader())));
    }

    [TestMethod]
    public async Task Rerun_of_a_saved_draft_job_returns_the_stored_links_even_when_ai_output_differs()
    {
        await using var db = Open();
        var school = new SchoolEntity { Id = Guid.NewGuid(), Name = "Draft rerun" };
        var department = new DepartmentEntity { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Math" };
        var actor = new AppUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString(), SchoolId = school.Id };
        db.AddRange(school, department, actor);
        db.Add(new RoleAssignmentEntity { Id = Guid.NewGuid(), UserId = actor.Id, SchoolId = school.Id, DepartmentId = department.Id, Role = AppRole.Teacher });
        db.Add(new SchoolSubjectEntity { SchoolId = school.Id, SubjectId = QuestionTaxonomySeed.MathId, DepartmentId = department.Id });
        await db.SaveChangesAsync();
        var topic = QuestionTaxonomySeed.Topics.First(t => t.SubjectId == QuestionTaxonomySeed.MathId);
        var blueprints = new BlueprintStore(db, new QuestionCatalog(db));
        var saved = await blueprints.SaveAsync(actor.Id, school.Id,
            new("Draft", QuestionTaxonomySeed.MathId, 45, 1, 0.25m, [new(topic.Id, "EASY", 1, 0.25m)]), null, null, default);
        var exams = new ExamGenerationStore(db, blueprints, new ExamSelectionService(new EmptyReader()));
        var exam = await exams.RequestAsync(actor.Id, school.Id, saved.Value!.Id, Convert.ToBase64String(saved.Value.RowVersion), 1, Guid.NewGuid().ToString("N"), default);
        Assert.IsTrue(await exams.ExecuteAsync(exam.Id, actor.Id, school.Id, default));
        var job = Guid.NewGuid();

        var first = await Store(db).SaveAsync(actor.Id, school.Id, exam.Id, job, [Output(topic.Id, "First AI stem")]);
        await using var rerunDb = Open();
        var rerun = await Store(rerunDb).SaveAsync(actor.Id, school.Id, exam.Id, job, [Output(topic.Id, "Regenerated AI stem")]);

        CollectionAssert.AreEqual(first.Select(x => (x.QuestionId, x.Revision)).ToArray(), rerun.Select(x => (x.QuestionId, x.Revision)).ToArray());
        await using var verify = Open();
        Assert.AreEqual(1, await verify.Set<ExamDraftLinkEntity>().CountAsync(x => x.ExamId == exam.Id));
        Assert.AreEqual(1, await verify.Set<ExamDraftBatchEntity>().CountAsync(x => x.ExamId == exam.Id));
        Assert.IsFalse(await verify.QuestionRevisions.AnyAsync(r => r.Stem == "Regenerated AI stem"));
    }
}
