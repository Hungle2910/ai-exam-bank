namespace AiExamBank.Modules.Jobs.Contracts;

public enum JobScopeKind
{
    School,
    MinistryExam
}

/// <summary>Authorization scope attached to the persisted job and rechecked on reads and retries.</summary>
public sealed record JobScope(JobScopeKind Kind, string ResourceId);
