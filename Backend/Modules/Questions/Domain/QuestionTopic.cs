namespace AiExamBank.Modules.Questions.Domain;

/// <summary>A catalog topic together with the subject that owns it, as loaded from the persisted taxonomy.</summary>
public sealed record QuestionTopic
{
    private QuestionTopic(Guid topicId, Guid subjectId)
    {
        TopicId = topicId;
        SubjectId = subjectId;
    }

    public Guid TopicId { get; }
    public Guid SubjectId { get; }

    public static QuestionTopic Create(Guid topicId, Guid subjectId)
    {
        if (topicId == Guid.Empty) throw new ArgumentException("A topic ID is required.", nameof(topicId));
        if (subjectId == Guid.Empty) throw new ArgumentException("A subject ID is required.", nameof(subjectId));
        return new QuestionTopic(topicId, subjectId);
    }
}
