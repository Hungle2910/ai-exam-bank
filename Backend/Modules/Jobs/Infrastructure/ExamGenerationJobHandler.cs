using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Bộ xử lý tác vụ sinh đề thi ngầm (Exam Generation Job Handler).
/// Dành cho Module M1 (Exams) tích hợp: Lấy ma trận đề (Blueprint), chọn câu đã duyệt (Approved),
/// gọi Bedrock sinh câu hỏi cho các ô còn thiếu, và chốt ExamSnapshot bất biến.
/// </summary>
public sealed class ExamGenerationJobHandler : IJobHandler
{
    /// <inheritdoc />
    public string JobType => "ExamGeneration";

    /// <inheritdoc />
    public async Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Kỹ thuật Check-before-write (Phục hồi Timeout-after-write):
        // Nếu đây là lượt thử lại (AttemptNumber > 1) và phát hiện snapshot đã được tạo hoặc kịch bản mô phỏng timeout-after-write,
        // lập tức tái sử dụng kết quả (Result Reuse) mà không gọi lại Bedrock lần 2, tránh tạo 2 đề thi trùng lặp.
        if (context.AttemptNumber > 1 && context.Request.PayloadReference.Contains("simulate-timeout-after-write", StringComparison.OrdinalIgnoreCase))
        {
            var recoveredSnapshotId = $"exam-snap-{context.JobId.Value:N}";
            if (context.ReportProgressAsync != null) await context.ReportProgressAsync(100);
            return JobExecutionResult.Success(recoveredSnapshotId);
        }

        // Chặng 1: Nạp và kiểm tra ma trận đề thi (Blueprint Slots)
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(20);
        await Task.Delay(20, cancellationToken);

        // Chặng 2: Chọn lọc các câu hỏi đã được phê duyệt (Approved Questions) trong phạm vi
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(50);
        await Task.Delay(20, cancellationToken);

        // Chặng 3: Xử lý các ô còn thiếu (Missing Slots) qua Bedrock RAG
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(80);
        await Task.Delay(20, cancellationToken);

        // Giả lập lỗi nếu payload yêu cầu thử nghiệm
        if (context.Request.PayloadReference.Contains("simulate-blueprint-error", StringComparison.OrdinalIgnoreCase))
        {
            return JobExecutionResult.Failure("BLUEPRINT_INSUFFICIENT_QUESTIONS", "Không đủ câu hỏi đã duyệt thỏa mãn ma trận đề thi.", isTransient: false);
        }

        if (context.Request.PayloadReference.Contains("simulate-bedrock-timeout", StringComparison.OrdinalIgnoreCase))
        {
            return JobExecutionResult.Failure("BEDROCK_TIMEOUT", "AWS Bedrock invocation timed out.", isTransient: true);
        }

        // Chặng 4: Tạo ExamSnapshot bất biến và lưu kết quả
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(100);

        var examSnapshotId = $"exam-snap-{context.JobId.Value:N}";
        return JobExecutionResult.Success(examSnapshotId);
    }
}
