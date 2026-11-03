using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Questions.Tests;

[TestClass]
public sealed class McqContentTests
{
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly QuestionTopic Topic = QuestionTopic.Create(Guid.NewGuid(), SubjectId);

    private static McqInput Valid() => new(
        "What is 2 + 3?",
        ["4", "5", "6", "7"],
        "B",
        "easy",
        SubjectId,
        Topic);

    private static string[] CodesFor(McqInput input) =>
        McqContent.Validate(input).Errors.Select(error => error.Code).ToArray();

    [TestMethod]
    public void Create_keeps_valid_content_with_the_answer_index_and_taxonomy()
    {
        var content = McqContent.Create(Valid());

        Assert.AreEqual("What is 2 + 3?", content.Stem);
        Assert.AreEqual('B', content.CorrectOption);
        Assert.AreEqual(1, content.CorrectOptionIndex);
        Assert.AreEqual(Difficulty.Easy, content.Difficulty);
        Assert.AreEqual(SubjectId, content.SubjectId);
        Assert.AreEqual(Topic.TopicId, content.TopicId);
        Assert.HasCount(4, content.Options);
    }

    [TestMethod]
    public void Validate_normalizes_whitespace_case_and_unicode_form()
    {
        var composed = "Đạo hàm".Normalize(System.Text.NormalizationForm.FormC);
        var decomposed = "Đạo hàm".Normalize(System.Text.NormalizationForm.FormD);
        var input = Valid() with
        {
            Stem = $"  {decomposed}  ",
            CorrectOption = " c ",
            Difficulty = "HARD",
            SourceText = "   "
        };

        var content = McqContent.Create(input);

        Assert.AreEqual(composed, content.Stem);
        Assert.AreEqual('C', content.CorrectOption);
        Assert.AreEqual(Difficulty.Hard, content.Difficulty);
        Assert.IsNull(content.SourceText);
    }

    [TestMethod]
    public void Validate_rejects_a_missing_correct_option()
    {
        CollectionAssert.AreEqual(
            new[] { QuestionErrorCodes.CorrectOptionRequired },
            CodesFor(Valid() with { CorrectOption = null }));
        CollectionAssert.AreEqual(
            new[] { QuestionErrorCodes.CorrectOptionRequired },
            CodesFor(Valid() with { CorrectOption = "  " }));
    }

    [TestMethod]
    public void Validate_rejects_an_answer_outside_the_four_options()
    {
        foreach (var answer in new[] { "E", "AB", "1", "0" })
            CollectionAssert.AreEqual(
                new[] { QuestionErrorCodes.CorrectOptionInvalid },
                CodesFor(Valid() with { CorrectOption = answer }),
                $"answer '{answer}'");
    }

    [TestMethod]
    public void Validate_rejects_the_wrong_number_of_options()
    {
        Assert.Contains(QuestionErrorCodes.OptionsCountInvalid, CodesFor(Valid() with { Options = null }));
        Assert.Contains(QuestionErrorCodes.OptionsCountInvalid, CodesFor(Valid() with { Options = ["4", "5", "6"] }));
        Assert.Contains(QuestionErrorCodes.OptionsCountInvalid, CodesFor(Valid() with { Options = ["4", "5", "6", "7", "8"] }));
    }

    [TestMethod]
    public void Validate_reports_the_empty_option_by_index()
    {
        var result = McqContent.Validate(Valid() with { Options = ["4", "5", " ", "7"] });

        Assert.IsFalse(result.IsValid);
        Assert.IsNull(result.Content);
        var error = Assert.ContainsSingle(result.Errors);
        Assert.AreEqual(QuestionErrorCodes.OptionRequired, error.Code);
        Assert.AreEqual("options[2]", error.Field);
    }

    [TestMethod]
    public void Validate_rejects_duplicate_options_after_normalization()
    {
        var result = McqContent.Validate(Valid() with { Options = ["HTTPS", "FTP", " https ", "SMTP"] });

        var error = Assert.ContainsSingle(result.Errors);
        Assert.AreEqual(QuestionErrorCodes.OptionsDuplicate, error.Code);
        Assert.AreEqual("options[2]", error.Field);
    }

