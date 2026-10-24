namespace AiExamBank.Modules.Jobs.Domain;

/// <summary>
/// Phân loại lỗi của tác vụ ngầm để quyết định chính sách thử lại (Retry Policy).
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Lỗi tạm thời (hạ tầng, mạng, timeout AI, quá tải rate limit) -> Có thể tự động thử lại (Retryable).
    /// </summary>
    Transient,

    /// <summary>
    /// Lỗi vĩnh viễn (dữ liệu sai, vi phạm schema, thiếu câu hỏi, phân quyền) -> Dừng ngay, không thử lại (Non-retryable).
    /// </summary>
    Permanent,

    /// <summary>
    /// Lỗi kết thúc (đã cạn hạn mức thử lại tối đa hoặc tác vụ bị hủy) -> Trạng thái cuối cùng (Terminal).
    /// </summary>
    Terminal
}

/// <summary>
/// Bộ phân loại lỗi (Error Classifier) dùng để xác định tính chất lỗi dựa trên mã lỗi (ErrorCode).
/// </summary>
public static class ErrorClassifier
{
    private static readonly HashSet<string> KnownTransientErrors = new(StringComparer.OrdinalIgnoreCase)
    {
        "BEDROCK_TIMEOUT",
        "BEDROCK_THROTTLED",
        "PROVIDER_TIMEOUT",
        "NETWORK_UNAVAILABLE",
        "CONCURRENCY_CONFLICT",
        "SOCKET_ERROR",
        "SERVICE_UNAVAILABLE",
        "HTTP_429",
        "HTTP_503",
        "LEASE_EXPIRED",
        "WORKER_CRASHED",
        "TRANSIENT_FAULT",
        "TIMEOUT",
        "UNHANDLED_EXCEPTION"
    };

    private static readonly HashSet<string> KnownPermanentErrors = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROW_VALIDATION_ERROR",
        "INVALID_CSV_SCHEMA",
        "PAYLOAD_CORRUPTED",
        "BLUEPRINT_INSUFFICIENT_QUESTIONS",
        "QUESTION_NOT_APPROVED",
        "SCOPED_ACCESS_DENIED",
        "UNAUTHORIZED_TOKEN",
        "INVALID_SCOPE",
        "IDEMPOTENCY_FINGERPRINT_MISMATCH",
        "RESOURCE_NOT_FOUND",
        "VALIDATION_FAILED",
        "ARGUMENT_ERROR",
        "SCHEMA_ERROR"
    };

    private static readonly HashSet<string> KnownTerminalErrors = new(StringComparer.OrdinalIgnoreCase)
    {
        "RETRY_BUDGET_EXHAUSTED",
        "JOB_CANCELLED",
        "JOB_ABORTED"
    };

    /// <summary>
    /// Phân loại mã lỗi thành nhóm Transient, Permanent, hoặc Terminal.
    /// Nếu mã lỗi không nằm trong danh mục, mặc định coi là Permanent để tránh lãng phí retry mù quáng.
    /// </summary>
    public static ErrorCategory Classify(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return ErrorCategory.Permanent;
        }

        if (KnownTerminalErrors.Contains(errorCode))
        {
            return ErrorCategory.Terminal;
        }

        if (KnownTransientErrors.Contains(errorCode))
        {
            return ErrorCategory.Transient;
        }

        if (KnownPermanentErrors.Contains(errorCode))
        {
            return ErrorCategory.Permanent;
        }

        // Kiểm tra hậu tố/tiền tố phổ biến
        if (errorCode.EndsWith("_TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
            errorCode.EndsWith("_THROTTLED", StringComparison.OrdinalIgnoreCase) ||
            errorCode.EndsWith("_UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorCategory.Transient;
        }

        // Mặc định các lỗi không xác định được coi là Permanent để bảo vệ hệ thống
        return ErrorCategory.Permanent;
    }

    /// <summary>
    /// Kiểm tra xem một mã lỗi có đủ điều kiện để tự động thử lại (Retryable) hay không.
    /// </summary>
    public static bool IsRetryable(string? errorCode) => Classify(errorCode) == ErrorCategory.Transient;

    /// <summary>
    /// Kiểm tra xem mã lỗi có nằm trong danh mục lỗi vĩnh viễn đã biết hay không.
    /// </summary>
    public static bool IsKnownPermanent(string? errorCode) =>
        !string.IsNullOrWhiteSpace(errorCode) && KnownPermanentErrors.Contains(errorCode);
}
