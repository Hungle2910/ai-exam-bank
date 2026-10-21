namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Ngữ cảnh thực thi được Worker cung cấp cho IJobHandler trong mỗi lượt chạy (attempt).
/// Các thành viên (M1 Sinh đề, M2 Import CSV, M4 RAG) sử dụng thông tin này khi xử lý tác vụ ngầm.
/// </summary>
public sealed class JobExecutionContext
{
    /// <summary>
    /// Định danh duy nhất và bất biến của tác vụ ngầm (Job ID).
    /// </summary>
    public required JobRef JobId { get; init; }

    /// <summary>
    /// Yêu cầu tác vụ ban đầu: Chứa tham chiếu dữ liệu (PayloadReference), người yêu cầu,
    /// phạm vi trường/Bộ (JobScope), khóa chống trùng (IdempotencyKey) và mã theo vết (CorrelationId).
    /// </summary>
    public required JobRequest Request { get; init; }

    /// <summary>
    /// Số thứ tự của lượt chạy hiện tại (1 là lần chạy đầu; 2 và 3 là các lượt thử lại khi gặp sự cố tạm thời).
    /// Tối đa 3 lượt chạy theo chính sách độ tin cậy M5-01.
    /// </summary>
    public required int AttemptNumber { get; init; }

    /// <summary>
    /// Hàm ủy thác (callback) tùy chọn để Handler cập nhật phần trăm tiến độ thực tế (từ 0 đến 100%).
    /// Chỉ gọi khi đo lường được tiến độ cụ thể (ví dụ: đã đọc 30/100 dòng CSV). Không sinh tiến độ giả.
    /// </summary>
    public Func<int, Task>? ReportProgressAsync { get; init; }
}
