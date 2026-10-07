using System;

namespace AiExamBank.Entities.BackgroundJobs
{
    public class JobStatusDto
    {
        public Guid JobId { get; set; }
        public JobStatus Status { get; set; }
        public int ProgressPercentage { get; set; }
        public string ResultMessage { get; set; }
        public string ErrorMessage { get; set; }
    }
}
