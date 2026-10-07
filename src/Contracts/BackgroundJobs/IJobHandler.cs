using System.Threading;
using System.Threading.Tasks;

namespace AiExamBank.Contracts.BackgroundJobs
{
    // Logic thực thi công việc
    public interface IJobHandler
    {
        string JobType { get; }
        int MaxRetries { get; } 
        Task ExecuteAsync(string payload, CancellationToken cancellationToken);
    }
}
