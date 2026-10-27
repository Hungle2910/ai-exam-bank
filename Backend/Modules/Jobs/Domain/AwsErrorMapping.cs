namespace AiExamBank.Modules.Jobs.Domain;

/// <summary>
/// Danh mục hằng số mã lỗi chuẩn cho các dịch vụ AWS (Bedrock, S3), RAG Knowledge Base và sự cố hạ tầng.
/// </summary>
public static class AwsErrorCodes
{
    // --- AWS Bedrock (AI Provider) ---
    /// <summary>Mô hình AI bị bóp băng thông / vượt ngưỡng lượt gọi (HTTP 429 ThrottlingException) - Transient.</summary>
    public const string BedrockThrottled = "AWS_BEDROCK_THROTTLED";

    /// <summary>Vượt quá hạn ngạch chi phí / token hàng tháng (ServiceQuotaExceededException) - Permanent.</summary>
    public const string BedrockQuotaExceeded = "AWS_BEDROCK_QUOTA_EXCEEDED";

    /// <summary>Thiếu quyền IAM gọi mô hình Bedrock (AccessDeniedException) - Permanent.</summary>
    public const string BedrockAccessDenied = "AWS_BEDROCK_ACCESS_DENIED";

    /// <summary>Mô hình AI phản hồi quá thời gian cho phép (ModelTimeoutException) - Transient.</summary>
    public const string BedrockModelTimeout = "AWS_BEDROCK_MODEL_TIMEOUT";

    /// <summary>Mô hình AI phản hồi sai cấu trúc JSON schema câu hỏi yêu cầu - Permanent.</summary>
    public const string AiMalformedOutput = "AI_MALFORMED_OUTPUT";

    // --- Amazon S3 (Storage & Import Provider) ---
    /// <summary>Không có quyền đọc/ghi bucket hoặc object trên Amazon S3 (403 AccessDenied) - Permanent.</summary>
    public const string S3AccessDenied = "AWS_S3_ACCESS_DENIED";

    /// <summary>Không tìm thấy file tài liệu hoặc file import trên Amazon S3 (404 NoSuchKey) - Permanent.</summary>
    public const string S3FileNotFound = "AWS_S3_FILE_NOT_FOUND";

    /// <summary>Kho lưu trữ Amazon S3 bị quá tải I/O (503 SlowDown) - Transient.</summary>
    public const string S3SlowDown = "AWS_S3_SLOW_DOWN";

    /// <summary>Kết nối tới Amazon S3 bị quá thời gian chờ (Timeout) - Transient.</summary>
    public const string S3Timeout = "AWS_S3_TIMEOUT";

    // --- RAG & Knowledge Base ---
    /// <summary>Không tìm thấy đoạn trích dẫn / tài liệu kiến thức tương ứng trong Knowledge Base - Permanent.</summary>
    public const string RagRetrievalEmpty = "RAG_RETRIEVAL_EMPTY";

    /// <summary>Không tìm thấy Knowledge Base chỉ định trên AWS - Permanent.</summary>
    public const string KnowledgeBaseNotFound = "KNOWLEDGE_BASE_NOT_FOUND";

    // --- Hạ Tầng & Phục Hồi Sự Cố ---
    /// <summary>Tác vụ bị ngắt do worker sập/mất nhịp tim, đã được tự động cấp cứu và phục hồi - Transient.</summary>
    public const string StaleLeaseRecovered = "STALE_LEASE_RECOVERED";

    /// <summary>Dịch vụ AI đang tạm ngắt mạch (Circuit Breaker) do chuỗi lỗi liên tiếp - Transient.</summary>
    public const string CircuitBreakerOpen = "CIRCUIT_BREAKER_OPEN";
}

