namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Chụp nhanh (snapshot) dữ liệu đo lường độ tin cậy và hiệu năng của hệ thống Job Engine.
/// Dùng cho các endpoint quan sát, health check và giao diện người dùng (/jobs/).
/// </summary>
public record JobMetricsSnapshot
{
    /// <summary>Tổng số job đã được xếp hàng (enqueued) từ khi khởi động.</summary>
    public long TotalEnqueued { get; init; }

    /// <summary>Tổng số job đã hoàn thành thành công.</summary>
    public long TotalCompleted { get; init; }

    /// <summary>Tổng số job thất bại (Failed).</summary>
    public long TotalFailed { get; init; }

    /// <summary>Tổng số lần thử lại (gồm cả tự động và thủ công).</summary>
    public long TotalRetries { get; init; }

    /// <summary>Số lượng job hiện đang ở trạng thái chờ (Pending).</summary>
    public long ActivePending { get; init; }

    /// <summary>Số lượng job hiện đang thực thi (Running).</summary>
    public long ActiveRunning { get; init; }

    /// <summary>Số lần phát hiện và khôi phục job bị kẹt lease (Stale Lease Recovery).</summary>
    public long StaleLeaseRecoveries { get; init; }

    /// <summary>Trạng thái Circuit Breaker hiện tại theo từng nhà cung cấp (ví dụ: AWS_BEDROCK: Closed).</summary>
    public IReadOnlyDictionary<string, string> CircuitBreakerStates { get; init; } = new Dictionary<string, string>();

    /// <summary>Tổng số lỗi ghi nhận từ các provider AI/Cloud.</summary>
    public IReadOnlyDictionary<string, long> ProviderErrors { get; init; } = new Dictionary<string, long>();

    /// <summary>Thời gian thực thi trung bình (milliseconds).</summary>
    public double AverageDurationMs { get; init; }

    /// <summary>Phân vị thời gian thực thi P50 (milliseconds).</summary>
    public double P50DurationMs { get; init; }

    /// <summary>Phân vị thời gian thực thi P90 (milliseconds).</summary>
    public double P90DurationMs { get; init; }

    /// <summary>Phân vị thời gian thực thi P95 (milliseconds).</summary>
    public double P95DurationMs { get; init; }

    /// <summary>Tỷ lệ thành công tổng thể (tính theo %: 0.0 - 100.0).</summary>
    public double SuccessRatePercentage { get; init; }

    /// <summary>Tỷ lệ thất bại (tính theo %: 0.0 - 100.0).</summary>
    public double FailureRatePercentage { get; init; }

    /// <summary>Thời điểm tính toán snapshot (UTC).</summary>
    public DateTimeOffset CalculatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Giao diện ghi nhận và phát telemetry metrics cho Job Engine theo chuẩn System.Diagnostics.Metrics.
/// Tuân thủ quy định Low-cardinality dimensions: Không ghi nhận user ID, question ID, token hoặc prompt vào dimensions.
/// </summary>
public interface IJobMetrics
{
    /// <summary>
    /// Ghi nhận khi một job được xếp hàng mới.
    /// </summary>
    void RecordJobEnqueued(string jobType, string scopeKind);

    /// <summary>
    /// Ghi nhận khi worker bắt đầu xử lý job.
    /// </summary>
    void RecordJobStarted(string jobType);

    /// <summary>
    /// Ghi nhận khi một job hoàn thành thành công.
    /// </summary>
    void RecordJobCompleted(string jobType, double durationMs);

    /// <summary>
    /// Ghi nhận khi một job thất bại.
    /// </summary>
    void RecordJobFailed(string jobType, string errorCategory, string? errorCode, double durationMs);

    /// <summary>
    /// Ghi nhận khi một job được thử lại (tự động hoặc thủ công).
    /// </summary>
    void RecordJobRetried(string jobType, bool isManual, int attemptNumber);

    /// <summary>
    /// Ghi nhận khi phát hiện và khôi phục một stale lease.
    /// </summary>
    void RecordLeaseRecovery(string jobType);

    /// <summary>
    /// Cập nhật trạng thái của bộ ngắt mạch AI Provider (Closed, Open, HalfOpen).
    /// </summary>
    void RecordCircuitBreakerState(string provider, CircuitBreakerState state);

    /// <summary>
    /// Ghi nhận lỗi phát sinh khi gọi dịch vụ bên ngoài (AWS Bedrock, S3, v.v.).
    /// </summary>
    void RecordProviderError(string provider, string errorCode, bool isTransient);

    /// <summary>
    /// Cập nhật số lượng job đang Pending và Running hiện tại.
    /// </summary>
    void UpdateActiveGauges(long pendingCount, long runningCount);

    /// <summary>
    /// Lấy bản chụp dữ liệu đo lường tổng hợp thời gian thực.
    /// </summary>
    JobMetricsSnapshot GetSnapshot();
}

