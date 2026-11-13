namespace AiExamBank.Modules.Questions.Domain;

/// <summary>MVP multiple-choice limits; these match the M2-01 CSV contract so import and API reject the same input.</summary>
public static class QuestionRules
{
    public const int MaxStemLength = 4000;
    public const int MaxOptionLength = 1000;
    public const int MaxSourceLength = 2000;
    public const string OptionLabels = "ABCD";
    public static int OptionCount => OptionLabels.Length;
}
