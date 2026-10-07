using System;
using System.Threading.Tasks;
using AiExamBank.Entities.BackgroundJobs;

namespace AiExamBank.Contracts.BackgroundJobs
{
    // Dùng để lấy % tiến độ hiển thị lên UI
    public interface IJobTracker
    {
        Task<JobStatusDto> GetJobStatusAsync(Guid jobId, string requesterId);
    }
}
