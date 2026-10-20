namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>Executes one claimed attempt. The Worker owns retry and lease policy.</summary>
public interface IJobHandler
{
    string JobType { get; }

    Task ExecuteAsync(JobRequest request, CancellationToken cancellationToken);
}
