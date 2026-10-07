using System;

namespace AiExamBank.Entities.BackgroundJobs
{
    public enum JobStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}