    [TestMethod]
    public void Validate_rejects_a_topic_from_another_subject()
    {
        var otherSubjectTopic = QuestionTopic.Create(Guid.NewGuid(), Guid.NewGuid());

        CollectionAssert.AreEqual(
            new[] { QuestionErrorCodes.TopicSubjectMismatch },
            CodesFor(Valid() with { Topic = otherSubjectTopic }));
    }

    [TestMethod]
    public void Validate_requires_subject_and_topic()
    {
        var codes = CodesFor(Valid() with { SubjectId = Guid.Empty, Topic = null });

        Assert.Contains(QuestionErrorCodes.SubjectRequired, codes);
        Assert.Contains(QuestionErrorCodes.TopicRequired, codes);
    }

    [TestMethod]
    public void Validate_rejects_an_unknown_difficulty()
    {
        foreach (var difficulty in new[] { null, "", "extreme", "1" })
            CollectionAssert.AreEqual(
                new[] { QuestionErrorCodes.DifficultyInvalid },
                CodesFor(Valid() with { Difficulty = difficulty }),
                $"difficulty '{difficulty}'");
    }

    [TestMethod]
    public void Validate_rejects_a_missing_or_oversized_stem_and_oversized_text()
    {
        Assert.Contains(QuestionErrorCodes.StemRequired, CodesFor(Valid() with { Stem = " " }));
        Assert.Contains(QuestionErrorCodes.StemTooLong,
            CodesFor(Valid() with { Stem = new string('x', QuestionRules.MaxStemLength + 1) }));
        Assert.Contains(QuestionErrorCodes.OptionTooLong,
            CodesFor(Valid() with { Options = ["4", new string('x', QuestionRules.MaxOptionLength + 1), "6", "7"] }));
        Assert.Contains(QuestionErrorCodes.SourceTooLong,
            CodesFor(Valid() with { SourceText = new string('x', QuestionRules.MaxSourceLength + 1) }));
    }

    [TestMethod]
    public void Validate_accepts_text_at_the_exact_limits()
    {
        var input = Valid() with
        {
            Stem = new string('x', QuestionRules.MaxStemLength),
            Options = [new string('a', QuestionRules.MaxOptionLength), "b", "c", "d"],
            SourceText = new string('s', QuestionRules.MaxSourceLength)
        };

        Assert.IsTrue(McqContent.Validate(input).IsValid);
    }

    [TestMethod]
    public void Validate_reports_every_failure_so_an_import_row_shows_all_errors()
    {
        var input = new McqInput("", ["5", "6", "", "8"], "E", "extreme", SubjectId, Topic);

        var codes = CodesFor(input);

        Assert.Contains(QuestionErrorCodes.StemRequired, codes);
        Assert.Contains(QuestionErrorCodes.OptionRequired, codes);
        Assert.Contains(QuestionErrorCodes.CorrectOptionInvalid, codes);
        Assert.Contains(QuestionErrorCodes.DifficultyInvalid, codes);
    }

    [TestMethod]
    public void Create_throws_with_the_collected_errors_for_invalid_content()
    {
        var exception = Assert.ThrowsExactly<QuestionValidationException>(
            () => McqContent.Create(Valid() with { CorrectOption = null }));

        Assert.AreEqual(QuestionErrorCodes.CorrectOptionRequired, Assert.ContainsSingle(exception.Errors).Code);
    }

    [TestMethod]
    public void QuestionTopic_rejects_empty_identifiers()
    {
        Assert.ThrowsExactly<ArgumentException>(() => QuestionTopic.Create(Guid.Empty, Guid.NewGuid()));
        Assert.ThrowsExactly<ArgumentException>(() => QuestionTopic.Create(Guid.NewGuid(), Guid.Empty));
    }
}
