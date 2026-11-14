using System.Collections.Concurrent;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Triển khai bộ ngắt mạch bảo vệ AI Provider trong bộ nhớ (In-memory Circuit Breaker).
/// Tự động ngắt kết nối tạm thời khi phát hiện 5 lỗi liên tiếp trong khoảng thời gian xác định.
/// </summary>
public sealed class InMemoryCircuitBreaker : IAiProviderCircuitBreaker
{
    private sealed class ProviderCircuit
    {
        public CircuitBreakerState State { get; set; } = CircuitBreakerState.Closed;
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset? WindowStartedAt { get; set; }
        public DateTimeOffset? OpenedAt { get; set; }
        public bool HalfOpenProbeInFlight { get; set; }
        public DateTimeOffset? LastProbeTime { get; set; }
    }

    private readonly ConcurrentDictionary<string, ProviderCircuit> _circuits = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _failureThreshold;
    private readonly TimeSpan _coolingPeriod;
    private readonly TimeSpan _failureWindow;
    private readonly Func<DateTimeOffset> _clock;
    private readonly IJobMetrics? _metrics;
    private readonly ISnsAlertPublisher? _alertPublisher;
    private readonly object _syncRoot = new();

    public InMemoryCircuitBreaker(
        int failureThreshold = 5,
        TimeSpan? coolingPeriod = null,
        Func<DateTimeOffset>? clock = null,
        TimeSpan? failureWindow = null,
        IJobMetrics? metrics = null,
        ISnsAlertPublisher? alertPublisher = null)
    {
        _failureThreshold = Math.Max(1, failureThreshold);
        _coolingPeriod = coolingPeriod ?? TimeSpan.FromSeconds(60);
        _failureWindow = failureWindow ?? TimeSpan.FromSeconds(60);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _metrics = metrics;
        _alertPublisher = alertPublisher;
    }

    /// <inheritdoc />
    public bool CanExecute(string provider = "AWS_BEDROCK")
    {
        lock (_syncRoot)
        {
            var circuit = GetOrCreate(provider);
            var now = _clock();

            switch (circuit.State)
            {
                case CircuitBreakerState.Closed:
                    return true;

                case CircuitBreakerState.Open:
                    if (circuit.OpenedAt.HasValue && now - circuit.OpenedAt.Value >= _coolingPeriod)
                    {
                        // Hết thời gian hạ nhiệt -> chuyển sang HalfOpen để thăm dò
                        circuit.State = CircuitBreakerState.HalfOpen;
                        circuit.HalfOpenProbeInFlight = true;
                        circuit.LastProbeTime = now;
                        return true;
                    }
                    return false;

                case CircuitBreakerState.HalfOpen:
                    // Trong trạng thái HalfOpen, chỉ cho phép 1 probe duy nhất tại một thời điểm
                    if (!circuit.HalfOpenProbeInFlight)
                    {
                        circuit.HalfOpenProbeInFlight = true;
                        circuit.LastProbeTime = now;
                        return true;
                    }

                    // Nếu probe cũ bị quá hạn (worker crash hoặc canceled) quá cooling period, cho phép cấp probe mới
                    if (circuit.LastProbeTime.HasValue && now - circuit.LastProbeTime.Value >= _coolingPeriod)
                    {
                        circuit.LastProbeTime = now;
                        return true;
                    }

                    return false;

                default:
                    return true;
            }
        }
    }

    /// <inheritdoc />
    public void RecordSuccess(string provider = "AWS_BEDROCK")
    {
        lock (_syncRoot)
        {
            var circuit = GetOrCreate(provider);
            circuit.ConsecutiveFailures = 0;
            circuit.WindowStartedAt = null;
            circuit.OpenedAt = null;
            circuit.HalfOpenProbeInFlight = false;
            circuit.LastProbeTime = null;
            circuit.State = CircuitBreakerState.Closed;
            _metrics?.RecordCircuitBreakerState(provider, CircuitBreakerState.Closed);
        }
    }

