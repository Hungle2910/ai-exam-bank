namespace AiExamBank.Modules.Import.Contracts;

/// <summary>Identifies an import batch across preview, commit and status calls.</summary>
public sealed record ImportBatchRef
{
    private ImportBatchRef(Guid value) => Value = value;

    public Guid Value { get; }

    public static ImportBatchRef Create(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("An import batch ID is required.", nameof(value));
        return new ImportBatchRef(value);
    }
}
