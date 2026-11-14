namespace AiExamBank.Persistence.Exams;

public sealed class ExamGenerationEntity
{
    public Guid Id { get; set; }
    public Guid BlueprintId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid DepartmentId { get; set; }
    public string BlueprintVersion { get; set; } = "";
    public string MatrixJson { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public int Seed { get; set; }
    public string Status { get; set; } = "QUEUED";
    public Guid? JobId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<ExamItemEntity> Items { get; set; } = [];
    public List<ExamGapEntity> Gaps { get; set; } = [];
}

public sealed class ExamItemEntity
{
    public Guid ExamId { get; set; }
    public int Position { get; set; }
    public int SlotIndex { get; set; }
    public Guid QuestionId { get; set; }
    public int Revision { get; set; }
    public decimal Points { get; set; }
    public string ContentJson { get; set; } = "";
}

public sealed class ExamGapEntity
{
    public Guid ExamId { get; set; }
    public int SlotIndex { get; set; }
    public Guid TopicId { get; set; }
    public string Difficulty { get; set; } = "";
    public int MissingCount { get; set; }
    public decimal MissingPoints { get; set; }
}
