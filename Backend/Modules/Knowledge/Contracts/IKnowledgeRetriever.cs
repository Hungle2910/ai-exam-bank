namespace AiExamBank.Modules.Knowledge.Contracts;

/// <summary>
/// A retrieval request scoped to one school, subject and topic. The actor is required: retrieval is only reached
/// through authenticated use cases, and a source reference is never treated as permission to read the source.
/// </summary>
public sealed record RetrievalQuery
{
    public const int MaxPassagesLimit = 20;

    private RetrievalQuery(Guid actorUserId, Guid schoolId, Guid subjectId, Guid topicId, string queryText, int maxPassages)
    {
        ActorUserId = actorUserId;
        SchoolId = schoolId;
        SubjectId = subjectId;
        TopicId = topicId;
        QueryText = queryText;
        MaxPassages = maxPassages;
    }

    public Guid ActorUserId { get; }
    public Guid SchoolId { get; }
    public Guid SubjectId { get; }
    public Guid TopicId { get; }
    public string QueryText { get; }
    public int MaxPassages { get; }

    public static RetrievalQuery Create(
        Guid actorUserId, Guid schoolId, Guid subjectId, Guid topicId, string queryText, int maxPassages)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("An actor ID is required.", nameof(actorUserId));
        if (schoolId == Guid.Empty) throw new ArgumentException("A school ID is required.", nameof(schoolId));
        if (subjectId == Guid.Empty) throw new ArgumentException("A subject ID is required.", nameof(subjectId));
        if (topicId == Guid.Empty) throw new ArgumentException("A topic ID is required.", nameof(topicId));
        if (string.IsNullOrWhiteSpace(queryText)) throw new ArgumentException("Query text is required.", nameof(queryText));
        if (maxPassages < 1 || maxPassages > MaxPassagesLimit)
            throw new ArgumentOutOfRangeException(nameof(maxPassages), $"Passages must be between 1 and {MaxPassagesLimit}.");
        return new RetrievalQuery(actorUserId, schoolId, subjectId, topicId, queryText.Trim(), maxPassages);
    }
}

/// <summary>One retrieved passage. It always carries a citation, so a draft built from it stays traceable.</summary>
public sealed record RetrievedPassage(SourceCitation Citation, string Text, double Score);

public interface IKnowledgeRetriever
{
    /// <summary>
    /// Returns the best passages from sources the school may use, best first. An empty list means nothing
    /// usable was found; the caller must not generate an uncited draft from it.
    /// </summary>
    Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(
        RetrievalQuery query, CancellationToken cancellationToken = default);
}
