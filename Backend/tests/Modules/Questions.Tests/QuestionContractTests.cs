using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Questions.Tests;

[TestClass]
public sealed class QuestionContractTests
{
    private static readonly Guid SubjectId = Guid.NewGuid();

    private static McqInput Content() => new(
        "What is 2 + 3?", ["4", "5", "6", "7"], "B", "easy", SubjectId, QuestionTopic.Create(Guid.NewGuid(), SubjectId));

    [TestMethod]
    public void Provenance_import_requires_a_batch_and_a_positive_row()
    {
        var batchId = Guid.NewGuid();

        var provenance = QuestionProvenance.Import(batchId, 2);

        Assert.AreEqual(QuestionOrigin.Import, provenance.Origin);
        Assert.AreEqual(batchId, provenance.ImportBatchId);
        Assert.AreEqual(2, provenance.ImportRowNumber);
        Assert.ThrowsExactly<ArgumentException>(() => QuestionProvenance.Import(Guid.Empty, 2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuestionProvenance.Import(batchId, 0));
    }

    [TestMethod]
    public void Provenance_of_other_origins_carries_no_import_reference()
    {
        foreach (var provenance in new[] { QuestionProvenance.Manual(), QuestionProvenance.AiDraft(), QuestionProvenance.Seed() })
        {
            Assert.IsNull(provenance.ImportBatchId);
            Assert.IsNull(provenance.ImportRowNumber);
        }

        Assert.AreEqual(QuestionOrigin.AiDraft, QuestionProvenance.AiDraft().Origin);
    }

    [TestMethod]
    public void CreateQuestionCommand_requires_actor_school_content_and_provenance()
    {
        var actor = Guid.NewGuid();
        var school = Guid.NewGuid();

        var command = CreateQuestionCommand.Create(actor, school, Content(), QuestionProvenance.Manual());

        Assert.AreEqual(actor, command.ActorUserId);
        Assert.AreEqual(school, command.SchoolId);
        Assert.ThrowsExactly<ArgumentException>(
            () => CreateQuestionCommand.Create(Guid.Empty, school, Content(), QuestionProvenance.Manual()));
        Assert.ThrowsExactly<ArgumentException>(
            () => CreateQuestionCommand.Create(actor, Guid.Empty, Content(), QuestionProvenance.Manual()));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => CreateQuestionCommand.Create(actor, school, null!, QuestionProvenance.Manual()));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => CreateQuestionCommand.Create(actor, school, Content(), null!));
    }

    [TestMethod]
    public void CreateQuestionCommand_does_not_validate_content_so_the_writer_reports_every_error()
    {
        var invalid = Content() with { CorrectOption = null };

        var command = CreateQuestionCommand.Create(Guid.NewGuid(), Guid.NewGuid(), invalid, QuestionProvenance.Manual());

        Assert.IsFalse(McqContent.Validate(command.Content).IsValid);
    }

    [TestMethod]
    public void CreateRevisionCommand_requires_a_positive_base_revision()
    {
        var actor = Guid.NewGuid();
        var question = Guid.NewGuid();

        var command = CreateRevisionCommand.Create(actor, question, 3, Content(), QuestionProvenance.Manual());

        Assert.AreEqual(3, command.BaseRevision);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => CreateRevisionCommand.Create(actor, question, 0, Content(), QuestionProvenance.Manual()));
        Assert.ThrowsExactly<ArgumentException>(
            () => CreateRevisionCommand.Create(actor, Guid.Empty, 1, Content(), QuestionProvenance.Manual()));
    }

    [TestMethod]
    public void ApprovedQuestionQuery_bounds_the_limit_and_defaults_to_no_exclusions()
    {
        var school = Guid.NewGuid();
        var topic = Guid.NewGuid();

        var query = ApprovedQuestionQuery.Create(school, topic, Difficulty.Hard, 5);

        Assert.AreEqual(5, query.Limit);
        Assert.IsEmpty(query.ExcludedQuestionIds);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ApprovedQuestionQuery.Create(school, topic, Difficulty.Hard, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ApprovedQuestionQuery.Create(school, topic, Difficulty.Hard, ApprovedQuestionQuery.MaxLimit + 1));
    }

    [TestMethod]
    public void ApprovedQuestionQuery_rejects_missing_scope_and_unknown_difficulty()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ApprovedQuestionQuery.Create(Guid.Empty, Guid.NewGuid(), Difficulty.Easy, 1));
        Assert.ThrowsExactly<ArgumentException>(
            () => ApprovedQuestionQuery.Create(Guid.NewGuid(), Guid.Empty, Difficulty.Easy, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ApprovedQuestionQuery.Create(Guid.NewGuid(), Guid.NewGuid(), (Difficulty)99, 1));
    }

    [TestMethod]
    public void CatalogResolution_is_resolved_only_when_subject_topic_are_found_without_errors()
    {
        var topic = QuestionTopic.Create(Guid.NewGuid(), SubjectId);
        var notFound = new QuestionValidationError(QuestionErrorCodes.TopicNotFound, "topic", "Unknown topic.");

        Assert.IsTrue(new CatalogResolution(SubjectId, topic, []).IsResolved);
        Assert.IsFalse(new CatalogResolution(SubjectId, null, [notFound]).IsResolved);
        Assert.IsFalse(new CatalogResolution(null, null, []).IsResolved);
    }
}
