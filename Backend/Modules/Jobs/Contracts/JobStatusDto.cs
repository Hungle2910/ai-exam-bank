namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Bản chiếu trạng thái an toàn của tác vụ ngầm phục vụ hiển thị trên Status UI và phản hồi API.
/// Tiến độ đo lường bằng null nếu Handler không đo lường được (không sinh tiến độ giả).
/// </summary>
/// <param name="Job">Tham chiếu định danh bất biến của tác vụ.</param>
/// <param name="JobType">Loại tác vụ được lưu trong kho job.</param>
/// <param name="Status">Trạng thái vòng đời hiện tại của tác vụ.</param>
/// <param name="ProgressPercentage">Phần trăm tiến độ thực tế (0 - 100%) hoặc null nếu không đo lường được.</param>
/// <param name="ResultId">Mã định danh kết quả sinh ra nếu tác vụ thành công.</param>
/// <param name="ErrorCode">Mã lỗi an toàn nếu tác vụ thất bại.</param>
/// <param name="SafeMessage">Thông điệp an toàn cho người dùng (đã loại bỏ thông tin nhạy cảm và PII).</param>
/// <param name="IsRetryEligible">Cho biết tác vụ có đủ điều kiện để người dùng bấm thử lại hay không.</param>
/// <param name="ActionableReason">Lời khuyên hoặc hướng dẫn hành động thân thiện cho người dùng dựa trên mã lỗi.</param>
/// <param name="AttemptCount">Tổng số lượt đã thực thi của tác vụ.</param>
/// <param name="RetryReason">Lý do thử lại tự động (ví dụ: thu hồi Stale Lease) hoặc thủ công gần nhất.</param>
/// <param name="ResultUrl">Đường dẫn điều hướng trực tiếp tới kết quả khi tác vụ hoàn thành.</param>
/// <param name="IsRecoveredFromFailure">Cờ cho biết tác vụ đã từng gặp sự cố nhưng được phục hồi hoặc đang trong luồng thử lại.</param>
/// <param name="UpdatedAt">Thời điểm cập nhật trạng thái gần nhất (UTC).</param>
public sealed record JobStatusDto(
    JobRef Job,
    string JobType,
    JobStatus Status,
    int? ProgressPercentage,
    string? ResultId,
    string? ErrorCode,
    string? SafeMessage,
    bool? IsRetryEligible = null,
    string? ActionableReason = null,
    int AttemptCount = 0,
    string? RetryReason = null,
    string? ResultUrl = null,
    bool IsRecoveredFromFailure = false,
    DateTimeOffset? UpdatedAt = null);
