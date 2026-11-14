using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Cài đặt hệ thống đo lường (Telemetry Metrics) cho Job Engine.
/// Sử dụng System.Diagnostics.Metrics để tương thích tiêu chuẩn OpenTelemetry và CloudWatch Agent,
/// đồng thời duy trì bộ nhớ trượt cục bộ để tính toán Snapshot thời gian thực cho API và UI.
/// </summary>
public class JobMetrics : IJobMetrics, IDisposable
{
    public const string MeterName = "AiExamBank.Jobs";
    public const string MeterVersion = "1.0.0";

    private readonly Meter _meter;
    private readonly Counter<long> _enqueuedCounter;
    private readonly Counter<long> _startedCounter;
    private readonly Counter<long> _completedCounter;
    private readonly Counter<long> _failedCounter;
    private readonly Counter<long> _retriedCounter;
    private readonly Counter<long> _leaseRecoveriesCounter;
    private readonly Histogram<double> _durationHistogram;
    private readonly Counter<long> _providerErrorsCounter;

    // In-memory aggregates for snapshotting
    private long _totalEnqueued;
    private long _totalStarted;
    private long _totalCompleted;
    private long _totalFailed;
    private long _totalRetries;
    private long _totalLeaseRecoveries;
    private long _activePending;
    private long _activeRunning;

    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakerStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _providerErrorCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<double> _recentDurationsMs = new();
    private const int MaxTrackedDurations = 500;

    public JobMetrics()
    {
        _meter = new Meter(MeterName, MeterVersion);

        _enqueuedCounter = _meter.CreateCounter<long>(
            "jobs_enqueued_total",
            description: "Total number of background jobs enqueued into the store.");

        _startedCounter = _meter.CreateCounter<long>(
            "jobs_started_total",
            description: "Total number of background jobs claimed and started by workers.");

        _completedCounter = _meter.CreateCounter<long>(
            "jobs_completed_total",
            description: "Total number of background jobs completed successfully.");

        _failedCounter = _meter.CreateCounter<long>(
            "jobs_failed_total",
            description: "Total number of background jobs failed.");

        _retriedCounter = _meter.CreateCounter<long>(
            "job_attempts_total",
            description: "Total number of job execution attempts and retries.");

        _leaseRecoveriesCounter = _meter.CreateCounter<long>(
            "job_lease_recoveries_total",
            description: "Total number of expired stale worker leases recovered.");

        _durationHistogram = _meter.CreateHistogram<double>(
            "job_processing_seconds",
            unit: "s",
            description: "Execution duration of background jobs.");

        _providerErrorsCounter = _meter.CreateCounter<long>(
            "ai_provider_errors_total",
            description: "Total number of errors encountered when invoking AI/Cloud providers.");

        _meter.CreateObservableGauge(
            "jobs_pending",
            () => Interlocked.Read(ref _activePending),
            description: "Current number of jobs in Pending state waiting for a worker.");

        _meter.CreateObservableGauge(
            "jobs_running",
            () => Interlocked.Read(ref _activeRunning),
            description: "Current number of jobs in Running state leased by workers.");
    }

    public void RecordJobEnqueued(string jobType, string scopeKind)
    {
        Interlocked.Increment(ref _totalEnqueued);
        Interlocked.Increment(ref _activePending);

        _enqueuedCounter.Add(1,
            new KeyValuePair<string, object?>("job_type", SanitizeDimension(jobType)),
            new KeyValuePair<string, object?>("scope_kind", SanitizeDimension(scopeKind)));
    }

    public void RecordJobStarted(string jobType)
    {
        Interlocked.Increment(ref _totalStarted);
        Interlocked.Decrement(ref _activePending);
        Interlocked.Increment(ref _activeRunning);

        _startedCounter.Add(1,
            new KeyValuePair<string, object?>("job_type", SanitizeDimension(jobType)));
    }

    public void RecordJobCompleted(string jobType, double durationMs)
    {
        Interlocked.Increment(ref _totalCompleted);
        Interlocked.Decrement(ref _activeRunning);

        TrackDuration(durationMs);

        _completedCounter.Add(1,
            new KeyValuePair<string, object?>("job_type", SanitizeDimension(jobType)));

        _durationHistogram.Record(durationMs / 1000.0,
            new KeyValuePair<string, object?>("job_type", SanitizeDimension(jobType)),
            new KeyValuePair<string, object?>("status", "Completed"));
    }

    public void RecordJobFailed(string jobType, string errorCategory, string? errorCode, double durationMs)
    {
        Interlocked.Increment(ref _totalFailed);
        Interlocked.Decrement(ref _activeRunning);

        TrackDuration(durationMs);

        _failedCounter.Add(1,
            new KeyValuePair<string, object?>("job_type", SanitizeDimension(jobType)),
            new KeyValuePair<string, object?>("error_category", SanitizeDimension(errorCategory)),
            new KeyValuePair<string, object?>("error_code", SanitizeDimension(errorCode ?? "UNKNOWN")));

        _durationHistogram.Record(durationMs / 1000.0,
            new KeyValuePair<string, object?>("job_type", SanitizeDimension(jobType)),
            new KeyValuePair<string, object?>("status", "Failed"));
    }

