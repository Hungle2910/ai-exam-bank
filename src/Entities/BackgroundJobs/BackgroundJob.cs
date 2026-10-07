using System;

namespace AiExamBank.Entities.BackgroundJobs
{
    public class BackgroundJob
    {
        public Guid Id { get; set; }
        public string Type { get; set; } // e.g., "QuestionImport", "ExamGeneration"
        public JobStatus Status { get; set; }
        public string Payload { get; set; } // JSONB
        public string Result { get; set; } // JSONB
        public string CreatedBy { get; set; } // UserID of requester
        public string IdempotencyKey { get; set; } // Unique constraint
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
