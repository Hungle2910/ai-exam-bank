using System;

namespace AiExamBank.Entities.BackgroundJobs
{
    public enum AttemptStatus
    {
        Started,
        Completed,
        Failed,
        Abandoned
    }

    public class JobAttempt
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; } // FK to BackgroundJob
        public int AttemptNumber { get; set; }
        public AttemptStatus Status { get; set; }
        public string WorkerId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime HeartbeatAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorTrace { get; set; }
    }
}
