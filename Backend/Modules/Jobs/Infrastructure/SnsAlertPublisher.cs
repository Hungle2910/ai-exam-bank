using System.Collections.Concurrent;
using System.Text.Json;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Cài đặt dịch vụ xuất bản thông báo sự cố qua Amazon SNS.
/// Cung cấp cơ chế chống bão cảnh báo (Throttling), tự động khử thông tin nhạy cảm (CWE-209),
/// và hỗ trợ mô phỏng hoàn chỉnh cho Staging/Local testing.
/// </summary>
public class SnsAlertPublisher : ISnsAlertPublisher
{
    private readonly SnsAlertOptions _options;
    private readonly IAwsSnsClient? _awsSnsClient;
    private readonly Func<IJobStore?>? _storeProvider;
    private readonly Action<string>? _logAction;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAlertTimestamps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<IncidentAlert> _recentAlerts = new();
    private const int MaxRecentAlerts = 50;

    private IJobStore? Store => _storeProvider?.Invoke();

    public SnsAlertPublisher(
        SnsAlertOptions? options = null,
        Action<string>? logAction = null)
        : this(options, null, (Func<IJobStore?>?)null, logAction)
    {
    }

    public SnsAlertPublisher(
        SnsAlertOptions? options,
        IAwsSnsClient? awsSnsClient,
        Action<string>? logAction = null)
        : this(options, awsSnsClient, (Func<IJobStore?>?)null, logAction)
    {
    }

    public SnsAlertPublisher(
        SnsAlertOptions? options,
        IAwsSnsClient? awsSnsClient,
        IJobStore? store,
        Action<string>? logAction = null)
        : this(options, awsSnsClient, store != null ? () => store : null, logAction)
    {
    }

    public SnsAlertPublisher(
        SnsAlertOptions? options,
        IAwsSnsClient? awsSnsClient,
        Func<IJobStore?>? storeProvider,
        Action<string>? logAction = null)
    {
        _options = options ?? new SnsAlertOptions();
        _awsSnsClient = awsSnsClient;
        _storeProvider = storeProvider;
        _logAction = logAction;
    }

    public async Task<PublishAlertResult> PublishIncidentAlertAsync(
        IncidentAlert alert,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);

        if (!_options.IsEnabled)
        {
            Log($"SNS alerts are disabled by configuration. Alert {alert.IncidentId} skipped.");
            return PublishAlertResult.Throttled("SNS alerts disabled.");
        }

        // 1. Kiểm tra Throttling để chống Alert Storm (kết hợp bộ nhớ tiến trình và kho lưu trữ liên tiến trình)
        var throttleKey = $"{alert.IncidentType}:{alert.JobType ?? "all"}:{alert.Provider ?? "all"}";
        var now = DateTimeOffset.UtcNow;

        DateTimeOffset? effectiveLastSent = null;
        if (_lastAlertTimestamps.TryGetValue(throttleKey, out var memLastSent))
        {
            effectiveLastSent = memLastSent;
        }

