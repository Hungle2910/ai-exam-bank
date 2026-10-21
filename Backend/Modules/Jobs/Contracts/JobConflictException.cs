namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Ngoại lệ ném ra khi phát hiện xung đột dữ liệu trong cơ chế Idempotency:
/// Cùng bộ khóa IdempotencyKey (Scope, CreatedBy, JobType, IdempotencyKey)
/// nhưng nội dung yêu cầu (Fingerprint / Payload) lại khác nhau.
/// Tầng HTTP API bắt ngoại lệ này để phản hồi mã lỗi chuẩn HTTP 409 Conflict.
/// </summary>
public sealed class JobConflictException : Exception
{
    /// <summary>
    /// Khởi tạo ngoại lệ xung đột Idempotency kèm thông báo lỗi.
    /// </summary>
    /// <param name="message">Thông điệp chi tiết về sự xung đột khóa.</param>
    public JobConflictException(string message) : base(message)
    {
    }

    /// <summary>
    /// Khởi tạo ngoại lệ xung đột Idempotency kèm thông báo lỗi và ngoại lệ gốc.
    /// </summary>
    /// <param name="message">Thông điệp chi tiết về sự xung đột.</param>
    /// <param name="innerException">Ngoại lệ nguyên nhân bên trong.</param>
    public JobConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
