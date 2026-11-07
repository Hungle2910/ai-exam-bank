using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Persistence.Exams;
using AiExamBank.Persistence.Questions;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Tests;

[TestClass]
public sealed class BlueprintPersistenceTests
{
    private static AiExamBankDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("BLUEPRINT_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Inconclusive("Set BLUEPRINT_TEST_CONNECTION to a migrated isolated SQL Server test database.");
        return new(new DbContextOptionsBuilder<AiExamBankDbContext>().UseSqlServer(connection).Options);
    }

    [TestMethod]
    public async Task Roundtrip_scope_validation_and_stale_write_use_real_sql_server()
    {
        await using var db = Open();
        var school = new SchoolEntity { Id = Guid.NewGuid(), Name = "Blueprint test" };
        var department = new DepartmentEntity { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Math" };
        var actor = new AppUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString(), SchoolId = school.Id };
        db.AddRange(school, department, actor);
        db.Add(new RoleAssignmentEntity { Id = Guid.NewGuid(), UserId = actor.Id, SchoolId = school.Id, DepartmentId = department.Id, Role = AppRole.Teacher });
        db.Add(new SchoolSubjectEntity { SchoolId = school.Id, SubjectId = QuestionTaxonomySeed.MathId, DepartmentId = department.Id });
        await db.SaveChangesAsync();
        var store = new BlueprintStore(db, new QuestionCatalog(db));
        var topic = QuestionTaxonomySeed.Topics.First(t => t.SubjectId == QuestionTaxonomySeed.MathId);
        var matrix = new BlueprintMatrix("Test", QuestionTaxonomySeed.MathId, 45, 3, 0.75m, [new(topic.Id, "EASY", 3, 0.25m)]);
        var created = await store.SaveAsync(actor.Id, school.Id, matrix, null, null, default);
        Assert.IsNotNull(created.Value);
        var id = created.Value.Id;
        Assert.HasCount(8, created.Value.RowVersion);
        Assert.IsFalse(await store.Accessible(Guid.NewGuid(), school.Id).AnyAsync());
        Assert.IsFalse(await store.Accessible(actor.Id, Guid.NewGuid()).AnyAsync());
        var invalid = await store.SaveAsync(actor.Id, school.Id, matrix with { Slots = [new(Guid.NewGuid(), "EASY", 3, 0.25m)] }, null, null, default);
        Assert.IsNull(invalid.Value);
        Assert.IsTrue(invalid.Errors.ContainsKey("slots[0].topicId"));

        await using var first = Open();
        await using var second = Open();
        var a = await first.Set<BlueprintEntity>().Include(b => b.Slots).SingleAsync(b => b.Id == id);
        var b = await second.Set<BlueprintEntity>().Include(b => b.Slots).SingleAsync(b => b.Id == id);
        var oldVersion = b.RowVersion.ToArray();
        var winner = await new BlueprintStore(first, new QuestionCatalog(first)).SaveAsync(actor.Id, school.Id,
            matrix with { Name = "Winner" }, a, a.RowVersion, default);
        Assert.IsNotNull(winner.Value);
        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() =>
            new BlueprintStore(second, new QuestionCatalog(second)).SaveAsync(actor.Id, school.Id,
                matrix with { Name = "Loser" }, b, oldVersion, default));
        await using var verify = Open();
        var saved = await verify.Set<BlueprintEntity>().AsNoTracking().Include(x => x.Slots).SingleAsync(x => x.Id == id);
        Assert.AreEqual("Winner", saved.Name);
        Assert.AreEqual(0.75m, saved.TotalPoints);
        Assert.HasCount(1, saved.Slots);
        Assert.AreEqual(topic.Id, saved.Slots[0].TopicId);
        await using var staleEditor = Open();
        var beforeArchive = await staleEditor.Set<BlueprintEntity>().Include(x => x.Slots).SingleAsync(x => x.Id == id);
        var archiveStore = new BlueprintStore(verify, new QuestionCatalog(verify));
        var toArchive = await archiveStore.Accessible(actor.Id, school.Id).Include(x => x.Slots).SingleAsync(x => x.Id == id);
        await archiveStore.ArchiveAsync(toArchive, toArchive.RowVersion, default);
        Assert.IsFalse(await archiveStore.List(actor.Id, school.Id).AnyAsync(x => x.Id == id));
        var archived = await archiveStore.List(actor.Id, school.Id, archived: true).Include(x => x.Slots).SingleAsync(x => x.Id == id);
        Assert.IsNotNull(archived.ArchivedAtUtc);
        Assert.AreEqual("Winner", archived.Name);
        Assert.HasCount(1, archived.Slots);
        Assert.AreEqual(topic.Id, archived.Slots[0].TopicId);
        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() =>
            new BlueprintStore(staleEditor, new QuestionCatalog(staleEditor)).SaveAsync(actor.Id, school.Id,
                matrix with { Name = "Stale edit after archive" }, beforeArchive, beforeArchive.RowVersion, default));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => archiveStore.SaveAsync(
            actor.Id, school.Id, matrix, archived, archived.RowVersion, default));
        var assignment = await verify.RoleAssignments.SingleAsync(r => r.UserId == actor.Id);
        verify.Remove(assignment);
        await verify.SaveChangesAsync();
        Assert.IsFalse(await new BlueprintStore(verify, new QuestionCatalog(verify)).Accessible(actor.Id, school.Id).AnyAsync());
    }
}
