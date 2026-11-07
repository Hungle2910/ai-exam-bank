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
public sealed class ExamGenerationPersistenceTests
{
    [TestMethod]
    public async Task Empty_bank_request_is_idempotent_and_persists_exact_gaps()
    {
        var connection = Environment.GetEnvironmentVariable("BLUEPRINT_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Inconclusive("Set BLUEPRINT_TEST_CONNECTION to an isolated migrated SQL Server database.");
        await using var db = new AiExamBankDbContext(new DbContextOptionsBuilder<AiExamBankDbContext>().UseSqlServer(connection).Options);
        var school = new SchoolEntity { Id = Guid.NewGuid(), Name = "Exam integration test" };
        var dept = new DepartmentEntity { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Math" };
        var user = new AppUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString(), SchoolId = school.Id };
        db.AddRange(school, dept, user);
        db.Add(new RoleAssignmentEntity { Id = Guid.NewGuid(), UserId = user.Id, SchoolId = school.Id, DepartmentId = dept.Id, Role = AppRole.Teacher });
        db.Add(new SchoolSubjectEntity { SchoolId = school.Id, SubjectId = QuestionTaxonomySeed.MathId, DepartmentId = dept.Id });
        await db.SaveChangesAsync();
        var blueprints = new BlueprintStore(db, new QuestionCatalog(db));
        var topic = QuestionTaxonomySeed.Topics.First(t => t.SubjectId == QuestionTaxonomySeed.MathId);
        var blueprint = (await blueprints.SaveAsync(user.Id, school.Id,
            new BlueprintMatrix("Empty bank", QuestionTaxonomySeed.MathId, 45, 2, 1, [new(topic.Id, "EASY", 2, 0.5m)]), null, null, default)).Value!;
        var store = new ExamGenerationStore(db, blueprints, new ExamSelectionService(new ApprovedQuestionReader(db)));
        var version = Convert.ToBase64String(blueprint.RowVersion);
        var exam = await store.RequestAsync(user.Id, school.Id, blueprint.Id, version, 1, "one-request", default);
        var retry = await store.RequestAsync(user.Id, school.Id, blueprint.Id, version, 1, "one-request", default);
        Assert.AreEqual(exam.Id, retry.Id);
        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() => store.RequestAsync(user.Id, school.Id, blueprint.Id, version, 2, "one-request", default));
        Assert.IsFalse(await store.ExecuteAsync(exam.Id, Guid.NewGuid(), school.Id, default));
        Assert.IsTrue(await store.ExecuteAsync(exam.Id, user.Id, school.Id, default));
        Assert.IsTrue(await store.ExecuteAsync(exam.Id, user.Id, school.Id, default));
        var saved = await db.Set<ExamGenerationEntity>().AsNoTracking().Include(x => x.Gaps).Include(x => x.Items).SingleAsync(x => x.Id == exam.Id);
        Assert.AreEqual("PARTIAL", saved.Status);
        Assert.IsEmpty(saved.Items);
        Assert.HasCount(1, saved.Gaps);
        Assert.AreEqual(2, saved.Gaps[0].MissingCount);
        Assert.AreEqual(1m, saved.Gaps[0].MissingPoints);
        Assert.IsFalse(await store.Accessible(user.Id, Guid.NewGuid()).AnyAsync());

        var citation = AiExamBank.Modules.Knowledge.Contracts.SourceCitation.Create("fixture-source", "page 1");
        var drafts = Enumerable.Range(1, 2).Select(i => new ValidatedAiDraft(McqContent.Create(new(
            $"Draft fixture {i}?", ["One", "Two", "Three", "Four"], "A", "EASY", QuestionTaxonomySeed.MathId,
            QuestionTopic.Create(topic.Id, QuestionTaxonomySeed.MathId), "fixture-source | page 1")), citation)).ToArray();
        DraftSlotResult[] outputs = [new(0, "fixture-correlation", "REVIEW_REQUIRED", null, drafts)];
        var draftJob = Guid.NewGuid();
        var draftStore = new ExamDraftStore(db, new QuestionWriter(db, TimeProvider.System));
        await Assert.ThrowsExactlyAsync<KeyNotFoundException>(() => draftStore.SaveAsync(Guid.NewGuid(), school.Id, exam.Id, draftJob, outputs));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => draftStore.SaveAsync(user.Id, school.Id, exam.Id, draftJob,
            [outputs[0] with { Drafts = drafts.Take(1).ToArray() }]));
        var failingStore = new ExamDraftStore(db, new FailSecondWriter(new QuestionWriter(db, TimeProvider.System)));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => failingStore.SaveAsync(user.Id, school.Id, exam.Id, draftJob, outputs));
        Assert.AreEqual(0, await db.Questions.CountAsync(x => x.SchoolId == school.Id));
        Assert.IsFalse(await db.Set<ExamDraftBatchEntity>().AnyAsync(x => x.ExamId == exam.Id));
        var failedJob = Guid.NewGuid();
        Assert.IsEmpty(await draftStore.SaveAsync(user.Id, school.Id, exam.Id, failedJob,
            [new(0, "failed-correlation", "FAILED", "GENERATION_TIMEOUT", [])]));
        Assert.AreEqual("FAILED", (await db.Set<ExamDraftSlotEntity>().SingleAsync(x => x.ExamId == exam.Id && x.JobId == failedJob)).Status);
        await Assert.ThrowsExactlyAsync<KeyNotFoundException>(() => draftStore.SaveAsync(user.Id, Guid.NewGuid(), exam.Id, draftJob, outputs));
        var links = await draftStore.SaveAsync(user.Id, school.Id, exam.Id, draftJob, outputs);
        var repeated = await draftStore.SaveAsync(user.Id, school.Id, exam.Id, draftJob, outputs);
        CollectionAssert.AreEqual(links.Select(x => x.QuestionId).ToArray(), repeated.Select(x => x.QuestionId).ToArray());
        Assert.AreEqual(2, await db.Questions.CountAsync(x => x.SchoolId == school.Id));
        foreach (var link in links)
        {
            var revision = await db.QuestionRevisions.SingleAsync(x => x.QuestionId == link.QuestionId && x.Revision == link.Revision);
            Assert.AreEqual(QuestionStatus.Draft, revision.Status);
            Assert.AreEqual(QuestionOrigin.AiDraft, revision.Origin);
        }
        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() => draftStore.SaveAsync(user.Id, school.Id, exam.Id, draftJob,
            [outputs[0] with { CorrelationId = "changed" }]));
        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() => draftStore.SaveAsync(user.Id, school.Id, exam.Id, Guid.NewGuid(), outputs));
        Assert.AreEqual("PARTIAL", (await db.Set<ExamGenerationEntity>().SingleAsync(x => x.Id == exam.Id)).Status);
        Assert.IsFalse(await db.Set<ExamItemEntity>().AnyAsync(x => x.ExamId == exam.Id));

        // Test-only fixtures model the output of M3 approval; this is not an application approval path.
        var author = new AppUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString(), SchoolId = school.Id };
        db.Add(author);
        var approvedIds = new HashSet<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var questionId = Guid.NewGuid();
            if (i < 2) approvedIds.Add(questionId);
            db.Add(new QuestionEntity { Id = questionId, SchoolId = school.Id, DepartmentId = dept.Id,
                SubjectId = QuestionTaxonomySeed.MathId, OwnerUserId = author.Id, LatestRevision = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
            db.Add(new QuestionRevisionEntity { QuestionId = questionId, Revision = 1, SubjectId = QuestionTaxonomySeed.MathId,
                TopicId = topic.Id, Difficulty = Difficulty.Easy, Stem = "Test-only question", CorrectOption = "A",
                Status = i < 2 ? QuestionStatus.Approved : QuestionStatus.Draft, Origin = QuestionOrigin.Manual,
                CreatedByUserId = author.Id, CreatedAtUtc = DateTimeOffset.UtcNow });
            foreach (var label in new[] { "A", "B", "C", "D" })
                db.Add(new QuestionOptionEntity { QuestionId = questionId, Revision = 1, Label = label, Text = "Option " + label });
        }
        await db.SaveChangesAsync();
        var enough = await store.RequestAsync(user.Id, school.Id, blueprint.Id, version, 1, "approved-bank", default);
        Assert.IsTrue(await store.ExecuteAsync(enough.Id, user.Id, school.Id, default));
        var selected = await db.Set<ExamGenerationEntity>().AsNoTracking().Include(x => x.Items).Include(x => x.Gaps).SingleAsync(x => x.Id == enough.Id);
        Assert.AreEqual("NEEDS_REVIEW", selected.Status);
        Assert.IsEmpty(selected.Gaps);
        CollectionAssert.AreEquivalent(approvedIds.ToArray(), selected.Items.Select(x => x.QuestionId).ToArray());
        Assert.IsTrue(selected.Items.All(x => x.Revision == 1));
        Assert.AreEqual(1m, selected.Items.Sum(x => x.Points));
    }

    private sealed class FailSecondWriter(IQuestionWriter inner) : IQuestionWriter
    {
        private int calls;
        public Task<QuestionWriteResult> CreateDraftAsync(CreateQuestionCommand command, CancellationToken cancellationToken = default)
        {
            if (++calls == 2) throw new InvalidOperationException("Injected second-write failure.");
            return inner.CreateDraftAsync(command, cancellationToken);
        }
        public Task<QuestionWriteResult> CreateRevisionAsync(CreateRevisionCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
