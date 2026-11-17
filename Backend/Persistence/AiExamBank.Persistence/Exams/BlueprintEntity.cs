namespace AiExamBank.Persistence.Exams;

public sealed class BlueprintEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid SubjectId { get; set; }
    public string Name { get; set; } = "";
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public decimal TotalPoints { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? ArchivedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<BlueprintSlotEntity> Slots { get; set; } = [];
}

public sealed class BlueprintSlotEntity
{
    public Guid Id { get; set; }
    public Guid BlueprintId { get; set; }
    public Guid TopicId { get; set; }
    public int Position { get; set; }
    public string Difficulty { get; set; } = "";
    public int QuestionCount { get; set; }
    public decimal PointsPerQuestion { get; set; }
}
