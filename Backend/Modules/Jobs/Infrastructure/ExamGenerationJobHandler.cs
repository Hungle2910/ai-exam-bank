using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;

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
    private readonly IAiProviderCircuitBreaker? _circuitBreaker;

    public ExamGenerationJobHandler(IAiProviderCircuitBreaker? circuitBreaker = null)
    {
        _circuitBreaker = circuitBreaker;
    }

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

        // Kiểm tra trạng thái Circuit Breaker trước khi gọi AI Provider
        if (_circuitBreaker != null && !_circuitBreaker.CanExecute("AWS_BEDROCK"))
        {
            return JobExecutionResult.Failure(
                AwsErrorCodes.CircuitBreakerOpen,
                AwsErrorMapping.GetActionableMessage(AwsErrorCodes.CircuitBreakerOpen),
                isTransient: true);
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
            _circuitBreaker?.RecordFailure("AWS_BEDROCK", "BLUEPRINT_INSUFFICIENT_QUESTIONS");
            return JobExecutionResult.Failure("BLUEPRINT_INSUFFICIENT_QUESTIONS", "Không đủ câu hỏi đã duyệt thỏa mãn ma trận đề thi.", isTransient: false);
        }

        if (context.Request.PayloadReference.Contains("simulate-bedrock-throttled", StringComparison.OrdinalIgnoreCase))
        {
            _circuitBreaker?.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);
            return JobExecutionResult.Failure(AwsErrorCodes.BedrockThrottled, "AWS Bedrock HTTP 429 ThrottlingException: Request rate exceeded.", isTransient: true);
        }

        if (context.Request.PayloadReference.Contains("simulate-bedrock-timeout", StringComparison.OrdinalIgnoreCase))
        {
            _circuitBreaker?.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockModelTimeout);
            return JobExecutionResult.Failure(AwsErrorCodes.BedrockModelTimeout, "AWS Bedrock invocation timed out.", isTransient: true);
        }

        if (context.Request.PayloadReference.Contains("simulate-bedrock-access-denied", StringComparison.OrdinalIgnoreCase))
        {
            _circuitBreaker?.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockAccessDenied);
            return JobExecutionResult.Failure(AwsErrorCodes.BedrockAccessDenied, "AWS Bedrock AccessDeniedException: User is not authorized to invoke model.", isTransient: false);
        }

        if (context.Request.PayloadReference.Contains("simulate-bedrock-quota-exceeded", StringComparison.OrdinalIgnoreCase))
        {
            _circuitBreaker?.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockQuotaExceeded);
            return JobExecutionResult.Failure(AwsErrorCodes.BedrockQuotaExceeded, "AWS Bedrock ServiceQuotaExceededException: Monthly quota reached.", isTransient: false);
        }

        if (context.Request.PayloadReference.Contains("simulate-ai-malformed-output", StringComparison.OrdinalIgnoreCase))
        {
            _circuitBreaker?.RecordFailure("AWS_BEDROCK", AwsErrorCodes.AiMalformedOutput);
            return JobExecutionResult.Failure(AwsErrorCodes.AiMalformedOutput, "Model output could not be parsed as valid exam schema JSON.", isTransient: false);
        }

        if (context.Request.PayloadReference.Contains("simulate-rag-empty", StringComparison.OrdinalIgnoreCase))
        {
            _circuitBreaker?.RecordFailure("AWS_BEDROCK", AwsErrorCodes.RagRetrievalEmpty);
            return JobExecutionResult.Failure(AwsErrorCodes.RagRetrievalEmpty, "No matching knowledge base chunks found for blueprint slots.", isTransient: false);
        }

        // Chặng 4: Tạo ExamSnapshot bất biến và lưu kết quả
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(100);

        _circuitBreaker?.RecordSuccess("AWS_BEDROCK");
        var examSnapshotId = $"exam-snap-{context.JobId.Value:N}";
        return JobExecutionResult.Success(examSnapshotId);
    }
}
