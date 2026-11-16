using System.Text.RegularExpressions;
using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Trình xuất bản Amazon SNS mặc định qua HTTP REST API (Amazon SNS Query API).
/// Tuân thủ nguyên tắc Ports & Adapters mà không phụ thuộc trực tiếp vào package AWSSDK,
/// cho phép gửi thông điệp trực tiếp tới AWS SNS hoặc LocalStack qua HTTP POST chuẩn.
/// </summary>
public sealed class DefaultAwsSnsClient : IAwsSnsClient
{
    private readonly HttpClient _httpClient;
    private readonly SnsAlertOptions _options;
    private readonly Action<string>? _logAction;

    public DefaultAwsSnsClient(
        HttpClient? httpClient = null,
        SnsAlertOptions? options = null,
        Action<string>? logAction = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _options = options ?? new SnsAlertOptions();
        _logAction = logAction;
    }

    /// <inheritdoc />
    public async Task<string> PublishAsync(
        string topicArn,
        string message,
        string? subject = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicArn);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        // Xác định endpoint AWS SNS từ cấu hình vùng (mặc định ap-southeast-1)
        var region = string.IsNullOrWhiteSpace(_options.Region) ? "ap-southeast-1" : _options.Region.Trim();
        var endpointUrl = $"https://sns.{region}.amazonaws.com/";

        // Chuẩn bị các tham số cho Amazon SNS Publish Query Action theo chuẩn AWS Query API
        var formParams = new List<KeyValuePair<string, string>>
        {
            new("Action", "Publish"),
            new("Version", "2010-03-31"),
            new("TopicArn", topicArn),
            new("Message", message)
        };

        if (!string.IsNullOrWhiteSpace(subject))
        {
            // AWS SNS Subject giới hạn tối đa 100 ký tự ASCII không chứa ký tự điều khiển
            var cleanSubject = new string(subject.Take(100).Where(c => c >= 32 && c < 127).ToArray());
            if (!string.IsNullOrWhiteSpace(cleanSubject))
            {
                formParams.Add(new("Subject", cleanSubject));
            }
        }

        using var requestContent = new FormUrlEncodedContent(formParams);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = requestContent
        };

        Log($"[AWS-SNS-HTTP] Dispatching Publish request to {endpointUrl} for Topic: {topicArn}");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            Log($"[AWS-SNS-HTTP] Connection error to {endpointUrl}: {ex.Message}");
            throw;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log($"[AWS-SNS-HTTP] Failed with HTTP {response.StatusCode}: {responseBody}");
            throw new HttpRequestException(
                $"Amazon SNS request to {endpointUrl} failed with status {response.StatusCode}: {responseBody}");
        }

        // Trích xuất MessageId từ XML response: <MessageId>94f20c4a-...</MessageId>
        var match = Regex.Match(
            responseBody,
            @"<MessageId>(.*?)</MessageId>",
            RegexOptions.IgnoreCase);

        var messageId = match.Success ? match.Groups[1].Value.Trim() : $"sns-msg-{Guid.NewGuid():N}";
        Log($"[AWS-SNS-HTTP] Published successfully. MessageId: {messageId}");

        return messageId;
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
}