/// <summary>
/// Bộ ánh xạ mã lỗi kỹ thuật sang thông điệp hướng dẫn người dùng (Actionable Reason)
/// và làm sạch dữ liệu lỗi đảm bảo an ninh không rò rỉ stack trace hay secrets (Zero Information Leak).
/// </summary>
public static class AwsErrorMapping
{
    private static readonly Dictionary<string, string> ActionableMessages = new(StringComparer.OrdinalIgnoreCase)
    {
        [AwsErrorCodes.BedrockThrottled] = "Dịch vụ AI đang quá tải lượt yêu cầu. Hệ thống đang tự động giãn cách và thử lại.",
        [AwsErrorCodes.BedrockQuotaExceeded] = "Hạn ngạch gọi AI của trường học trong tháng đã hết. Vui lòng liên hệ Quản trị viên để nâng hạn ngạch gói.",
        [AwsErrorCodes.BedrockAccessDenied] = "Không có quyền truy cập mô hình AI. Vui lòng kiểm tra quyền hạn IAM của tài khoản trường học.",
        [AwsErrorCodes.BedrockModelTimeout] = "Mô hình AI phản hồi quá thời gian quy định (Timeout). Hệ thống sẽ tự động thử lại.",
        [AwsErrorCodes.AiMalformedOutput] = "Mô hình AI phản hồi sai định dạng cấu trúc đề thi yêu cầu. Vui lòng kiểm tra lại yêu cầu hoặc thử lại.",
        [AwsErrorCodes.S3AccessDenied] = "Không có quyền truy cập kho lưu trữ tài liệu đám mây Amazon S3.",
        [AwsErrorCodes.S3FileNotFound] = "Không tìm thấy file tài liệu hoặc danh sách câu hỏi import trên Amazon S3.",
        [AwsErrorCodes.S3SlowDown] = "Kho lưu trữ Amazon S3 đang phản hồi chậm do quá tải. Hệ thống đang tự động thử lại.",
        [AwsErrorCodes.S3Timeout] = "Kết nối tải dữ liệu từ Amazon S3 bị quá thời gian chờ. Hệ thống sẽ thử lại.",
        [AwsErrorCodes.RagRetrievalEmpty] = "Không tìm thấy tài liệu kiến thức tương ứng trong Knowledge Base để làm ngữ cảnh sinh câu hỏi.",
        [AwsErrorCodes.KnowledgeBaseNotFound] = "Không tìm thấy nguồn kiến thức (Knowledge Base) đã cấu hình trên hệ thống.",
        [AwsErrorCodes.StaleLeaseRecovered] = "Tiến trình xử lý bị mất kết nối đột ngột (Worker Timeout). Tác vụ đã được tự động cấp cứu và khôi phục.",
        [AwsErrorCodes.CircuitBreakerOpen] = "Dịch vụ AI đang tạm ngưng do phát hiện sự cố liên tiếp. Hệ thống sẽ tự động khôi phục sau ít phút.",
        ["RETRY_BUDGET_EXHAUSTED"] = "Tác vụ đã thử lại tối đa 3 lần nhưng không thành công. Vui lòng kiểm tra lại dữ liệu đầu vào.",
        ["ROW_VALIDATION_ERROR"] = "Dữ liệu dòng trong file import không hợp lệ. Vui lòng sửa lại file và nộp lại.",
        ["INVALID_CSV_SCHEMA"] = "Cấu trúc cột trong file CSV không đúng định dạng chuẩn. Vui lòng tải file mẫu để kiểm tra."
    };

    /// <summary>
    /// Lấy thông điệp hướng dẫn hành động thân thiện cho người dùng dựa trên mã lỗi.
    /// </summary>
    public static string GetActionableMessage(string? errorCode, string? fallbackMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(errorCode) && ActionableMessages.TryGetValue(errorCode, out var message))
        {
            return message;
        }

        if (!string.IsNullOrWhiteSpace(fallbackMessage))
        {
            return SanitizeForClient(fallbackMessage, errorCode);
        }

        return "Tác vụ gặp lỗi trong quá trình xử lý. Vui lòng liên hệ Quản trị viên nếu sự cố tiếp diễn.";
    }

    /// <summary>
    /// Làm sạch thông điệp lỗi trước khi trả về API hoặc UI, loại bỏ triệt để stack trace,
    /// đường dẫn file nhạy cảm, AWS ARN, Access Keys và Credentials (CWE-209).
    /// </summary>
    public static string SanitizeForClient(string? rawMessage, string? errorCode = null)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return GetActionableMessage(errorCode);
        }

        // Nếu thông điệp chứa các dấu hiệu của Stack Trace hoặc AWS internal details
        if (rawMessage.Contains("at System.", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("Exception:", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("Stack trace:", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("arn:aws:", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("AKIA", StringComparison.Ordinal) ||
            rawMessage.Contains("SecretAccessKey", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains(@"C:\", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("/home/", StringComparison.OrdinalIgnoreCase))
        {
            return GetActionableMessage(errorCode);
        }

        // Giới hạn độ dài thông điệp lỗi tối đa 250 ký tự
        return rawMessage.Length > 250 ? rawMessage[..247] + "..." : rawMessage;
    }
}
