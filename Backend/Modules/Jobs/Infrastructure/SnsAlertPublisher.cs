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

        // 1. Kiểm tra Throttling để chống Alert Storm
        var throttleKey = $"{alert.IncidentType}:{alert.JobType ?? "all"}:{alert.Provider ?? "all"}";
        var now = DateTimeOffset.UtcNow;

        if (_lastAlertTimestamps.TryGetValue(throttleKey, out var lastSent))
        {
            var elapsedSeconds = (now - lastSent).TotalSeconds;

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

        // 3. Cập nhật timestamp lần phát gần nhất
        _lastAlertTimestamps[throttleKey] = now;

        // 4. Lưu vết vào Recent Alerts Audit Queue (cả in-memory và store bền vững liên tiến trình)
        _recentAlerts.Enqueue(sanitizedAlert);
        while (_recentAlerts.Count > MaxRecentAlerts && _recentAlerts.TryDequeue(out _)) { }
        _ = Store?.RecordIncidentAlertAsync(sanitizedAlert);

        // 5. Định dạng thông điệp theo chuẩn AWS SNS Payload
        var snsMessageBody = FormatSnsPayload(sanitizedAlert);

        // 6. Phát cảnh báo: Phân biệt rõ chế độ Mock Simulation vs Live AWS SNS
        if (_options.UseMockSimulation)
        {
            var mockMessageId = $"sns-mock-{Guid.NewGuid():N}";
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

    public IReadOnlyList<IncidentAlert> GetRecentAlerts()
    {
        if (Store != null)
        {
            try
            {
                var storeAlerts = Store.GetRecentAlertsAsync().GetAwaiter().GetResult();
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
