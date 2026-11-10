using System.Data.Common;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using AiExamBank.Persistence.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiExamBank.Persistence.Tests;

/// <summary>
/// Revision behaviour against a real SQL Server: edit after approval, stale updates, immutability, history order,
/// scope and query count. They need a server, so they run only when <c>AIEXAMBANK_TEST_SQLSERVER</c> holds a
/// connection string without a database, for example
/// <c>Server=.\SQLEXPRESS;Integrated Security=true;TrustServerCertificate=True</c>. Each class creates a throwaway
/// database, applies every migration, and drops it afterwards.
/// </summary>
[TestClass]
public sealed class QuestionRevisionDatabaseTests
{
    private const string ServerVariable = "AIEXAMBANK_TEST_SQLSERVER";
    private static readonly Guid MathTopic = QuestionTaxonomySeed.Topics.First(topic => topic.SubjectId == QuestionTaxonomySeed.MathId).Id;
    private static readonly Guid OtherMathTopic = QuestionTaxonomySeed.Topics.Where(topic => topic.SubjectId == QuestionTaxonomySeed.MathId).Skip(1).First().Id;

    private static string? _server;
    private static string _database = "";

    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid DepartmentId = Guid.NewGuid();
    private static readonly Guid OtherDepartmentId = Guid.NewGuid();
    private static readonly Guid Teacher = Guid.NewGuid();
    private static readonly Guid OtherTeacher = Guid.NewGuid();
    private static readonly Guid Head = Guid.NewGuid();
    private static readonly Guid Outsider = Guid.NewGuid();

    [ClassInitialize]
    public static async Task CreateDatabase(TestContext _)
    {
        _server = Environment.GetEnvironmentVariable(ServerVariable);
        if (string.IsNullOrWhiteSpace(_server)) return;

        _database = "AiExamBankRevisionTests_" + Guid.NewGuid().ToString("N");
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        db.Schools.Add(new SchoolEntity { Id = SchoolId, Name = "Trường thử" });
        db.Departments.Add(new DepartmentEntity { Id = DepartmentId, SchoolId = SchoolId, Name = "Tổ Toán" });
        db.Departments.Add(new DepartmentEntity { Id = OtherDepartmentId, SchoolId = SchoolId, Name = "Tổ Lý" });
        await db.SaveChangesAsync();

        foreach (var (id, name) in new[] { (Teacher, "teacher"), (OtherTeacher, "other-teacher"), (Head, "head"), (Outsider, "outsider") })
            db.Users.Add(new AppUser { Id = id, UserName = name, NormalizedUserName = name.ToUpperInvariant(), SchoolId = SchoolId });
        await db.SaveChangesAsync();

        db.RoleAssignments.AddRange(
            new RoleAssignmentEntity { Id = Guid.NewGuid(), UserId = Teacher, Role = AppRole.Teacher, SchoolId = SchoolId, DepartmentId = DepartmentId },
            new RoleAssignmentEntity { Id = Guid.NewGuid(), UserId = OtherTeacher, Role = AppRole.Teacher, SchoolId = SchoolId, DepartmentId = OtherDepartmentId },
            new RoleAssignmentEntity { Id = Guid.NewGuid(), UserId = Head, Role = AppRole.DepartmentHead, SchoolId = SchoolId, DepartmentId = DepartmentId });
        db.SchoolSubjects.Add(new SchoolSubjectEntity { SchoolId = SchoolId, SubjectId = QuestionTaxonomySeed.MathId, DepartmentId = DepartmentId });
        await db.SaveChangesAsync();
    }

