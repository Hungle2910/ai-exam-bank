namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Ngoại lệ ném ra khi phát hiện xung đột dữ liệu trong cơ chế Idempotency:
/// Cùng bộ khóa IdempotencyKey (Scope, CreatedBy, JobType, IdempotencyKey)
/// nhưng nội dung yêu cầu (Fingerprint / Payload) lại khác nhau.
/// Tầng HTTP API bắt ngoại lệ này để phản hồi mã lỗi chuẩn HTTP 409 Conflict.
/// </summary>
public sealed class JobConflictException : Exception
{
    public JobConflictException(string message) : base(message)
    {
    }

    public JobConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
