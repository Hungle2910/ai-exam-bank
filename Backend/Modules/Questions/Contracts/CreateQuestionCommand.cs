using AiExamBank.Modules.Questions.Domain;

namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>
/// Creates a question and its first DRAFT revision. The school comes from the authenticated upload or request
/// context, never from client-supplied scope, and the writer re-checks it against the actor's current assignments.
/// Content is validated by the writer so the API, the editor and import rows share one set of rules.
/// </summary>
public sealed record CreateQuestionCommand
{
    private CreateQuestionCommand(Guid actorUserId, Guid schoolId, McqInput content, QuestionProvenance provenance)
    {
        ActorUserId = actorUserId;
        SchoolId = schoolId;
        Content = content;
        Provenance = provenance;
    }

    public Guid ActorUserId { get; }
    public Guid SchoolId { get; }
    public McqInput Content { get; }
    public QuestionProvenance Provenance { get; }

    public static CreateQuestionCommand Create(
        Guid actorUserId, Guid schoolId, McqInput content, QuestionProvenance provenance)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("An actor ID is required.", nameof(actorUserId));
        if (schoolId == Guid.Empty) throw new ArgumentException("A school ID is required.", nameof(schoolId));
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(provenance);
        return new CreateQuestionCommand(actorUserId, schoolId, content, provenance);
    }
}
