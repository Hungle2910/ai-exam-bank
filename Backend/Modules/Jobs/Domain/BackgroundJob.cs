using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Domain;

/// <summary>
/// Thực thể Domain đại diện cho một tác vụ chạy ngầm bền vững (Durable Background Job).
/// Quản lý vòng đời trạng thái, phân quyền phạm vi, khóa chống trùng lặp và siêu dữ liệu thực thi.
/// </summary>
public sealed class BackgroundJob
{
    /// <summary>
    /// Khóa chính định danh duy nhất của tác vụ (Job ID).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Loại tác vụ nghiệp vụ (ví dụ: "ExamGeneration", "CsvImport").
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Trạng thái vòng đời hiện tại của tác vụ (Pending, Running, Completed, Failed, Cancelled).
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// Tham chiếu đến dữ liệu đầu vào (ví dụ: đường dẫn S3, mã đợt import).
    /// Tuyệt đối không lưu trực tiếp đề thi, đáp án hay dữ liệu cá nhân (PII) tại đây.
    /// </summary>
    public required string PayloadReference { get; set; }

    /// <summary>
    /// Định danh tài nguyên đầu ra được sinh ra khi tác vụ thành công (ví dụ: ID đề thi vừa tạo).
    /// Dùng để hiển thị nút [Xem kết quả] trên giao diện Web UI.
    /// </summary>
    public string? ResultId { get; set; }

    /// <summary>
    /// ID của người dùng tạo tác vụ (Requester ID).
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Phạm vi phân quyền của tác vụ: thuộc về trường học (School) hoặc kỳ thi cấp Bộ (MinistryExam).
    /// Dùng để đảm bảo người dùng trường khác không xem hoặc hủy trộm tác vụ.
    /// </summary>
    public required JobScope Scope { get; set; }

    /// <summary>
    /// Khóa chống trùng lặp do client gửi lên.
    /// Bộ khóa duy nhất là: (Scope.Kind, Scope.ResourceId, CreatedBy, Type, IdempotencyKey).
    /// </summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>
    /// Mã băm (fingerprint) của nội dung yêu cầu.
    /// Cùng IdempotencyKey + cùng Fingerprint => trả về Job cũ (idempotent).
    /// Cùng IdempotencyKey + khác Fingerprint => báo lỗi xung đột (HTTP 409 Conflict).
    /// </summary>
    public required string RequestFingerprint { get; set; }

    /// <summary>
    /// Mã liên kết vết (Correlation ID) dùng để nối log giữa API, Worker và hạ tầng giám sát.
    /// </summary>
    public required string CorrelationId { get; set; }

    /// <summary>
    /// Thời điểm sớm nhất mà tác vụ được phép chạy hoặc thử lại (UTC).
    /// Dùng để hẹn giờ retry: lần 1 sau 1 phút, lần 2 sau 5 phút.
    /// </summary>
    public DateTimeOffset? AvailableAt { get; set; }

    /// <summary>
    /// Thời điểm người dùng gửi yêu cầu hủy tác vụ (UTC).
    /// </summary>
    public DateTimeOffset? CancellationRequestedAt { get; set; }

    /// <summary>
    /// Số lần đã thử thực thi tác vụ (Attempt Count). Tối đa 3 lần theo chính sách M5-01.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Phần trăm tiến độ thực tế (0 - 100%) do Handler đo lường và báo cáo về.
    /// Bằng null nếu tác vụ chưa báo tiến độ hoặc không đo lường được (không sinh % giả).
    /// </summary>
    public int? ProgressPercentage { get; set; }

    /// <summary>
    /// Mã lỗi an toàn khi tác vụ thất bại (ví dụ: VALIDATION_FAILED, RETRY_BUDGET_EXHAUSTED).
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Thông điệp lỗi đã được lọc/redact an toàn cho người dùng cuối hiển thị trên Status UI.
    /// </summary>
    public string? SafeErrorMessage { get; set; }

    /// <summary>
    /// Lý do thử lại tác vụ gần nhất (do hệ thống tự động hoặc Admin/User chỉ định).
    /// </summary>
    public string? RetryReason { get; set; }

    /// <summary>
    /// Người dùng yêu cầu thử lại thủ công (nếu có).
    /// </summary>
    public string? LastRetryRequestedBy { get; set; }

    /// <summary>
    /// Thời điểm yêu cầu thử lại thủ công gần nhất (UTC).
    /// </summary>
    public DateTimeOffset? LastRetryRequestedAt { get; set; }

    /// <summary>
    /// Thời điểm tạo bản ghi (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Thời điểm cập nhật bản ghi gần nhất (UTC).
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
