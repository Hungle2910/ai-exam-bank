namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>Identifies one immutable question revision, rather than a mutable question head.</summary>
public sealed record QuestionRevisionRef
{
    private QuestionRevisionRef(Guid questionId, int revision)
    {
        QuestionId = questionId;
        Revision = revision;
    }

    public Guid QuestionId { get; }
    public int Revision { get; }

    public static QuestionRevisionRef Create(Guid questionId, int revision)
    {
        if (questionId == Guid.Empty) throw new ArgumentException("A question ID is required.", nameof(questionId));
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision), "Revision must be positive.");
        return new QuestionRevisionRef(questionId, revision);
    }
}
