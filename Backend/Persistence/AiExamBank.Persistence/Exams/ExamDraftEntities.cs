namespace AiExamBank.Persistence.Exams;

public sealed class ExamDraftBatchEntity
{
    public Guid ExamId { get; set; }
    public Guid JobId { get; set; }
    public string Fingerprint { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ExamDraftSlotEntity
{
    public Guid ExamId { get; set; }
    public Guid JobId { get; set; }
    public int SlotIndex { get; set; }
    public string CorrelationId { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ErrorCode { get; set; }
}

public sealed class ExamDraftLinkEntity
{
    public Guid ExamId { get; set; }
    public Guid JobId { get; set; }
    public int SlotIndex { get; set; }
    public int Ordinal { get; set; }
    public Guid QuestionId { get; set; }
    public int Revision { get; set; }
    public string SourceId { get; set; } = "";
    public string SourceLocator { get; set; } = "";
}
