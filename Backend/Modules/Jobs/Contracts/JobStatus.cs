namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Trạng thái vòng đời của một tác vụ nền bền vững theo chuẩn thiết kế M5-01 / M5-02.
/// </summary>
public enum JobStatus
{
    /// <summary>Tác vụ đã được tiếp nhận vào hàng đợi và đang chờ Worker thăm dò.</summary>
    Pending,
    /// <summary>Tác vụ đang được Worker chiếm quyền thực thi (Lease) và xử lý tích cực.</summary>
    Running,
    /// <summary>Tác vụ đã thực thi thành công 100% và tạo ra kết quả an toàn.</summary>
    Completed,
    /// <summary>Tác vụ thất bại do lỗi không thể sửa hoặc đã cạn kiệt 3 lần thử lại.</summary>
    Failed,
    /// <summary>Tác vụ đã bị người dùng hoặc hệ thống hủy bỏ an toàn.</summary>
    Cancelled
}
