namespace AiExamBank.Modules.Exams.Domain;

/// <summary>One requested exam slot; selection and finalization remain separate use cases.</summary>
public sealed record BlueprintSlot
{
    private BlueprintSlot(Guid topicId, int questionCount, decimal pointsPerQuestion)
    {
        TopicId = topicId;
        QuestionCount = questionCount;
        PointsPerQuestion = pointsPerQuestion;
    }

    public Guid TopicId { get; }
    public int QuestionCount { get; }
    public decimal PointsPerQuestion { get; }
    public decimal TotalPoints => checked(QuestionCount * PointsPerQuestion);

    public static BlueprintSlot Create(Guid topicId, int questionCount, decimal pointsPerQuestion)
    {
        if (topicId == Guid.Empty) throw new ArgumentException("A topic ID is required.", nameof(topicId));
        if (questionCount < 1) throw new ArgumentOutOfRangeException(nameof(questionCount), "Count must be positive.");
        if (pointsPerQuestion <= 0) throw new ArgumentOutOfRangeException(nameof(pointsPerQuestion), "Points must be positive.");
        _ = checked(questionCount * pointsPerQuestion);
        return new BlueprintSlot(topicId, questionCount, pointsPerQuestion);
    }
}
