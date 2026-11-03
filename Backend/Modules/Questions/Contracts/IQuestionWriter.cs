namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>
/// The only write path into the question bank for the API, import and AI draft generation.
/// Implementations join the caller's current transaction, so an import commit and its question writes
/// succeed or fail together. They never approve or publish content.
/// </summary>
public interface IQuestionWriter
{
    /// <exception cref="Domain.QuestionValidationException">The content breaks a question rule.</exception>
    /// <exception cref="QuestionAccessDeniedException">The actor may not write to this school and subject.</exception>
    Task<QuestionWriteResult> CreateDraftAsync(CreateQuestionCommand command, CancellationToken cancellationToken = default);

    /// <exception cref="Domain.QuestionValidationException">The content breaks a question rule.</exception>
    /// <exception cref="QuestionNotFoundException">The question does not exist.</exception>
    /// <exception cref="QuestionAccessDeniedException">The actor may not edit this question.</exception>
    /// <exception cref="QuestionConflictException">The latest revision is no longer the base revision.</exception>
    Task<QuestionWriteResult> CreateRevisionAsync(CreateRevisionCommand command, CancellationToken cancellationToken = default);
}
