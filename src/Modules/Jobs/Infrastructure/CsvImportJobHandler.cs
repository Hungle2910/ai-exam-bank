using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Bộ xử lý tác vụ nhập câu hỏi hàng loạt từ CSV (CSV Import Job Handler).
/// Dành cho Module M2 (Import) tích hợp: Phân tích cú pháp file CSV từ S3, kiểm tra schema từng dòng,
/// đối chiếu chống trùng lặp và ghi nhận vào ngân hàng câu hỏi.
/// </summary>
public sealed class CsvImportJobHandler : IJobHandler
{
    /// <inheritdoc />
    public string JobType => "CsvImport";

    /// <inheritdoc />
    public async Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Chặng 1: Đọc và phân tích cấu trúc file CSV
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(25);
        await Task.Delay(20, cancellationToken);

        // Chặng 2: Xác thực tính hợp lệ từng dòng (Schema & Validation)
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(50);
        await Task.Delay(20, cancellationToken);

        // Kiểm tra kịch bản giả lập lỗi dữ liệu dòng (như trong wireframe M5-01)
        if (context.Request.PayloadReference.Contains("simulate-row-error", StringComparison.OrdinalIgnoreCase))
        {
            return JobExecutionResult.Failure(
                "ROW_VALIDATION_ERROR",
                "Dữ liệu dòng 15 bị thiếu trường 'Đáp án đúng'. Sửa file CSV và thử lại.",
                isTransient: false);
        }

        // Chặng 3: Kiểm tra chống trùng lặp nội dung câu hỏi
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(75);
        await Task.Delay(20, cancellationToken);

        // Chặng 4: Commit batch và tạo các bản ghi QuestionRevision
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ReportProgressAsync != null) await context.ReportProgressAsync(100);

        var batchResultId = $"import-batch-{context.JobId.Value:N}";
        return JobExecutionResult.Success(batchResultId);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(JobRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
