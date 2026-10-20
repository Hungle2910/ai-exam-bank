namespace AiExamBank.Modules.Jobs.Contracts;

public enum JobScopeKind
{
    School,
    MinistryExam
}

/// <summary>Authorization scope attached to the persisted job and rechecked on reads and retries.</summary>
public sealed record JobScope
{
    private JobScope(JobScopeKind kind, string resourceId) => (Kind, ResourceId) = (kind, resourceId);

    public JobScopeKind Kind { get; }
    public string ResourceId { get; }

    public static JobScope Create(JobScopeKind kind, string resourceId)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("A scope resource ID is required.", nameof(resourceId));
        return new JobScope(kind, resourceId);
    }
}
