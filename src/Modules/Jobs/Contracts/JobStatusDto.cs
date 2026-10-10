namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>Safe status projection; progress is null when a handler cannot report it.</summary>
public sealed record JobStatusDto(
    JobRef Job,
    JobStatus Status,
    int? ProgressPercentage,
    string? ResultId,
    string? ErrorCode,
    string? SafeMessage);
