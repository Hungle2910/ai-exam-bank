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

/// <summary>
/// Resolves the subject and topic names found in a CSV row or an editor form. It never creates taxonomy:
/// a name that is not in the school's catalog is reported as an error, not added.
/// </summary>
public interface IQuestionCatalog
{
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
