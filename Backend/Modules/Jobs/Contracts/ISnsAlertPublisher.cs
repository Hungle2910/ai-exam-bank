namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Mức độ nghiêm trọng của sự cố vận hành (Incident Severity).
/// </summary>
public static class IncidentSeverity
{
    public const string Info = "INFO";
    public const string Warning = "WARNING";
    public const string Critical = "CRITICAL";
}

/// <summary>
/// Các loại sự cố chuẩn hóa cần hành động (Actionable Incident Types).
/// </summary>
public static class IncidentTypes
{
    /// <summary>Bộ ngắt mạch AI Provider bị ngắt kết nối do lỗi liên tiếp.</summary>
    public const string CircuitBreakerOpen = "CIRCUIT_BREAKER_OPEN";

    /// <summary>Phát hiện job bị kẹt lease quá hạn do worker sập hoặc phân vùng mạng.</summary>
    public const string StaleLeaseDetected = "STALE_LEASE_DETECTED";

    /// <summary>Job đã cạn ngân sách retry (chạm MaxAttempts = 3) và thất bại vĩnh viễn.</summary>
    public const string RetryBudgetExhausted = "RETRY_BUDGET_EXHAUSTED";

    /// <summary>Tỷ lệ lỗi xử lý job vượt ngưỡng cho phép trong cửa sổ trượt.</summary>
    public const string HighFailureRate = "HIGH_FAILURE_RATE";

    /// <summary>Cảnh báo diễn tập sự cố có kiểm soát (Drill Test Alert).</summary>
    public const string DrillTestAlert = "DRILL_TEST_ALERT";
}

/// <summary>
/// Dữ liệu thông báo sự cố có thể hành động gửi qua Amazon SNS.
/// </summary>
public record IncidentAlert
{
    /// <summary>Mã định danh duy nhất của sự cố.</summary>
    public string IncidentId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Loại sự cố (thuộc IncidentTypes).</summary>
    public string IncidentType { get; init; } = string.Empty;

    /// <summary>Mức độ nghiêm trọng (INFO, WARNING, CRITICAL).</summary>
    public string Severity { get; init; } = IncidentSeverity.Warning;

    /// <summary>Tiêu đề ngắn gọn của sự cố.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Mô tả chi tiết sự cố (Đã được làm sạch, không chứa stacktrace, secrets hoặc PII).</summary>
    public string Details { get; init; } = string.Empty;

    /// <summary>Loại job liên quan (nếu có, ví dụ: ExamGeneration, CsvImport).</summary>
    public string? JobType { get; init; }

    /// <summary>Nhà cung cấp liên quan (nếu có, ví dụ: AWS_BEDROCK, AWS_S3).</summary>
    public string? Provider { get; init; }

    /// <summary>Mã lỗi cụ thể (nếu có).</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Hành động khuyến nghị cho đội ngũ vận hành / trực ca.</summary>
    public string RecommendedAction { get; init; } = string.Empty;

    /// <summary>Đường dẫn tài liệu quy trình xử lý sự cố (Runbook URL).</summary>
    public string RunbookUrl { get; init; } = string.Empty;

    /// <summary>Môi trường phát sinh sự cố (Development, Staging, Production).</summary>
    public string Environment { get; init; } = "Staging";

    /// <summary>Thời điểm phát sinh sự cố (UTC).</summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Kết quả xuất bản thông báo sự cố qua Amazon SNS.
/// </summary>
public record PublishAlertResult
{
    public bool Success { get; init; }
    public string? MessageId { get; init; }
    public string? Error { get; init; }
    public bool WasThrottled { get; init; }
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;

    public static PublishAlertResult Succeeded(string messageId) => new()
    {
        Success = true,
        MessageId = messageId
    };

    public static PublishAlertResult Throttled(string reason) => new()
    {
        Success = true,
        WasThrottled = true,
        Error = reason
    };

    public static PublishAlertResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// Giao diện dịch vụ định tuyến và xuất bản thông báo sự cố qua Amazon SNS.
/// </summary>
public interface ISnsAlertPublisher
{
    /// <summary>
    /// Xuất bản thông báo sự cố đến SNS Topic tương ứng.
    /// Có cơ chế throttling tự động để chống bão thông báo (Alert Storm).
    /// </summary>
    Task<PublishAlertResult> PublishIncidentAlertAsync(IncidentAlert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách lịch sử các cảnh báo đã được gửi gần đây (phục vụ Audit và giao diện UI).
    /// </summary>
    IReadOnlyList<IncidentAlert> GetRecentAlerts();
}

