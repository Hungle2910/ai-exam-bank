using System.Text;

namespace AiExamBank.Modules.Questions.Domain;

/// <summary>Untrusted question content as received from the API, the editor or an import row.</summary>
public sealed record McqInput(
    string? Stem,
    IReadOnlyList<string?>? Options,
    string? CorrectOption,
    string? Difficulty,
    Guid SubjectId,
    QuestionTopic? Topic,
    string? SourceText = null);

public sealed record McqValidationResult(McqContent? Content, IReadOnlyList<QuestionValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Validated single-answer multiple-choice content with normalized text.
/// Validation reports every failure so an import preview can show all errors for a row.
/// </summary>
public sealed record McqContent
{
    private McqContent(
        string stem,
        IReadOnlyList<string> options,
        char correctOption,
        Difficulty difficulty,
        Guid subjectId,
        Guid topicId,
        string? sourceText)
    {
        Stem = stem;
        Options = options;
        CorrectOption = correctOption;
        Difficulty = difficulty;
        SubjectId = subjectId;
        TopicId = topicId;
        SourceText = sourceText;
    }

    public string Stem { get; }
    public IReadOnlyList<string> Options { get; }
    public char CorrectOption { get; }
    public Difficulty Difficulty { get; }
    public Guid SubjectId { get; }
    public Guid TopicId { get; }
    public string? SourceText { get; }
    public int CorrectOptionIndex => QuestionRules.OptionLabels.IndexOf(CorrectOption);

    public static McqContent Create(McqInput input)
    {
        var result = Validate(input);
        return result.Content ?? throw new QuestionValidationException(result.Errors);
    }

    public static McqValidationResult Validate(McqInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new List<QuestionValidationError>();

        var stem = Normalize(input.Stem);
        if (stem.Length == 0)
            errors.Add(new(QuestionErrorCodes.StemRequired, "stem", "The question text is required."));
        else if (stem.Length > QuestionRules.MaxStemLength)
            errors.Add(new(QuestionErrorCodes.StemTooLong, "stem",
                $"The question text must be at most {QuestionRules.MaxStemLength} characters."));

        var options = ValidateOptions(input.Options, errors);
        var correctOption = ValidateCorrectOption(input.CorrectOption, errors);
        var difficulty = ValidateDifficulty(input.Difficulty, errors);
        ValidateTaxonomy(input.SubjectId, input.Topic, errors);

        var source = Normalize(input.SourceText);
        if (source.Length > QuestionRules.MaxSourceLength)
            errors.Add(new(QuestionErrorCodes.SourceTooLong, "source",
                $"The source must be at most {QuestionRules.MaxSourceLength} characters."));

        if (errors.Count > 0) return new McqValidationResult(null, errors);

        return new McqValidationResult(
            new McqContent(stem, options, correctOption!.Value, difficulty!.Value,
                input.SubjectId, input.Topic!.TopicId, source.Length == 0 ? null : source),
            errors);
    }

    private static string[] ValidateOptions(IReadOnlyList<string?>? raw, List<QuestionValidationError> errors)
    {
        if (raw is null || raw.Count != QuestionRules.OptionCount)
        {
            errors.Add(new(QuestionErrorCodes.OptionsCountInvalid, "options",
                $"Exactly {QuestionRules.OptionCount} options are required."));
            return [];
        }

        var options = new string[raw.Count];
        for (var index = 0; index < raw.Count; index++)
        {
            options[index] = Normalize(raw[index]);
            var field = $"options[{index}]";
            if (options[index].Length == 0)
                errors.Add(new(QuestionErrorCodes.OptionRequired, field,
                    $"Option {QuestionRules.OptionLabels[index]} is required."));
            else if (options[index].Length > QuestionRules.MaxOptionLength)
                errors.Add(new(QuestionErrorCodes.OptionTooLong, field,
                    $"Option {QuestionRules.OptionLabels[index]} must be at most {QuestionRules.MaxOptionLength} characters."));
        }

        for (var index = 1; index < options.Length; index++)
        {
            if (options[index].Length == 0) continue;
            for (var earlier = 0; earlier < index; earlier++)
            {
                if (!string.Equals(options[earlier], options[index], StringComparison.OrdinalIgnoreCase)) continue;
                errors.Add(new(QuestionErrorCodes.OptionsDuplicate, $"options[{index}]",
                    $"Option {QuestionRules.OptionLabels[index]} repeats option {QuestionRules.OptionLabels[earlier]}."));
                break;
            }
        }

        return options;
    }

    private static char? ValidateCorrectOption(string? raw, List<QuestionValidationError> errors)
    {
        var value = Normalize(raw);
        if (value.Length == 0)
        {
            errors.Add(new(QuestionErrorCodes.CorrectOptionRequired, "correctOption", "The correct option is required."));
            return null;
        }

        if (value.Length == 1 && QuestionRules.OptionLabels.Contains(char.ToUpperInvariant(value[0])))
            return char.ToUpperInvariant(value[0]);

        errors.Add(new(QuestionErrorCodes.CorrectOptionInvalid, "correctOption",
            $"The correct option must be one of {string.Join(", ", QuestionRules.OptionLabels.ToCharArray())}."));
        return null;
    }

    private static Difficulty? ValidateDifficulty(string? raw, List<QuestionValidationError> errors)
    {
        Difficulty? parsed = Normalize(raw).ToUpperInvariant() switch
        {
            "EASY" => Domain.Difficulty.Easy,
            "MEDIUM" => Domain.Difficulty.Medium,
            "HARD" => Domain.Difficulty.Hard,
            _ => null
        };
        if (parsed is null)
            errors.Add(new(QuestionErrorCodes.DifficultyInvalid, "difficulty", "The difficulty must be easy, medium or hard."));
        return parsed;
    }

    private static void ValidateTaxonomy(Guid subjectId, QuestionTopic? topic, List<QuestionValidationError> errors)
    {
        if (subjectId == Guid.Empty)
            errors.Add(new(QuestionErrorCodes.SubjectRequired, "subjectId", "The subject is required."));
        if (topic is null)
            errors.Add(new(QuestionErrorCodes.TopicRequired, "topicId", "The topic is required."));
        else if (subjectId != Guid.Empty && topic.SubjectId != subjectId)
            errors.Add(new(QuestionErrorCodes.TopicSubjectMismatch, "topicId", "The topic does not belong to the selected subject."));
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Normalize(NormalizationForm.FormC);
}
