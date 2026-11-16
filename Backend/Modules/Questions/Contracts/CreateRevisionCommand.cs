using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>
/// Edits a question by appending a new DRAFT revision; an existing revision is never changed in place.
/// <see cref="BaseRevision"/> is the revision the editor started from, so a concurrent edit is detected.
/// </summary>
public sealed record CreateRevisionCommand
{
    private CreateRevisionCommand(
        Guid actorUserId, Guid questionId, int baseRevision, McqInput content, QuestionProvenance provenance)
    {
        ActorUserId = actorUserId;
        QuestionId = questionId;
        BaseRevision = baseRevision;
        Content = content;
        Provenance = provenance;
    }

    public Guid ActorUserId { get; }
    public Guid QuestionId { get; }
    public int BaseRevision { get; }
    public McqInput Content { get; }
    public QuestionProvenance Provenance { get; }

    public static CreateRevisionCommand Create(
        Guid actorUserId, Guid questionId, int baseRevision, McqInput content, QuestionProvenance provenance)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("An actor ID is required.", nameof(actorUserId));
        if (questionId == Guid.Empty) throw new ArgumentException("A question ID is required.", nameof(questionId));
        if (baseRevision < 1) throw new ArgumentOutOfRangeException(nameof(baseRevision), "Base revision must be positive.");
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(provenance);
        return new CreateRevisionCommand(actorUserId, questionId, baseRevision, content, provenance);
    }
}
