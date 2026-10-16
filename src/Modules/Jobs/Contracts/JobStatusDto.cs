namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Bản chiếu trạng thái an toàn của tác vụ ngầm phục vụ hiển thị trên Status UI và phản hồi API.
/// Tiến độ đo lường bằng null nếu Handler không đo lường được (không sinh tiến độ giả).
/// </summary>
/// <param name="Job">Tham chiếu định danh bất biến của tác vụ.</param>
/// <param name="Status">Trạng thái vòng đời hiện tại của tác vụ.</param>
/// <param name="ProgressPercentage">Phần trăm tiến độ thực tế (0 - 100%) hoặc null nếu không đo lường được.</param>
/// <param name="ResultId">Mã định danh kết quả sinh ra nếu tác vụ thành công.</param>
/// <param name="ErrorCode">Mã lỗi an toàn nếu tác vụ thất bại.</param>
/// <param name="SafeMessage">Thông điệp an toàn cho người dùng (đã loại bỏ thông tin nhạy cảm và PII).</param>
public sealed record JobStatusDto(
    JobRef Job,
    JobStatus Status,
    int? ProgressPercentage,
    string? ResultId,
    string? ErrorCode,
    string? SafeMessage);
