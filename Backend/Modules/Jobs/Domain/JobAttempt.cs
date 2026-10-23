namespace AiExamBank.Modules.Jobs.Domain;

/// <summary>
/// Trạng thái của một lượt thực thi (Attempt) cụ thể gắn với Worker.
/// </summary>
public enum AttemptStatus
{
    /// <summary>Worker đã nhận việc và đang thực thi.</summary>
    Started,
    /// <summary>Lượt thực thi hoàn thành thành công.</summary>
    Completed,
    /// <summary>Lượt thực thi gặp lỗi (thất bại).</summary>
    Failed,
    /// <summary>Worker bị mất kết nối/crash, hết hạn Lease mà không Heartbeat, bị hệ thống thu hồi.</summary>
    Abandoned
}

/// <summary>
/// Đại diện cho một lượt thực thi (Execution Attempt) của một BackgroundJob.
/// Chứa siêu dữ liệu về Worker, thời gian bắt đầu, mã phiên độc quyền LeaseToken, và thời hạn Lease.
/// </summary>
public sealed class JobAttempt
{
    /// <summary>
    /// Khóa chính định danh duy nhất của lượt thực thi (Attempt ID).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID của tác vụ gốc (Job ID) tương ứng.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Thứ tự lần chạy (1, 2 hoặc 3). Tối đa 3 lần theo chính sách M5-01.
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Trạng thái của lượt chạy này (Started, Completed, Failed, Abandoned).
    /// </summary>
    public AttemptStatus Status { get; set; }

    /// <summary>
    /// Tên hoặc mã định danh của máy trạm Worker thực thi lượt này.
    /// </summary>
    public required string WorkerId { get; set; }

    /// <summary>
    /// Mã phiên độc quyền (Lease Token) sinh ngẫu nhiên khi claim job.
    /// Dùng cho cơ chế Fencing: Ngăn chặn Worker cũ bị timeout/treo ghi đè kết quả lên Job.
    /// </summary>
    public Guid LeaseToken { get; set; }

    /// <summary>
    /// Thời điểm Worker bắt đầu chạy lượt này (UTC).
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Thời điểm Worker gửi tín hiệu Heartbeat gần nhất (UTC).
    /// </summary>
    public DateTimeOffset HeartbeatAt { get; set; }

    /// <summary>
    /// Thời điểm hết hạn phiên thuê (UTC). Mặc định là HeartbeatAt + 2 phút.
    /// Sau thời điểm này nếu không có heartbeat, job được coi là Abandoned.
    /// </summary>
    public DateTimeOffset LeaseExpiresAt { get; set; }

    /// <summary>
    /// Thời điểm kết thúc lượt chạy (UTC), null nếu đang chạy hoặc bị bỏ hoang.
    /// </summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>
    /// Mã lỗi chuẩn nếu lượt chạy này thất bại.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Thông điệp lỗi an toàn (đã redact dữ liệu nhạy cảm) của lượt chạy này.
    /// </summary>
    public string? SafeErrorMessage { get; set; }

    /// <summary>
    /// Thời gian thực thi thực tế tính bằng mili-giây (Execution Duration in Ms).
    /// </summary>
    public long? ExecutionDurationMs { get; set; }

    /// <summary>
    /// Lý do thử lại (nếu lượt chạy này được kích hoạt lại bởi hệ thống hoặc người dùng).
    /// </summary>
    public string? RetryReason { get; set; }

    /// <summary>
    /// Đánh dấu lượt chạy này do người dùng/Admin bấm thử lại thủ công (Manual Retry) hay do hệ thống tự động.
    /// </summary>
    public bool IsManualRetry { get; set; }
}
