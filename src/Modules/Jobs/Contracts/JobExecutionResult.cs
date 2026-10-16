namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Kết quả thực thi do Job Handler báo cáo lại cho Worker sau khi hoàn thành lượt chạy (attempt).
/// </summary>
/// <param name="Succeeded">Đánh dấu tác vụ chạy thành công (true) hay thất bại (false).</param>
/// <param name="ResultId">Mã định danh tài nguyên sinh ra (ví dụ: ID đề thi vừa tạo, ID lô import) để hiển thị nút [Xem kết quả].</param>
/// <param name="ErrorCode">Mã lỗi an toàn (ví dụ: VALIDATION_FAILED, BEDROCK_TIMEOUT).</param>
/// <param name="SafeErrorMessage">Thông điệp lỗi đã được lọc/redact an toàn cho người dùng (tuyệt đối không in stack trace hay lộ secret/đáp án).</param>
/// <param name="IsTransient">Đánh dấu lỗi tạm thời (true: đứt mạng, quá tải 429 để Worker tự động retry) hay lỗi vĩnh viễn (false: sai định dạng file, dừng luôn).</param>
public sealed record JobExecutionResult(
    bool Succeeded,
    string? ResultId = null,
    string? ErrorCode = null,
    string? SafeErrorMessage = null,
    bool IsTransient = false)
{
    /// <summary>Tạo kết quả báo cáo thành công kèm mã kết quả (nếu có).</summary>
    public static JobExecutionResult Success(string? resultId = null) =>
        new(true, ResultId: resultId);

    /// <summary>Tạo kết quả báo cáo thất bại kèm mã lỗi, thông điệp an toàn và cờ đánh dấu lỗi tạm thời.</summary>
    public static JobExecutionResult Failure(string errorCode, string safeErrorMessage, bool isTransient = false) =>
        new(false, ErrorCode: errorCode, SafeErrorMessage: safeErrorMessage, IsTransient: isTransient);
}
