namespace AiExamBank.Modules.Questions.Domain;

public sealed class QuestionValidationException : Exception
{
    public QuestionValidationException(IReadOnlyList<QuestionValidationError> errors)
        : base($"Question content is invalid: {string.Join(", ", errors.Select(error => error.Code))}.")
    {
        Errors = errors;
    }

    public IReadOnlyList<QuestionValidationError> Errors { get; }
}
