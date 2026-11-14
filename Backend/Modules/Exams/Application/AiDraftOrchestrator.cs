using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiExamBank.Modules.Exams.Contracts;
using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Knowledge.Contracts;
using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Exams.Application;

public sealed record DraftGenerationLimits(int BatchSize = 5, int MaxQuestions = 50, int MaxSlots = 10,
    int MaxOutputTokens = 4096, int MaxResponseBytes = 65536, int BatchTimeoutSeconds = 30, int RequestTimeoutSeconds = 120);
public sealed record ValidatedAiDraft(McqContent Content, SourceCitation Citation);
public sealed record DraftSlotResult(int SlotIndex, string CorrelationId, string Status, string? ErrorCode,
    IReadOnlyList<ValidatedAiDraft> Drafts);

/// <summary>Authorizes no actors itself: input must come from an authorized persisted exam and its server-computed gaps.</summary>
public sealed class AiDraftOrchestrator
{
    private readonly IKnowledgeRetriever _retriever;
    private readonly IAiDraftGenerator _generator;
    private readonly DraftGenerationLimits _limits;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 8
    };

    public AiDraftOrchestrator(IKnowledgeRetriever retriever, IAiDraftGenerator generator, DraftGenerationLimits? limits = null)
    {
        _retriever = retriever;
        _generator = generator;
        _limits = limits ?? new();
        if (_limits.BatchSize is < 1 or > 10 || _limits.MaxQuestions is < 1 or > 100 || _limits.MaxSlots is < 1 or > 200 ||
            _limits.MaxOutputTokens is < 1 or > 32768 || _limits.MaxResponseBytes is < 1 or > 1048576 ||
            _limits.BatchTimeoutSeconds is < 1 or > 120 || _limits.RequestTimeoutSeconds is < 1 or > 600)
            throw new ArgumentOutOfRangeException(nameof(limits));
    }

    public async Task<IReadOnlyList<DraftSlotResult>> GenerateAsync(Guid requestId, Guid actorId, Guid schoolId,
        Guid departmentId, BlueprintMatrix matrix, IReadOnlyList<SlotDeficit> gaps, CancellationToken ct = default)
    {
        if (requestId == Guid.Empty || actorId == Guid.Empty || schoolId == Guid.Empty || departmentId == Guid.Empty)
            throw new ArgumentException("Persisted request and actor scope are required.");
        if (matrix.Validate().Count != 0) throw new ArgumentException("Invalid matrix.", nameof(matrix));
        ArgumentNullException.ThrowIfNull(gaps);
        if (gaps.Select(g => g.SlotIndex).Distinct().Count() != gaps.Count || gaps.Any(g =>
            g.SlotIndex < 0 || g.SlotIndex >= matrix.Slots.Count || g.MissingCount < 1 ||
            g.MissingCount > matrix.Slots[g.SlotIndex].QuestionCount || g.TopicId != matrix.Slots[g.SlotIndex].TopicId ||
            g.Difficulty != matrix.Slots[g.SlotIndex].Difficulty ||
            g.MissingPoints != g.MissingCount * matrix.Slots[g.SlotIndex].PointsPerQuestion))
            throw new ArgumentException("Gaps must match the persisted matrix.", nameof(gaps));
        if (gaps.Count == 0) return [];
        if (gaps.Count > _limits.MaxSlots || gaps.Sum(g => g.MissingCount) > _limits.MaxQuestions)
            return gaps.Select(g => Failed(g, requestId, "REQUEST_LIMIT_EXCEEDED")).ToArray();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(_limits.RequestTimeoutSeconds));
        var results = new List<DraftSlotResult>();
        foreach (var gap in gaps.OrderBy(g => g.SlotIndex))
        {
            ct.ThrowIfCancellationRequested();
            var correlation = Correlation(requestId, gap.SlotIndex);
            var drafts = new List<ValidatedAiDraft>();
            try
            {
                deadline.Token.ThrowIfCancellationRequested();
                using var retrievalTimeout = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                retrievalTimeout.CancelAfter(TimeSpan.FromSeconds(_limits.BatchTimeoutSeconds));
                var passages = await _retriever.RetrieveAsync(RetrievalQuery.Create(actorId, schoolId, matrix.SubjectId,
                    gap.TopicId, $"Subject {matrix.SubjectId:D}, topic {gap.TopicId:D}, difficulty {gap.Difficulty}", 5), retrievalTimeout.Token)
                    .WaitAsync(retrievalTimeout.Token);
                if (passages.Count == 0) { results.Add(Failed(gap, requestId, "NO_RETRIEVAL_RESULTS")); continue; }
                if (passages.Count > 5 || passages.Any(p => p is null || p.Citation is null ||
                        p.Citation.SourceId.Length > 1000 || p.Citation.Locator.Length > 1000 || string.IsNullOrWhiteSpace(p.Text)) ||
                    passages.Sum(p => (long)p.Text.Length) > 32000)
                { results.Add(Failed(gap, requestId, "RETRIEVAL_LIMIT_OR_SCHEMA_INVALID")); continue; }
                for (var offset = 0; offset < gap.MissingCount; offset += _limits.BatchSize)
                {
                    using var batchTimeout = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                    batchTimeout.CancelAfter(TimeSpan.FromSeconds(_limits.BatchTimeoutSeconds));
                    var count = Math.Min(_limits.BatchSize, gap.MissingCount - offset);
                    var raw = await _generator.GenerateAsync(new(actorId, schoolId, departmentId, matrix.SubjectId,
                        gap.TopicId, gap.Difficulty, count, _limits.MaxOutputTokens, $"{correlation}-{offset}", passages), batchTimeout.Token)
                        .WaitAsync(batchTimeout.Token);
                    batchTimeout.Token.ThrowIfCancellationRequested();
                    var batch = Parse(raw, matrix.SubjectId, gap, count, passages);
                    if (batch is null) { drafts.Clear(); break; }
                    drafts.AddRange(batch);
                }
                if (drafts.Count != gap.MissingCount || drafts.Select(d => d.Content.Stem).Distinct(StringComparer.OrdinalIgnoreCase).Count() != drafts.Count)
                    results.Add(Failed(gap, requestId, "DRAFT_SCHEMA_OR_COUNT_INVALID"));
                else results.Add(new(gap.SlotIndex, correlation, "REVIEW_REQUIRED", null, drafts.ToArray()));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            { results.Add(Failed(gap, requestId, "GENERATION_TIMEOUT")); }
            // Provider-specific exceptions are intentionally propagated to the M5 adapter/handler for classification.
        }
        return results;
    }

    private IReadOnlyList<ValidatedAiDraft>? Parse(string raw, Guid subjectId, SlotDeficit gap, int count, IReadOnlyList<RetrievedPassage> passages)
    {
        if (raw is null || raw.Length > _limits.MaxResponseBytes || Encoding.UTF8.GetByteCount(raw) > _limits.MaxResponseBytes) return null;
        try
        {
            var response = JsonSerializer.Deserialize<ProviderBatch>(raw, JsonOptions);
            if (response?.Questions is null || response.Questions.Count != count) return null;
            var drafts = new List<ValidatedAiDraft>();
            foreach (var item in response.Questions)
            {
                if (item is null || item.SourceIndex is null || item.SourceIndex < 0 || item.SourceIndex >= passages.Count ||
                    item.SubjectId != subjectId || item.TopicId != gap.TopicId || item.Difficulty != gap.Difficulty) return null;
                var citation = passages[item.SourceIndex.Value].Citation;
                var validated = McqContent.Validate(new(item.Stem, item.Options, item.CorrectOption, item.Difficulty,
                    subjectId, QuestionTopic.Create(gap.TopicId, subjectId), $"{citation.SourceId} | {citation.Locator}"));
                if (validated.Content is null) return null;
                drafts.Add(new(validated.Content, citation));
            }
            return drafts;
        }
        catch (JsonException) { return null; }
    }

    private static string Correlation(Guid request, int slot) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(FormattableString.Invariant($"{request:D}:{slot}"))))[..24];
    private static DraftSlotResult Failed(SlotDeficit gap, Guid request, string code) =>
        new(gap.SlotIndex, Correlation(request, gap.SlotIndex), "FAILED", code, []);
    private sealed record ProviderBatch(IReadOnlyList<ProviderQuestion>? Questions);
    private sealed record ProviderQuestion(string? Stem, IReadOnlyList<string?>? Options, string? CorrectOption,
        string? Difficulty, Guid SubjectId, Guid TopicId, int? SourceIndex);
}