    /// <inheritdoc />
    public void RecordFailure(string provider = "AWS_BEDROCK", string? errorCode = null)
    {
        IncidentAlert? alertToPublish = null;
        lock (_syncRoot)
        {
            var circuit = GetOrCreate(provider);
            var now = _clock();

            // Ghi nhận lỗi phát sinh vào metrics
            _metrics?.RecordProviderError(provider, errorCode ?? "UNKNOWN", isTransient: ErrorClassifier.Classify(errorCode) == ErrorCategory.Transient);

            // Chỉ tính vào chuỗi lỗi nếu là lỗi tạm thời (Transient) hoặc lỗi Throttled
            var category = ErrorClassifier.Classify(errorCode);
            if (category == ErrorCategory.Permanent && errorCode != AwsErrorCodes.BedrockQuotaExceeded)
            {
                // Lỗi validation hay sai schema từ phía client không kích hoạt ngắt mạch.
                // Giải phóng probe để mạch không bị kẹt ở HalfOpen.
                if (circuit.State == CircuitBreakerState.HalfOpen)
                {
                    circuit.HalfOpenProbeInFlight = false;
                }
                return;
            }

            // Cửa sổ trượt đếm lỗi liên tiếp (mặc định 60s):
            // Bắt đầu cửa sổ khi có lỗi đầu tiên, hoặc đặt lại số lỗi nếu lỗi tiếp theo xảy ra sau khi cửa sổ đã hết hạn.
            if (circuit.State == CircuitBreakerState.Closed &&
                (!circuit.WindowStartedAt.HasValue || now - circuit.WindowStartedAt.Value > _failureWindow))
            {
                circuit.ConsecutiveFailures = 0;
                circuit.WindowStartedAt = now;
            }

            circuit.ConsecutiveFailures++;

            if (circuit.State == CircuitBreakerState.HalfOpen || circuit.ConsecutiveFailures >= _failureThreshold)
            {
                var previousState = circuit.State;
                circuit.State = CircuitBreakerState.Open;
                circuit.OpenedAt = now;
                circuit.HalfOpenProbeInFlight = false;
                circuit.LastProbeTime = null;
                circuit.WindowStartedAt = null;

                _metrics?.RecordCircuitBreakerState(provider, CircuitBreakerState.Open);

                // Chuẩn bị thông báo sự cố SNS (phát ngoài lock)
                alertToPublish = new IncidentAlert
                {
                    IncidentType = IncidentTypes.CircuitBreakerOpen,
                    Severity = IncidentSeverity.Critical,
                    Title = $"AI Provider [{provider}] Circuit Breaker has Tripped OPEN",
                    Details = $"Circuit transitioned from {previousState} to OPEN after {circuit.ConsecutiveFailures} consecutive failures (Threshold: {_failureThreshold}). AI generation and import requests are paused to protect quota and prevent cascade failure. Error code: {errorCode ?? "UNKNOWN"}.",
                    Provider = provider,
                    ErrorCode = errorCode,
                    RecommendedAction = "Inspect AWS Bedrock service health, API throttling/quota status, and IAM credentials in AWS Console.",
                    RunbookUrl = "https://github.com/Hungle2910/ai-exam-bank/blob/dev/Backend/docs/M5_04/M5_04_FAILURE_DRILL_RUNBOOK.md",
                    TimestampUtc = now
                };
            }
        }

        if (alertToPublish != null && _alertPublisher != null)
        {
            _ = Task.Run(() => _alertPublisher.PublishIncidentAlertAsync(alertToPublish));
        }
    }

    /// <inheritdoc />
    public CircuitBreakerState GetState(string provider = "AWS_BEDROCK")
    {
        lock (_syncRoot)
        {
            var circuit = GetOrCreate(provider);
            var now = _clock();

            if (circuit.State == CircuitBreakerState.Open &&
                circuit.OpenedAt.HasValue &&
                now - circuit.OpenedAt.Value >= _coolingPeriod)
            {
                return CircuitBreakerState.HalfOpen;
            }

            return circuit.State;
        }
    }

    /// <inheritdoc />
    public void Reset(string provider = "AWS_BEDROCK")
    {
        lock (_syncRoot)
        {
            var circuit = GetOrCreate(provider);
            circuit.State = CircuitBreakerState.Closed;
            circuit.ConsecutiveFailures = 0;
            circuit.WindowStartedAt = null;
            circuit.OpenedAt = null;
            circuit.HalfOpenProbeInFlight = false;
            circuit.LastProbeTime = null;
        }
    }

    private ProviderCircuit GetOrCreate(string provider) =>
        _circuits.GetOrAdd(provider, _ => new ProviderCircuit());
}
