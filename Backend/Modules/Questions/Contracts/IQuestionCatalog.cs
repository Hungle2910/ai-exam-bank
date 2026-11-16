using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>Subject and topic found by name, or the reasons they were not found.</summary>
public sealed record CatalogResolution(
    Guid? SubjectId,
    QuestionTopic? Topic,
    IReadOnlyList<QuestionValidationError> Errors)
{
    public bool IsResolved => Errors.Count == 0 && SubjectId is not null && Topic is not null;
}

public sealed record CatalogTopic(Guid Id, string Code, string Name);

/// <summary>
/// A subject the school offers, with the department that owns it and its topics. <paramref name="CanWrite"/> is
/// true when the actor is a teacher of that department; a department head sees the subject to filter by it but
/// cannot write to it.
/// </summary>
public sealed record CatalogSubject(
    Guid Id,
    string Code,
    string Name,
    Guid DepartmentId,
    bool CanWrite,
    IReadOnlyList<CatalogTopic> Topics);

/// <summary>
/// Resolves the subject and topic names found in a CSV row or an editor form. It never creates taxonomy:
/// a name that is not in the school's catalog is reported as an error, not added.
/// </summary>
public interface IQuestionCatalog
{
    /// <summary>
    /// The subjects of the actor's departments in this school, with their topics, for filters and form dropdowns.
    /// Only subjects with <see cref="CatalogSubject.CanWrite"/> may be written to.
    /// </summary>
    Task<IReadOnlyList<CatalogSubject>> ListAsync(
        Guid actorUserId, Guid schoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Matches names after trimming, Unicode NFC normalization and case folding, among the subjects the school
    /// offers and the actor may write to. Unknown names produce <c>QUESTION_SUBJECT_NOT_FOUND</c> or
    /// <c>QUESTION_TOPIC_NOT_FOUND</c>.
    /// </summary>
    Task<CatalogResolution> ResolveAsync(
        Guid actorUserId,
        Guid schoolId,
        string? subjectName,
        string? topicName,
        CancellationToken cancellationToken = default);
}