        var store = Store;
        if (store != null)
        {
            try
            {
                var recentStoreAlerts = await store.GetRecentAlertsAsync(cancellationToken);
                foreach (var storeAlert in recentStoreAlerts)
                {
                    var storeKey = $"{storeAlert.IncidentType}:{storeAlert.JobType ?? "all"}:{storeAlert.Provider ?? "all"}";
                    if (string.Equals(storeKey, throttleKey, StringComparison.OrdinalIgnoreCase) &&
                        (!effectiveLastSent.HasValue || storeAlert.TimestampUtc > effectiveLastSent.Value))
                    {
                        effectiveLastSent = storeAlert.TimestampUtc;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"[SNS-ALERT] Unable to read recent alerts from shared store for throttling check: {ex.Message}");
            }
        }

        if (effectiveLastSent.HasValue)
        {
            var elapsedSeconds = (now - effectiveLastSent.Value).TotalSeconds;

            // Xử lý giãn cách riêng cho cảnh báo diễn tập (DRILL_TEST_ALERT) để ngăn chặn spam/DoS
            if (alert.IncidentType == IncidentTypes.DrillTestAlert)
            {
                if (elapsedSeconds < _options.DrillThrottleWindowSeconds)
                {
                    var remaining = (int)(_options.DrillThrottleWindowSeconds - elapsedSeconds);
                    Log($"Throttled DRILL_TEST_ALERT. Last triggered {elapsedSeconds:N0}s ago. Drill cooldown: {remaining}s remaining.");

                    return PublishAlertResult.Throttled(
                        $"Cảnh báo diễn tập đang bị giới hạn tốc độ. Vui lòng chờ thêm {remaining} giây trước khi diễn tập lại.");
                }
            }
            else if (elapsedSeconds < _options.ThrottleWindowSeconds)
            {
                var remaining = (int)(_options.ThrottleWindowSeconds - elapsedSeconds);
                Log($"Throttled SNS alert {alert.IncidentType} for {alert.JobType}/{alert.Provider}. Last sent {elapsedSeconds:N0}s ago. Cooldown: {remaining}s remaining.");

                return PublishAlertResult.Throttled(
                    $"Alert throttled. Cooldown active for next {remaining} seconds.");
            }
        }

        // 2. Khử toàn bộ thông tin nhạy cảm khỏi Details (Stacktrace, AWS Secrets, ARN, Paths - CWE-209)
        var sanitizedDetails = AwsErrorMapping.SanitizeForClient(alert.Details);

        var sanitizedAlert = alert with
        {
            Details = sanitizedDetails,
            Environment = _options.EnvironmentName,
            TimestampUtc = now
        };

        // 3. Định dạng thông điệp theo chuẩn AWS SNS Payload
        var snsMessageBody = FormatSnsPayload(sanitizedAlert);

        // 4. Phát cảnh báo: Phân biệt rõ chế độ Mock Simulation vs Live AWS SNS.
        // Chỉ ghi nhận vào bộ nhớ và IJobStore SAU KHI gửi thành công để tránh việc lần gửi lỗi chặn (throttle) lần thử lại kế tiếp.
        if (_options.UseMockSimulation)
        {
            var mockMessageId = $"sns-mock-{Guid.NewGuid():N}";
            await RecordPublishedAlertAsync(throttleKey, now, sanitizedAlert, store, cancellationToken);
            Log($"🧪 [SNS ALERT SIMULATED] [{sanitizedAlert.Severity}] {sanitizedAlert.IncidentType} - {sanitizedAlert.Title}. MessageId: {mockMessageId}. Mode: MockSimulation. Target Topic: {_options.TopicArn ?? "arn:aws:sns:local:mock-topic"}");
            Log($"SNS Payload:\n{snsMessageBody}");
            return PublishAlertResult.Succeeded(mockMessageId);
        }

        // Chế độ phát AWS SNS thật (Live Mode):
        if (string.IsNullOrWhiteSpace(_options.TopicArn))
        {
            var error = "TopicArn is required when UseMockSimulation is false.";
            Log($"❌ [SNS ALERT FAILED] {error}");
            return PublishAlertResult.Failed(error);
        }

        if (_awsSnsClient == null)
        {
            var error = $"AWS SNS client (IAwsSnsClient) is not configured for live publishing. Target TopicArn: {_options.TopicArn}. Please register IAwsSnsClient or set UseMockSimulation = true.";
            Log($"❌ [SNS ALERT FAILED] {error}");
            return PublishAlertResult.Failed(error);
        }

        try
        {
            var liveMessageId = await _awsSnsClient.PublishAsync(
                _options.TopicArn,
                snsMessageBody,
                $"[{sanitizedAlert.Severity}] {sanitizedAlert.IncidentType}",
                cancellationToken);

            await RecordPublishedAlertAsync(throttleKey, now, sanitizedAlert, store, cancellationToken);
            Log($"🚨 [SNS ALERT PUBLISHED LIVE] [{sanitizedAlert.Severity}] {sanitizedAlert.IncidentType} - {sanitizedAlert.Title}. MessageId: {liveMessageId}. Target Topic: {_options.TopicArn}");
            return PublishAlertResult.Succeeded(liveMessageId);
        }
        catch (Exception ex)
        {
            var error = $"AWS SNS Publish failed: {ex.Message}";
            Log($"❌ [SNS ALERT FAILED] {error}");
            return PublishAlertResult.Failed(error);
        }
    }

    private async Task RecordPublishedAlertAsync(
        string throttleKey,
        DateTimeOffset timestamp,
        IncidentAlert sanitizedAlert,
        IJobStore? store,
        CancellationToken cancellationToken)
    {
        _lastAlertTimestamps[throttleKey] = timestamp;
        _recentAlerts.Enqueue(sanitizedAlert);
        while (_recentAlerts.Count > MaxRecentAlerts && _recentAlerts.TryDequeue(out _)) { }

        if (store != null)
        {
            try
            {
                await store.RecordIncidentAlertAsync(sanitizedAlert, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"[SNS-ALERT] Failed to persist incident alert to shared store: {ex.Message}");
            }
        }
    }

    public IReadOnlyList<IncidentAlert> GetRecentAlerts()
    {
        var store = Store;
        if (store != null)
        {
            try
            {
                var storeAlerts = store.GetRecentAlertsAsync().GetAwaiter().GetResult();
                if (storeAlerts.Count > 0)
                {
                    return storeAlerts;
                }
            }
            catch
            {
                // Fallback in-memory nếu kho lưu trữ gặp sự cố đọc
            }
        }

        return _recentAlerts.Reverse().ToArray();
    }

    private void Log(string message)
    {
        if (_logAction != null)
        {
            _logAction(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    private string FormatSnsPayload(IncidentAlert alert)
    {
        var payload = new
        {
            AlarmName = $"AIEB-Alarm-{alert.IncidentType}",
            AlarmDescription = alert.Title,
            NewStateValue = alert.Severity == IncidentSeverity.Critical ? "ALARM" : "WARNING",
            NewStateReason = alert.Details,
            StateChangeTime = alert.TimestampUtc.ToString("o"),
            Region = _options.Region,
            Environment = alert.Environment,
            Incident = new
            {
                alert.IncidentId,
                alert.IncidentType,
                alert.Severity,
                alert.JobType,
                alert.Provider,
                alert.ErrorCode,
                alert.RecommendedAction,
                alert.RunbookUrl
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
