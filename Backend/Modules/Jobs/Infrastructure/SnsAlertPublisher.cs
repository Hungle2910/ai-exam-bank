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
    private readonly Action<string>? _logAction;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAlertTimestamps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<IncidentAlert> _recentAlerts = new();
    private const int MaxRecentAlerts = 50;

    public SnsAlertPublisher(
        SnsAlertOptions? options = null,
        Action<string>? logAction = null)
    {
        _options = options ?? new SnsAlertOptions();
        _logAction = logAction;
    }

    public Task<PublishAlertResult> PublishIncidentAlertAsync(
        IncidentAlert alert,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);

        if (!_options.IsEnabled)
        {
            Log($"SNS alerts are disabled by configuration. Alert {alert.IncidentId} skipped.");
            return Task.FromResult(PublishAlertResult.Throttled("SNS alerts disabled."));
        }

        // 1. Kiểm tra Throttling để chống Alert Storm (ngoại trừ cảnh báo kiểm tra diễn tập DRILL_TEST_ALERT)
        var throttleKey = $"{alert.IncidentType}:{alert.JobType ?? "all"}:{alert.Provider ?? "all"}";
        var now = DateTimeOffset.UtcNow;

        if (alert.IncidentType != IncidentTypes.DrillTestAlert &&
            _lastAlertTimestamps.TryGetValue(throttleKey, out var lastSent))
        {
            var elapsedSeconds = (now - lastSent).TotalSeconds;
            if (elapsedSeconds < _options.ThrottleWindowSeconds)
            {
                var remaining = (int)(_options.ThrottleWindowSeconds - elapsedSeconds);
                Log($"Throttled SNS alert {alert.IncidentType} for {alert.JobType}/{alert.Provider}. Last sent {elapsedSeconds:N0}s ago. Cooldown: {remaining}s remaining.");

                return Task.FromResult(PublishAlertResult.Throttled(
                    $"Alert throttled. Cooldown active for next {remaining} seconds."));
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

        // 4. Lưu vết vào Recent Alerts Audit Queue
        _recentAlerts.Enqueue(sanitizedAlert);
        while (_recentAlerts.Count > MaxRecentAlerts && _recentAlerts.TryDequeue(out _)) { }

        // 5. Định dạng thông điệp theo chuẩn AWS SNS Payload
        var snsMessageBody = FormatSnsPayload(sanitizedAlert);

        // 6. Phát cảnh báo (Mô phỏng an toàn cho Staging/Local hoặc gọi trực tiếp)
        var messageId = $"sns-msg-{Guid.NewGuid():N}";

        Log($"🚨 [SNS ALERT PUBLISHED] [{sanitizedAlert.Severity}] {sanitizedAlert.IncidentType} - {sanitizedAlert.Title}. MessageId: {messageId}. Target Topic: {_options.TopicArn ?? "arn:aws:sns:local:mock-topic"}");
        Log($"SNS Payload:\n{snsMessageBody}");

        return Task.FromResult(PublishAlertResult.Succeeded(messageId));
    }

    public IReadOnlyList<IncidentAlert> GetRecentAlerts()
    {
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
