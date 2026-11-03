namespace AiExamBank.Modules.Questions.Contracts;

public enum QuestionOrigin
{
    Manual = 1,
    Import = 2,
    AiDraft = 3,
    Seed = 4
}

/// <summary>
/// Where a revision came from. An import revision carries its batch and logical row number so the database
/// can reject a retried commit that would write the same row twice.
/// </summary>
public sealed record QuestionProvenance
{
    private QuestionProvenance(QuestionOrigin origin, Guid? importBatchId, int? importRowNumber)
    {
        Origin = origin;
        ImportBatchId = importBatchId;
        ImportRowNumber = importRowNumber;
    }

    public QuestionOrigin Origin { get; }
    public Guid? ImportBatchId { get; }
    public int? ImportRowNumber { get; }

    public static QuestionProvenance Manual() => new(QuestionOrigin.Manual, null, null);
    public static QuestionProvenance AiDraft() => new(QuestionOrigin.AiDraft, null, null);

    /// <summary>Seed data bypasses review and must only be written by development and test tooling.</summary>
    public static QuestionProvenance Seed() => new(QuestionOrigin.Seed, null, null);

    public static QuestionProvenance Import(Guid importBatchId, int rowNumber)
    {
        if (importBatchId == Guid.Empty) throw new ArgumentException("An import batch ID is required.", nameof(importBatchId));
        if (rowNumber < 1) throw new ArgumentOutOfRangeException(nameof(rowNumber), "Row number must be positive.");
        return new QuestionProvenance(QuestionOrigin.Import, importBatchId, rowNumber);
    }
}
