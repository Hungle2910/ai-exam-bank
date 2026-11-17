namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// DTO tổng hợp chỉ số độ tin cậy và hiệu năng hệ thống (Reliability Summary).
/// Phục vụ bảng điều khiển trực quan /jobs/ và endpoint API /api/v1/jobs/metrics/summary.
/// </summary>
public sealed record ReliabilitySummaryDto
{
    public long TotalEnqueued { get; init; }
    public long TotalCompleted { get; init; }
    public long TotalFailed { get; init; }
    public long TotalRetries { get; init; }
    public long ActivePending { get; init; }
    public long ActiveRunning { get; init; }
    public long StaleLeaseRecoveries { get; init; }
    public double SuccessRatePercentage { get; init; }
    public double FailureRatePercentage { get; init; }
    public double AverageDurationMs { get; init; }
    public double P50DurationMs { get; init; }
    public double P90DurationMs { get; init; }
    public double P95DurationMs { get; init; }
    public IReadOnlyDictionary<string, string> CircuitBreakerStates { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, long> ProviderErrors { get; init; } = new Dictionary<string, long>();
    public IReadOnlyList<IncidentAlert> RecentAlerts { get; init; } = Array.Empty<IncidentAlert>();
    public DateTimeOffset CalculatedAt { get; init; } = DateTimeOffset.UtcNow;
}

