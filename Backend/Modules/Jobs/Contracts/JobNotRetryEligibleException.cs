namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Ngoại lệ ném ra khi yêu cầu thử lại (retry) một tác vụ không đủ điều kiện thử lại
/// (ví dụ: tác vụ thất bại do lỗi vĩnh viễn Permanent, cấu hình sai hoặc thiếu quyền truy cập).
/// </summary>
public sealed class JobNotRetryEligibleException(string message) : InvalidOperationException(message);
