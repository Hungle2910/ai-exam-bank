using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Bộ xử lý tác vụ mẫu (Sample Job Handler) dùng cho kiểm thử, tích hợp và trình diễn Worker.
/// Minh họa đầy đủ quy trình: báo cáo tiến độ từng chặng, tôn trọng CancellationToken và trả về kết quả an toàn.
/// </summary>
public sealed class SampleJobHandler : IJobHandler
{
    /// <inheritdoc />
    public string JobType => "SampleJob";

    /// <inheritdoc />
    public async Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Mô phỏng chặng 1: Khởi động tác vụ
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(25);
        await Task.Delay(10, cancellationToken);

        // Mô phỏng chặng 2: Xử lý dữ liệu trung gian
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(50);
        await Task.Delay(10, cancellationToken);

        // Mô phỏng chặng 3: Tính toán gần hoàn tất
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(75);
        await Task.Delay(10, cancellationToken);

        // Kiểm tra kịch bản giả lập lỗi nếu payload yêu cầu mô phỏng lỗi
        if (context.Request.PayloadReference.Contains("simulate-transient-error", StringComparison.OrdinalIgnoreCase))
        {
            return JobExecutionResult.Failure("SIMULATED_TRANSIENT_TIMEOUT", "Simulated external service timeout.", isTransient: true);
        }

        if (context.Request.PayloadReference.Contains("simulate-permanent-error", StringComparison.OrdinalIgnoreCase))
        {
            return JobExecutionResult.Failure("SIMULATED_VALIDATION_ERROR", "Simulated permanent business error.", isTransient: false);
        }

        // Mô phỏng chặng 4: Hoàn thành 100%
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(100);

        var generatedResultId = $"result-{context.JobId.Value:N}";
        return JobExecutionResult.Success(generatedResultId);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(JobRequest request, CancellationToken cancellationToken)
    {
        // Tương thích ngược với interface cũ
        return Task.CompletedTask;
    }
}
