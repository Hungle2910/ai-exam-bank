namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>Stable identifier for status, retries and logs of one logical job.</summary>
public sealed record JobRef
{
    private JobRef(Guid value) => Value = value;

    public Guid Value { get; }

    public static JobRef Create(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("A job ID is required.", nameof(value));
        return new JobRef(value);
    }
}