    [ClassCleanup]
    public static async Task DropDatabase()
    {
        if (string.IsNullOrWhiteSpace(_server) || _database.Length == 0) return;
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    [TestInitialize]
    public void RequireServer()
    {
        if (string.IsNullOrWhiteSpace(_server))
            Assert.Inconclusive($"Set {ServerVariable} to run the SQL Server revision tests.");
    }

    private static AiExamBankDbContext NewContext(CommandCounter? counter = null) =>
        new(new DbContextOptionsBuilder<AiExamBankDbContext>()
            .UseSqlServer($"{_server};Database={_database}")
            .AddInterceptors(counter ?? new CommandCounter())
            .Options);

    private static McqInput Content(string stem, Guid? topic = null, string correct = "A") => new(
        stem,
        ["một", "hai", "ba", "bốn"],
        correct,
        "EASY",
        QuestionTaxonomySeed.MathId,
        QuestionTopic.Create(topic ?? MathTopic, QuestionTaxonomySeed.MathId),
        "SGK");

    private static async Task<Guid> CreateDraftAsync(string stem, QuestionProvenance? provenance = null)
    {
        await using var db = NewContext();
        var result = await new QuestionWriter(db, TimeProvider.System).CreateDraftAsync(
            CreateQuestionCommand.Create(Teacher, SchoolId, Content(stem), provenance ?? QuestionProvenance.Manual()));
        return result.Revision.QuestionId;
    }

    private static async Task EditAsync(Guid questionId, int baseRevision, string stem, Guid? topic = null, Guid? actor = null)
    {
        await using var db = NewContext();
        await new QuestionWriter(db, TimeProvider.System).CreateRevisionAsync(
            CreateRevisionCommand.Create(actor ?? Teacher, questionId, baseRevision, Content(stem, topic), QuestionProvenance.Manual()));
    }

    /// <summary>What the Review module does: moves the status of a revision, never its content.</summary>
    private static async Task SetStatusAsync(Guid questionId, int revision, QuestionStatus status)
    {
        await using var db = NewContext();
        var row = await db.QuestionRevisions.SingleAsync(item => item.QuestionId == questionId && item.Revision == revision);
        row.Status = status;
        await db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task Editing_an_approved_question_adds_a_draft_and_keeps_the_approved_revision_in_use()
    {
        var id = await CreateDraftAsync("Câu gốc");
        await SetStatusAsync(id, 1, QuestionStatus.PendingReview);
        await SetStatusAsync(id, 1, QuestionStatus.Approved);

        await EditAsync(id, 1, "Câu đã sửa");

        await using var db = NewContext();
        var revisions = await db.QuestionRevisions.AsNoTracking().Where(item => item.QuestionId == id).OrderBy(item => item.Revision).ToListAsync();
        Assert.HasCount(2, revisions);
        Assert.AreEqual(QuestionStatus.Approved, revisions[0].Status);
        Assert.AreEqual("Câu gốc", revisions[0].Stem);
        Assert.AreEqual(QuestionStatus.Draft, revisions[1].Status, "the new revision must be reviewed again");
        Assert.AreEqual("Câu đã sửa", revisions[1].Stem);

        var reader = new ApprovedQuestionReader(db);
        var pinned = await reader.GetApprovedAsync(QuestionRevisionRef.Create(id, 1));
        Assert.IsNotNull(pinned, "the pinned revision still reads exactly as approved");
        Assert.AreEqual("Câu gốc", pinned.Stem);
        Assert.IsNull(await reader.GetApprovedAsync(QuestionRevisionRef.Create(id, 2)), "a draft is never selectable");

        var selectable = await reader.FindApprovedAsync(ApprovedQuestionQuery.Create(SchoolId, MathTopic, Difficulty.Easy, 50));
        var mine = selectable.Single(item => item.Revision.QuestionId == id);
        Assert.AreEqual(1, mine.Revision.Revision, "selection keeps using the approved revision while the draft is open");
    }

    [TestMethod]
    public async Task A_stale_base_revision_is_a_conflict()
    {
        var id = await CreateDraftAsync("v1");
        await EditAsync(id, 1, "v2");

        await Assert.ThrowsExactlyAsync<QuestionConflictException>(() => EditAsync(id, 1, "v2 again"));
        await EditAsync(id, 2, "v3");
    }

    [TestMethod]
    public async Task Two_editors_racing_for_the_same_revision_cannot_both_win()
    {
        var id = await CreateDraftAsync("race");

        var results = await Task.WhenAll(
            Try(() => EditAsync(id, 1, "editor one")),
            Try(() => EditAsync(id, 1, "editor two")));

        Assert.AreEqual(1, results.Count(item => item is null), "exactly one edit wins");
        Assert.IsTrue(results.Where(item => item is not null).All(item => item is QuestionConflictException));

        await using var db = NewContext();
        Assert.AreEqual(2, await db.QuestionRevisions.CountAsync(item => item.QuestionId == id));
        Assert.AreEqual(2, (await db.Questions.SingleAsync(item => item.Id == id)).LatestRevision);

        static async Task<Exception?> Try(Func<Task> action)
        {
            try { await action(); return null; }
            catch (Exception exception) { return exception; }
        }
    }

    [TestMethod]
    public async Task A_question_waiting_for_review_cannot_be_edited()
    {
        var id = await CreateDraftAsync("waiting");
        await SetStatusAsync(id, 1, QuestionStatus.PendingReview);

        await Assert.ThrowsExactlyAsync<QuestionConflictException>(() => EditAsync(id, 1, "sneaky"));
    }

    [TestMethod]
    public async Task A_decided_revision_cannot_be_rewritten_or_deleted()
    {
        var id = await CreateDraftAsync("final");
        await SetStatusAsync(id, 1, QuestionStatus.Approved);

        await using (var db = NewContext())
        {
            var row = await db.QuestionRevisions.SingleAsync(item => item.QuestionId == id);
            row.Stem = "rewritten";
            await Assert.ThrowsExactlyAsync<QuestionImmutableException>(() => db.SaveChangesAsync());
        }

        await using (var db = NewContext())
        {
            var row = await db.QuestionRevisions.SingleAsync(item => item.QuestionId == id);
            row.CorrectOption = "B";
            await Assert.ThrowsExactlyAsync<QuestionImmutableException>(() => db.SaveChangesAsync());
        }

        await using (var db = NewContext())
        {
            var row = await db.QuestionRevisions.SingleAsync(item => item.QuestionId == id);
            row.Status = QuestionStatus.Draft;
            await Assert.ThrowsExactlyAsync<QuestionImmutableException>(() => db.SaveChangesAsync(), "approved is final");
        }

        await using (var db = NewContext())
        {
            db.QuestionRevisions.Remove(await db.QuestionRevisions.SingleAsync(item => item.QuestionId == id));
            await Assert.ThrowsExactlyAsync<QuestionImmutableException>(() => db.SaveChangesAsync());
        }

        await using (var db = NewContext())
        {
            (await db.QuestionOptions.FirstAsync(item => item.QuestionId == id)).Text = "rewritten";
            await Assert.ThrowsExactlyAsync<QuestionImmutableException>(() => db.SaveChangesAsync());
        }

        await using (var db = NewContext())
        {
            var row = await db.QuestionRevisions.AsNoTracking().SingleAsync(item => item.QuestionId == id);
            Assert.AreEqual("final", row.Stem);
            Assert.AreEqual(QuestionStatus.Approved, row.Status);
        }
    }

    /// <summary>Runs raw SQL, which skips the application guard, and returns the error number the database raised (0 if none).</summary>
    private static async Task<int> RawAsync(FormattableString sql)
    {
        await using var db = NewContext();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(sql);
            return 0;
        }
        catch (Exception exception) when (FindSqlError(exception) is { } number)
        {
            return number;
        }

        static int? FindSqlError(Exception? exception)
        {
            for (; exception is not null; exception = exception.InnerException)
                if (exception is Microsoft.Data.SqlClient.SqlException sql) return sql.Number;
            return null;
        }
    }

    [TestMethod]
    public async Task The_database_itself_refuses_to_rewrite_a_revision()
    {
        var id = await CreateDraftAsync("db-final");
        await SetStatusAsync(id, 1, QuestionStatus.Approved);

        Assert.AreEqual(51002, await RawAsync($"UPDATE QuestionRevisions SET Stem = N'x' WHERE QuestionId = {id}"));
        Assert.AreEqual(51002, await RawAsync($"UPDATE QuestionRevisions SET CorrectOption = 'B' WHERE QuestionId = {id}"));
        Assert.AreEqual(51002, await RawAsync($"UPDATE QuestionRevisions SET SourceText = NULL WHERE QuestionId = {id}"), "NULL is a change too");
        Assert.AreEqual(51003, await RawAsync($"UPDATE QuestionRevisions SET Status = 'Draft' WHERE QuestionId = {id}"));
        // Options reference the revision, so the foreign key (547) refuses first; the trigger (51001) covers a revision without options.
        Assert.AreNotEqual(0, await RawAsync($"DELETE FROM QuestionRevisions WHERE QuestionId = {id}"));
        Assert.AreNotEqual(0, await RawAsync($"UPDATE QuestionRevisions SET Revision = 9 WHERE QuestionId = {id}"));
        Assert.AreEqual(51004, await RawAsync($"UPDATE QuestionOptions SET Text = N'x' WHERE QuestionId = {id}"));
        Assert.AreEqual(51004, await RawAsync($"DELETE FROM QuestionOptions WHERE QuestionId = {id}"));
        Assert.AreNotEqual(0, await RawAsync($"DELETE FROM Questions WHERE Id = {id}"), "refused by the revisions' foreign key, or by the trigger");

        await using var db = NewContext();
        var row = await db.QuestionRevisions.AsNoTracking().SingleAsync(item => item.QuestionId == id);
        Assert.AreEqual("db-final", row.Stem);
        Assert.AreEqual("SGK", row.SourceText);
        Assert.AreEqual(QuestionStatus.Approved, row.Status);
        Assert.AreEqual(4, await db.QuestionOptions.CountAsync(item => item.QuestionId == id));
    }

    [TestMethod]
    public async Task The_database_lets_the_review_flow_move_a_status_and_a_question_move_forward()
    {
        var id = await CreateDraftAsync("db-flow");

        Assert.AreEqual(0, await RawAsync($"UPDATE QuestionRevisions SET Status = 'PendingReview' WHERE QuestionId = {id}"));
        Assert.AreEqual(0, await RawAsync($"UPDATE QuestionRevisions SET Status = 'Approved' WHERE QuestionId = {id}"));
        Assert.AreEqual(0, await RawAsync($"UPDATE QuestionRevisions SET Status = 'Approved' WHERE QuestionId = {id}"), "no change is not a change");
        Assert.AreEqual(0, await RawAsync($"UPDATE Questions SET UpdatedAtUtc = SYSDATETIMEOFFSET() WHERE Id = {id}"));
    }

    [TestMethod]
    public async Task The_latest_revision_of_a_question_cannot_move_backwards_in_the_database()
    {
        var id = await CreateDraftAsync("db-forward");
        await EditAsync(id, 1, "db-forward 2");

        Assert.AreEqual(51006, await RawAsync($"UPDATE Questions SET LatestRevision = 1 WHERE Id = {id}"));
    }

    [TestMethod]
    public async Task Even_a_draft_revision_keeps_its_content_but_may_change_status()
    {
        var id = await CreateDraftAsync("draft");

        await using (var db = NewContext())
        {
            (await db.QuestionRevisions.SingleAsync(item => item.QuestionId == id)).Stem = "changed";
            await Assert.ThrowsExactlyAsync<QuestionImmutableException>(() => db.SaveChangesAsync(),
                "an edit is a new revision, never an in-place change");
        }

        await SetStatusAsync(id, 1, QuestionStatus.PendingReview);
        await SetStatusAsync(id, 1, QuestionStatus.Rejected);
        await using var check = NewContext();
        (await check.QuestionRevisions.SingleAsync(item => item.QuestionId == id)).Status = QuestionStatus.Approved;
        await Assert.ThrowsExactlyAsync<QuestionImmutableException>(() => check.SaveChangesAsync(), "rejected is final");
    }

    [TestMethod]
    public async Task History_lists_every_revision_newest_first_with_its_own_content()
    {
        var id = await CreateDraftAsync("v1");
        await SetStatusAsync(id, 1, QuestionStatus.Approved);
        await EditAsync(id, 1, "v2", OtherMathTopic);
        await EditAsync(id, 2, "v3");

        await using var db = NewContext();
        var history = await new QuestionQueries(db).GetHistoryAsync(Teacher, id);

        Assert.IsNotNull(history);
        CollectionAssert.AreEqual(new[] { 3, 2, 1 }, history.Revisions.Select(item => item.Revision).ToArray());
        CollectionAssert.AreEqual(new[] { "v3", "v2", "v1" }, history.Revisions.Select(item => item.Stem).ToArray());
        Assert.AreEqual(3, history.LatestRevision);
        Assert.AreEqual(1, history.ApprovedRevision);
        Assert.AreEqual(3, history.Revisions.Single(item => item.IsLatest).Revision);
        Assert.AreEqual(1, history.Revisions.Single(item => item.IsApprovedInUse).Revision);
        Assert.AreEqual(OtherMathTopic, history.Revisions.Single(item => item.Revision == 2).TopicId);
        Assert.IsTrue(history.Revisions.All(item => item.Options.Count == 4 && item.CorrectOption == 'A' && item.TopicName.Length > 0));
        Assert.IsTrue(history.Revisions.All(item => item.Origin == QuestionOrigin.Manual));

        var one = await new QuestionQueries(db).GetRevisionAsync(Teacher, id, 2);
        Assert.IsNotNull(one);
        Assert.AreEqual("v2", one.Stem);
        Assert.IsNull(await new QuestionQueries(db).GetRevisionAsync(Teacher, id, 99));
    }

    [TestMethod]
    public async Task History_keeps_the_import_identifiers_of_each_revision()
    {
        var batch = Guid.NewGuid();
        var id = await CreateDraftAsync("imported", QuestionProvenance.Import(batch, 7));

        await using var db = NewContext();
        var history = await new QuestionQueries(db).GetHistoryAsync(Teacher, id);

        var first = history!.Revisions.Single();
        Assert.AreEqual(QuestionOrigin.Import, first.Origin);
        Assert.AreEqual(batch, first.ImportBatchId);
        Assert.AreEqual(7, first.ImportRowNumber);
    }

    [TestMethod]
    public async Task History_is_visible_only_to_users_who_may_read_the_question()
    {
        var id = await CreateDraftAsync("private");

        await using var db = NewContext();
        var queries = new QuestionQueries(db);
        Assert.IsNotNull(await queries.GetHistoryAsync(Teacher, id));
        Assert.IsNull(await queries.GetHistoryAsync(OtherTeacher, id), "another department's teacher");
        Assert.IsNull(await queries.GetHistoryAsync(Outsider, id), "a user with no role");
        Assert.IsNull(await queries.GetHistoryAsync(Head, id), "a head does not see drafts");
        Assert.IsNull(await queries.GetRevisionAsync(OtherTeacher, id, 1));
        Assert.IsNull(await queries.GetHistoryAsync(Teacher, Guid.NewGuid()), "an unknown question looks the same");

        await SetStatusAsync(id, 1, QuestionStatus.PendingReview);
        await using var after = NewContext();
        Assert.IsNotNull(await new QuestionQueries(after).GetHistoryAsync(Head, id), "a head reads what is submitted to them");
    }

    [TestMethod]
    public async Task History_costs_the_same_number_of_queries_however_many_revisions_there_are()
    {
        var short1 = await CreateDraftAsync("short");
        var long1 = await CreateDraftAsync("long");
        for (var revision = 1; revision <= 8; revision++)
            await EditAsync(long1, revision, $"long v{revision + 1}");

        var shortCount = await CountCommandsAsync(short1);
        var longCount = await CountCommandsAsync(long1);

        Assert.AreEqual(shortCount, longCount, "no N+1: the history of 9 revisions uses as many queries as the history of 1");
        Assert.IsLessThanOrEqualTo(8, longCount, $"history used {longCount} queries");

        static async Task<int> CountCommandsAsync(Guid id)
        {
            var counter = new CommandCounter();
            await using var db = NewContext(counter);
            Assert.IsNotNull(await new QuestionQueries(db).GetHistoryAsync(Teacher, id));
            return counter.Count;
        }
    }

    [TestMethod]
    public async Task Combined_filters_page_and_order_are_stable()
    {
        var ids = new List<Guid>();
        for (var index = 0; index < 5; index++)
            ids.Add(await CreateDraftAsync($"lọc-{index}"));
        await EditAsync(ids[0], 1, "lọc-0 sửa", OtherMathTopic);

        await using var db = NewContext();
        var queries = new QuestionQueries(db);
        var all = await queries.SearchAsync(QuestionSearchQuery.Create(
            Teacher, SchoolId, QuestionTaxonomySeed.MathId, MathTopic, Difficulty.Easy, QuestionStatus.Draft, "lọc", 1, 2));

        Assert.AreEqual(4, all.Total, "the edited question moved to another topic");
        Assert.HasCount(2, all.Items);
        var second = await queries.SearchAsync(QuestionSearchQuery.Create(
            Teacher, SchoolId, QuestionTaxonomySeed.MathId, MathTopic, Difficulty.Easy, QuestionStatus.Draft, "lọc", 2, 2));
        Assert.IsFalse(all.Items.Select(item => item.QuestionId).Intersect(second.Items.Select(item => item.QuestionId)).Any());
        Assert.IsTrue(all.Items.Concat(second.Items).Zip(all.Items.Concat(second.Items).Skip(1))
            .All(pair => pair.First.UpdatedAtUtc >= pair.Second.UpdatedAtUtc), "newest update first");
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
