using AiExamBank.Modules.Knowledge.Contracts;

namespace AiExamBank.Modules.Exams.Contracts;

/// <summary>M1 consumer contract. The live M4 adapter must enforce scope, token limits and cancellation.</summary>
public sealed record AiDraftBatchRequest(Guid ActorId, Guid SchoolId, Guid DepartmentId, Guid SubjectId,
    Guid TopicId, string Difficulty, int Count, int MaxOutputTokens, string CorrelationId,
    IReadOnlyList<RetrievedPassage> Passages);

public interface IAiDraftGenerator
{
    /// <summary>Returns untrusted JSON. No implementation is registered until the M4 live adapter is available.</summary>
    Task<string> GenerateAsync(AiDraftBatchRequest request, CancellationToken cancellationToken);
}
