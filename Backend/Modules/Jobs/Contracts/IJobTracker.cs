namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>Reads status only after checking the current actor against the persisted job scope.</summary>
public interface IJobTracker
{
    Task<JobStatusDto> GetJobStatusAsync(JobRef job, string requesterId, CancellationToken cancellationToken);
}
