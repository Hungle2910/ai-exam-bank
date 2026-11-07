using System.Text.Json;
using AiExamBank.Modules.Exams.Application;
using AiExamBank.Modules.Exams.Contracts;
using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Knowledge.Contracts;

namespace AiExamBank.Modules.Exams.Tests;

[TestClass]
public sealed class AiDraftOrchestratorTests
{
    private static readonly Guid Subject = Guid.NewGuid(), Topic = Guid.NewGuid();
    private static BlueprintMatrix Matrix(int count = 2) => new("Matrix", Subject, 45, count, count, [new(Topic, "EASY", count, 1)]);
    private static SlotDeficit Gap(int count = 2) => new(0, Topic, "EASY", count, count);
    private static Task<IReadOnlyList<DraftSlotResult>> Run(AiDraftOrchestrator service, IReadOnlyList<SlotDeficit> gaps, int count = 2, CancellationToken ct = default) =>
        service.GenerateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Matrix(count), gaps, ct);

    [TestMethod]
    public async Task Full_approved_selection_calls_neither_retrieval_nor_generation()
    {
        var provider = new Provider(); var retrieval = new Retrieval();
        Assert.IsEmpty(await Run(new(retrieval, provider), []));
        Assert.AreEqual(0, provider.Calls); Assert.AreEqual(0, retrieval.Calls);
    }

    [TestMethod]
    public async Task Empty_retrieval_never_calls_generator()
    {
        var provider = new Provider();
        var result = await Run(new(new Retrieval { Empty = true }, provider), [Gap()]);
        Assert.AreEqual("NO_RETRIEVAL_RESULTS", result[0].ErrorCode);
        Assert.AreEqual(0, provider.Calls);
    }

    [TestMethod]
    public async Task Batches_are_bounded_and_citations_come_from_retrieval()
    {
        var provider = new Provider();
        var result = await Run(new(new Retrieval(), provider, new(BatchSize: 1)), [Gap()]);
        Assert.AreEqual(2, provider.Calls);
        Assert.IsTrue(provider.Requests.All(r => r.Count == 1 && r.MaxOutputTokens == 4096));
        Assert.AreEqual("REVIEW_REQUIRED", result[0].Status);
        Assert.HasCount(2, result[0].Drafts);
        Assert.IsTrue(result[0].Drafts.All(d => d.Citation.SourceId == "trusted-source"));
    }

    [TestMethod]
    [DataRow("invalid-json")]
    [DataRow("extra")]
    [DataRow("missing")]
    [DataRow("wrong-topic")]
    [DataRow("approved")]
    [DataRow("unknown-source")]
    public async Task Untrusted_invalid_output_is_rejected_without_partial_drafts(string mode)
    {
        var result = await Run(new(new Retrieval(), new Provider { Mode = mode }), [Gap()]);
        Assert.AreEqual("DRAFT_SCHEMA_OR_COUNT_INVALID", result[0].ErrorCode);
        Assert.IsEmpty(result[0].Drafts);
    }

    [TestMethod]
    public async Task Request_limit_is_checked_before_any_provider_call()
    {
        var provider = new Provider(); var retrieval = new Retrieval();
        var result = await Run(new(retrieval, provider, new(MaxQuestions: 1)), [Gap()]);
        Assert.AreEqual("REQUEST_LIMIT_EXCEEDED", result[0].ErrorCode);
        Assert.AreEqual(0, retrieval.Calls); Assert.AreEqual(0, provider.Calls);
    }

    [TestMethod]
    public async Task Timeout_returns_safe_slot_failure()
    {
        var result = await Run(new(new Retrieval(), new Provider { Mode = "timeout" }, new(BatchTimeoutSeconds: 1)), [Gap()]);
        Assert.AreEqual("GENERATION_TIMEOUT", result[0].ErrorCode);
        Assert.IsEmpty(result[0].Drafts);
    }

    [TestMethod]
    public async Task Caller_cancellation_propagates_to_worker()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Run(new(new Retrieval(), new Provider()), [Gap()], ct: cts.Token));
    }

    private sealed class Retrieval : IKnowledgeRetriever
    {
        public bool Empty { get; init; }
        public int Calls { get; private set; }
        public Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<RetrievedPassage>>(Empty ? [] : [new(SourceCitation.Create("trusted-source", "page-1"), "Trusted passage", 1)]);
        }
    }

    private sealed class Provider : IAiDraftGenerator
    {
        public string Mode { get; init; } = "valid";
        public int Calls { get; private set; }
        public List<AiDraftBatchRequest> Requests { get; } = [];
        public async Task<string> GenerateAsync(AiDraftBatchRequest request, CancellationToken ct)
        {
            Calls++; Requests.Add(request);
            if (Mode == "timeout") await Task.Delay(Timeout.Infinite, ct);
            if (Mode == "invalid-json") return "not JSON";
            var count = request.Count + (Mode == "extra" ? 1 : Mode == "missing" ? -1 : 0);
            var questions = Enumerable.Range(0, count).Select(i => new Dictionary<string, object?>
            {
                ["stem"] = $"Question {Calls}-{i}", ["options"] = new[] { "One", "Two", "Three", "Four" },
                ["correctOption"] = "A", ["difficulty"] = request.Difficulty,
                ["subjectId"] = request.SubjectId, ["topicId"] = Mode == "wrong-topic" ? Guid.NewGuid() : request.TopicId,
                ["sourceIndex"] = Mode == "unknown-source" ? 99 : 0
            }).ToArray();
            if (Mode == "approved") questions[0]["status"] = "APPROVED";
            return JsonSerializer.Serialize(new { questions });
        }
    }
}
