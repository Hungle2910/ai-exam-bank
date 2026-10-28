namespace AiExamBank.Modules.Questions.Domain;

/// <summary>One stable, UI-safe validation failure. <paramref name="Field"/> names the input, e.g. <c>options[2]</c>.</summary>
public sealed record QuestionValidationError(string Code, string Field, string Message);

public static class QuestionErrorCodes
{
    public const string StemRequired = "QUESTION_STEM_REQUIRED";
    public const string StemTooLong = "QUESTION_STEM_TOO_LONG";
    public const string OptionsCountInvalid = "QUESTION_OPTIONS_COUNT_INVALID";
    public const string OptionRequired = "QUESTION_OPTION_REQUIRED";
    public const string OptionTooLong = "QUESTION_OPTION_TOO_LONG";
    public const string OptionsDuplicate = "QUESTION_OPTIONS_DUPLICATE";
    public const string CorrectOptionRequired = "QUESTION_CORRECT_OPTION_REQUIRED";
    public const string CorrectOptionInvalid = "QUESTION_CORRECT_OPTION_INVALID";
    public const string DifficultyInvalid = "QUESTION_DIFFICULTY_INVALID";
    public const string SubjectRequired = "QUESTION_SUBJECT_REQUIRED";
    public const string TopicRequired = "QUESTION_TOPIC_REQUIRED";
    public const string TopicSubjectMismatch = "QUESTION_TOPIC_SUBJECT_MISMATCH";
    public const string SourceTooLong = "QUESTION_SOURCE_TOO_LONG";
    public const string SubjectNotFound = "QUESTION_SUBJECT_NOT_FOUND";
    public const string TopicNotFound = "QUESTION_TOPIC_NOT_FOUND";
}
