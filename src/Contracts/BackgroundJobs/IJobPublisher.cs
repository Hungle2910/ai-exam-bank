using System;
using System.Threading.Tasks;

namespace AiExamBank.Contracts.BackgroundJobs
{
    // Dùng để ném việc vào hàng đợi
    public interface IJobPublisher
    {
        Task<Guid> EnqueueAsync(string jobType, string payload, string requesterId, string idempotencyKey);
    }
}