    public void RecordJobRetried(string jobType, bool isManual, int attemptNumber)
    {
        Interlocked.Increment(ref _totalRetries);

        _retriedCounter.Add(1,
            new KeyValuePair<string, object?>("job_type", SanitizeDimension(jobType)),
            new KeyValuePair<string, object?>("is_manual", isManual),
            new KeyValuePair<string, object?>("attempt", attemptNumber));
    }

    public void RecordLeaseRecovery(string jobType)
    {
        Interlocked.Increment(ref _totalLeaseRecoveries);

        _leaseRecoveriesCounter.Add(1,
            new KeyValuePair<string, object?>("job_type", SanitizeDimension(jobType)));
    }

    public void RecordCircuitBreakerState(string provider, CircuitBreakerState state)
    {
        var cleanProvider = SanitizeDimension(provider);
        _circuitBreakerStates[cleanProvider] = state;
    }

    public void RecordProviderError(string provider, string errorCode, bool isTransient)
    {
        var cleanProvider = SanitizeDimension(provider);
        var cleanCode = SanitizeDimension(errorCode);

        _providerErrorCounts.AddOrUpdate(cleanCode, 1, (_, current) => current + 1);

        _providerErrorsCounter.Add(1,
            new KeyValuePair<string, object?>("provider", cleanProvider),
            new KeyValuePair<string, object?>("error_code", cleanCode),
            new KeyValuePair<string, object?>("is_transient", isTransient));
    }

    public void UpdateActiveGauges(long pendingCount, long runningCount)
    {
        Interlocked.Exchange(ref _activePending, Math.Max(0, pendingCount));
        Interlocked.Exchange(ref _activeRunning, Math.Max(0, runningCount));
    }

    public JobMetricsSnapshot GetSnapshot()
    {
        var durations = _recentDurationsMs.ToArray();
        Array.Sort(durations);

        double avg = 0;
        double p50 = 0;
        double p90 = 0;
        double p95 = 0;

        if (durations.Length > 0)
        {
            double sum = 0;
            foreach (var d in durations) sum += d;
            avg = Math.Round(sum / durations.Length, 2);

            p50 = Math.Round(GetPercentile(durations, 0.50), 2);
            p90 = Math.Round(GetPercentile(durations, 0.90), 2);
            p95 = Math.Round(GetPercentile(durations, 0.95), 2);
        }

        var completed = Interlocked.Read(ref _totalCompleted);
        var failed = Interlocked.Read(ref _totalFailed);
        var terminalTotal = completed + failed;

        double successRate = terminalTotal > 0 ? Math.Round((double)completed / terminalTotal * 100.0, 1) : 100.0;
        double failureRate = terminalTotal > 0 ? Math.Round((double)failed / terminalTotal * 100.0, 1) : 0.0;

        var cbDict = _circuitBreakerStates.ToDictionary(k => k.Key, v => v.Value.ToString());
        var errDict = _providerErrorCounts.ToDictionary(k => k.Key, v => v.Value);

        return new JobMetricsSnapshot
        {
            TotalEnqueued = Interlocked.Read(ref _totalEnqueued),
            TotalCompleted = completed,
            TotalFailed = failed,
            TotalRetries = Interlocked.Read(ref _totalRetries),
            ActivePending = Math.Max(0, Interlocked.Read(ref _activePending)),
            ActiveRunning = Math.Max(0, Interlocked.Read(ref _activeRunning)),
            StaleLeaseRecoveries = Interlocked.Read(ref _totalLeaseRecoveries),
            CircuitBreakerStates = cbDict,
            ProviderErrors = errDict,
            AverageDurationMs = avg,
            P50DurationMs = p50,
            P90DurationMs = p90,
            P95DurationMs = p95,
            SuccessRatePercentage = successRate,
            FailureRatePercentage = failureRate,
            CalculatedAt = DateTimeOffset.UtcNow
        };
    }

    private void TrackDuration(double durationMs)
    {
        if (durationMs < 0) return;
        _recentDurationsMs.Enqueue(durationMs);
        while (_recentDurationsMs.Count > MaxTrackedDurations && _recentDurationsMs.TryDequeue(out _)) { }
    }

    private static double GetPercentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0) return 0;
        if (sortedValues.Length == 1) return sortedValues[0];

        double rank = percentile * (sortedValues.Length - 1);
        int low = (int)Math.Floor(rank);
        int high = (int)Math.Ceiling(rank);
        if (low == high) return sortedValues[low];

        double weight = rank - low;
        return sortedValues[low] * (1.0 - weight) + sortedValues[high] * weight;
    }

    private static string SanitizeDimension(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "none";
        // Bounded low-cardinality: Chỉ giữ chữ cái, số, gạch dưới, gạch ngang, tối đa 32 ký tự
        var chars = input.Trim().Take(32).Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray();
        return chars.Length == 0 ? "unknown" : new string(chars);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}

