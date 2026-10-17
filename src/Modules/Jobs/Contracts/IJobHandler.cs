namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Hợp đồng thực thi tác vụ ngầm do Worker gọi trong mỗi lượt chạy (attempt).
/// Các module nghiệp vụ (M1 Sinh đề AI, M2 Import CSV, M4 RAG) implement interface này để xử lý công việc.
/// Worker sở hữu chính sách lease, heartbeat và retry; Handler chỉ tập trung vào logic nghiệp vụ.
/// </summary>
public interface IJobHandler
{
    /// <summary>
    /// Tên định danh duy nhất của loại tác vụ (ví dụ: "ExamGeneration", "CsvImport").
    /// Worker dựa vào chuỗi này để tìm đúng Handler tương ứng trong JobRegistry.
    /// </summary>
    string JobType { get; }

    /// <summary>
    /// Thực thi một lượt chạy tác vụ với đầy đủ ngữ cảnh (JobId, tham số, lượt chạy, báo tiến độ).
    /// Mặc định ủy quyền sang overload tương thích ngược ExecuteAsync(JobRequest, ...) và trả về Success.
    /// </summary>
    /// <param name="context">Hồ sơ giao việc chứa thông số và callback báo tiến độ.</param>
    /// <param name="cancellationToken">Token phát tín hiệu khi người dùng bấm hủy hoặc Worker shutdown.</param>
    /// <returns>Biên bản kết quả thực thi (thành công/thất bại, mã kết quả, lỗi an toàn, cờ thử lại).</returns>
    async Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        await ExecuteAsync(context.Request, cancellationToken);
        return JobExecutionResult.Success();
    }

    /// <summary>
    /// Phương thức tương thích ngược dành cho các handler đơn giản chỉ cần JobRequest.
    /// </summary>
    /// <param name="request">Thông tin yêu cầu tác vụ.</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Task đại diện cho tiến trình thực thi.</returns>
    Task ExecuteAsync(JobRequest request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
