using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Questions.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

/// <summary>One question as shown in the bank list: the latest revision, without the answer key.</summary>
public sealed record QuestionListItem(
    Guid QuestionId,
    int Revision,
    string Stem,
    Guid SubjectId,
    string SubjectName,
    Guid TopicId,
    string TopicName,
    Difficulty Difficulty,
    QuestionStatus Status,
    Guid OwnerUserId,
    DateTimeOffset UpdatedAtUtc);

public sealed record QuestionRevisionSummary(
    int Revision,
    QuestionStatus Status,
    QuestionOrigin Origin,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc);

/// <summary>The latest revision with its full content, plus the summary of every revision.</summary>
public sealed record QuestionDetail(
    Guid QuestionId,
    int Revision,
    QuestionStatus Status,
    QuestionOrigin Origin,
    string Stem,
    IReadOnlyList<string> Options,
    char CorrectOption,
    Guid SubjectId,
    string SubjectName,
    Guid TopicId,
    string TopicName,
    Difficulty Difficulty,
    string? SourceText,
    Guid OwnerUserId,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<QuestionRevisionSummary> Revisions,
    int? ApprovedRevision = null);

/// <summary>
/// One immutable revision with its full content, as the history view shows and compares it.
/// <see cref="IsLatest"/> marks the revision the question currently points at; <see cref="IsApprovedInUse"/> marks
/// the newest approved revision, the one exam selection uses while a newer draft is being reviewed.
/// </summary>
public sealed record QuestionRevisionDetail(
    int Revision,
    QuestionStatus Status,
    QuestionOrigin Origin,
    string Stem,
    IReadOnlyList<string> Options,
    char CorrectOption,
    Guid TopicId,
    string TopicName,
    Difficulty Difficulty,
    string? SourceText,
    Guid? ImportBatchId,
    int? ImportRowNumber,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc,
    bool IsLatest,
    bool IsApprovedInUse);

/// <summary>Every revision of a question, newest first. <see cref="ApprovedRevision"/> is null until one is approved.</summary>
public sealed record QuestionHistory(
    Guid QuestionId,
    Guid SubjectId,
    string SubjectName,
    int LatestRevision,
    int? ApprovedRevision,
    IReadOnlyList<QuestionRevisionDetail> Revisions);

/// <summary>
/// Filters for the bank list. The school comes from the authenticated actor, never from the request,
/// and the status filter applies to the latest revision of each question.
/// </summary>
public sealed record QuestionSearchQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 10_000;
    public const int MaxTextLength = 200;

    private QuestionSearchQuery(
        Guid actorUserId, Guid schoolId, Guid? subjectId, Guid? topicId, Difficulty? difficulty,
        QuestionStatus? status, string? text, int page, int pageSize)
    {
        ActorUserId = actorUserId;
        SchoolId = schoolId;
        SubjectId = subjectId;
        TopicId = topicId;
        Difficulty = difficulty;
        Status = status;
        Text = text;
        Page = page;
        PageSize = pageSize;
    }

    public Guid ActorUserId { get; }
    public Guid SchoolId { get; }
    public Guid? SubjectId { get; }
    public Guid? TopicId { get; }
    public Difficulty? Difficulty { get; }
    public QuestionStatus? Status { get; }
    public string? Text { get; }
    public int Page { get; }
    public int PageSize { get; }

    public static QuestionSearchQuery Create(
        Guid actorUserId, Guid schoolId, Guid? subjectId = null, Guid? topicId = null,
        Difficulty? difficulty = null, QuestionStatus? status = null, string? text = null,
        int page = 1, int pageSize = DefaultPageSize)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("An actor ID is required.", nameof(actorUserId));
        if (schoolId == Guid.Empty) throw new ArgumentException("A school ID is required.", nameof(schoolId));
        if (difficulty is { } d && !Enum.IsDefined(d)) throw new ArgumentOutOfRangeException(nameof(difficulty), "Unknown difficulty.");
        if (status is { } s && !Enum.IsDefined(s)) throw new ArgumentOutOfRangeException(nameof(status), "Unknown status.");
        if (page < 1 || page > MaxPage)
            throw new ArgumentOutOfRangeException(nameof(page), $"Page must be between 1 and {MaxPage}.");
        if (pageSize < 1 || pageSize > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"Page size must be between 1 and {MaxPageSize}.");

        var trimmed = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (trimmed is { Length: > MaxTextLength })
            throw new ArgumentOutOfRangeException(nameof(text), $"Search text must be at most {MaxTextLength} characters.");

        return new QuestionSearchQuery(actorUserId, schoolId, subjectId, topicId, difficulty, status, trimmed, page, pageSize);
    }
}

/// <summary>Read side for the bank list and editor. Results are limited to what the actor may read.</summary>
public interface IQuestionQueries
{
    Task<PagedResult<QuestionListItem>> SearchAsync(QuestionSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>Null when the question does not exist or the actor may not read it, so existence is not revealed.</summary>
    Task<QuestionDetail?> GetAsync(Guid actorUserId, Guid questionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every revision with its content, newest first. Null when the question does not exist or the actor may not
    /// read it. Answer keys of old revisions are as sensitive as the latest one, so the same read check applies.
    /// </summary>
    Task<QuestionHistory?> GetHistoryAsync(Guid actorUserId, Guid questionId, CancellationToken cancellationToken = default);

    /// <summary>One revision with its content. Null when the question or revision is missing, or the actor may not read the question.</summary>
    Task<QuestionRevisionDetail?> GetRevisionAsync(
        Guid actorUserId, Guid questionId, int revision, CancellationToken cancellationToken = default);
}
