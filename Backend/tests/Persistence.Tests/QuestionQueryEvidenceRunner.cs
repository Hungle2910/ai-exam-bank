using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using AiExamBank.Persistence.Questions;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Tests;

/// <summary>
/// Not a regression test: it runs the real application queries against a database seeded by
/// <c>docs/M4-03/query_evidence_seed.sql</c> so their cost can be read from SQL Server
/// (see <c>docs/M4-03/query_evidence.sql</c>). It is skipped unless <c>AIEXAMBANK_PERF_CONNECTION</c> is set.
/// </summary>
[TestClass]
public sealed class QuestionQueryEvidenceRunner
{
    private static readonly Guid School = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Teacher = Guid.Parse("44444444-4444-4444-4444-000000000001");
    private static readonly Guid Head = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Math = QuestionTaxonomySeed.MathId;


    [TestMethod]
    public async Task Run_every_hot_query_twice()
    {
        var connection = Environment.GetEnvironmentVariable("AIEXAMBANK_PERF_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Inconclusive("Set AIEXAMBANK_PERF_CONNECTION to run the query evidence.");

        for (var pass = 0; pass < 2; pass++)
        {
            await using var db = new AiExamBankDbContext(
                new DbContextOptionsBuilder<AiExamBankDbContext>().UseSqlServer(connection, sql => sql.CommandTimeout(600)).Options);
            var queries = new QuestionQueries(db);
            // Same pick as the seed script: SQL Server orders uniqueidentifiers its own way, so ask it.
            var Topic = await db.Topics.AsNoTracking().Where(item => item.SubjectId == Math).OrderBy(item => item.Id).Select(item => item.Id).FirstAsync();

            // Bank list as a teacher: nothing, each filter, all filters together, a deep page, and text search.
            await Timed("SearchAsync(Query(Teacher))", () => queries.SearchAsync(Query(Teacher)));
            await Timed("SearchAsync(Query(Teacher, status: QuestionStatus.Draft))", () => queries.SearchAsync(Query(Teacher, status: QuestionStatus.Draft)));
            await Timed("SearchAsync(Query(Teacher, topic: Topic, difficulty: Difficulty.Easy))", () => queries.SearchAsync(Query(Teacher, topic: Topic, difficulty: Difficulty.Easy)));
            await Timed("SearchAsync(Query(Teacher, topic: Topic, difficulty: Difficulty.Easy, status: QuestionStatus.Approved))", () => queries.SearchAsync(Query(Teacher, topic: Topic, difficulty: Difficulty.Easy, status: QuestionStatus.Approved)));
            await Timed("SearchAsync(Query(Teacher, page: 50))", () => queries.SearchAsync(Query(Teacher, page: 50)));
            await Timed("SearchAsync(Query(Teacher, text: so 4001))", () => queries.SearchAsync(Query(Teacher, text: "số 4001")));

            // Bank list as a department head: sees only what was submitted for review.
            await Timed("SearchAsync(Query(Head))", () => queries.SearchAsync(Query(Head)));
            await Timed("SearchAsync(Query(Head, topic: Topic, difficulty: Difficulty.Hard))", () => queries.SearchAsync(Query(Head, topic: Topic, difficulty: Difficulty.Hard)));

            // Detail and history of one question.
            var owned = await db.Questions.AsNoTracking().Where(item => item.OwnerUserId == Teacher && item.LatestRevision == 4)
                .Select(item => item.Id).FirstAsync();
            await Timed("GetAsync(Teacher, owned)", () => queries.GetAsync(Teacher, owned));
            await Timed("GetHistoryAsync(Teacher, owned)", () => queries.GetHistoryAsync(Teacher, owned));
            await Timed("GetRevisionAsync(Teacher, owned, 2)", () => queries.GetRevisionAsync(Teacher, owned, 2));

            // Exam selection side.
            var reader = new ApprovedQuestionReader(db);
            await Timed("FindApprovedAsync(ApprovedQuestionQuery.Create(School, Topic, Difficulty.Easy, 50))", () => reader.FindApprovedAsync(ApprovedQuestionQuery.Create(School, Topic, Difficulty.Easy, 50)));
            await Timed("CountApprovedAsync(School, Math)", () => reader.CountApprovedAsync(School, Math));
        }
    }

    private static async Task Timed<T>(string label, Func<Task<T>> call)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await call();
        Console.WriteLine($"{clock.ElapsedMilliseconds,8} ms  {label}");
    }

    private static QuestionSearchQuery Query(
        Guid actor, Guid? topic = null, Difficulty? difficulty = null, QuestionStatus? status = null,
        string? text = null, int page = 1) =>
        QuestionSearchQuery.Create(actor, School, Math, topic, difficulty, status, text, page, 20);
}
