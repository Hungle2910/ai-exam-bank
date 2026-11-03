using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>One approved revision with the content an exam snapshot pins.</summary>
public sealed record ApprovedQuestionRead(
    QuestionRevisionRef Revision,
    Guid SchoolId,
    Guid DepartmentId,
    Guid SubjectId,
    Guid TopicId,
    Difficulty Difficulty,
    string Stem,
    IReadOnlyList<string> Options,
    char CorrectOption);

/// <summary>Approved questions for one slot. Results are ordered by question ID so a seeded selection can be repeated.</summary>
public sealed record ApprovedQuestionQuery
{
    public const int MaxLimit = 500;

    private ApprovedQuestionQuery(
        Guid schoolId, Guid topicId, Difficulty difficulty, int limit, IReadOnlyCollection<Guid> excludedQuestionIds)
    {
        SchoolId = schoolId;
        TopicId = topicId;
        Difficulty = difficulty;
        Limit = limit;
        ExcludedQuestionIds = excludedQuestionIds;
    }

    public Guid SchoolId { get; }
    public Guid TopicId { get; }
    public Difficulty Difficulty { get; }
    public int Limit { get; }
    public IReadOnlyCollection<Guid> ExcludedQuestionIds { get; }

    public static ApprovedQuestionQuery Create(
        Guid schoolId, Guid topicId, Difficulty difficulty, int limit, IReadOnlyCollection<Guid>? excludedQuestionIds = null)
    {
        if (schoolId == Guid.Empty) throw new ArgumentException("A school ID is required.", nameof(schoolId));
        if (topicId == Guid.Empty) throw new ArgumentException("A topic ID is required.", nameof(topicId));
        if (!Enum.IsDefined(difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty), "Unknown difficulty.");
        if (limit < 1 || limit > MaxLimit)
            throw new ArgumentOutOfRangeException(nameof(limit), $"Limit must be between 1 and {MaxLimit}.");
        return new ApprovedQuestionQuery(schoolId, topicId, difficulty, limit, excludedQuestionIds ?? []);
    }
}

public sealed record ApprovedQuestionCount(Guid TopicId, Difficulty Difficulty, int Count);

/// <summary>
/// Read side for exam selection. It returns only revisions whose status is <see cref="QuestionStatus.Approved"/>;
/// draft, review-required and rejected content is never visible through it. The caller authorizes the actor
/// against the blueprint; whether a teacher may read the shared bank is a pending M3 permission decision.
/// </summary>
public interface IApprovedQuestionReader
{
    Task<IReadOnlyList<ApprovedQuestionRead>> FindApprovedAsync(
        ApprovedQuestionQuery query, CancellationToken cancellationToken = default);

    /// <summary>Re-reads one pinned revision, for revalidation before an exam is finalized. Null when it is not approved.</summary>
    Task<ApprovedQuestionRead?> GetApprovedAsync(
        QuestionRevisionRef revision, CancellationToken cancellationToken = default);

    /// <summary>Counts approved questions per topic and difficulty for a subject; cells with none are omitted.</summary>
    Task<IReadOnlyList<ApprovedQuestionCount>> CountApprovedAsync(
        Guid schoolId, Guid subjectId, CancellationToken cancellationToken = default);
}
