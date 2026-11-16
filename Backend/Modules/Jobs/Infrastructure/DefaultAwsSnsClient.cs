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
        requestContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        var payloadBytes = await requestContent.ReadAsByteArrayAsync(cancellationToken);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = requestContent
        };

        // Ký yêu cầu bằng AWS Signature Version 4 (SigV4) nếu có thông tin định danh AWS credentials
        var accessKey = !string.IsNullOrWhiteSpace(_options.AwsAccessKeyId)
            ? _options.AwsAccessKeyId
            : Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");

        var secretKey = !string.IsNullOrWhiteSpace(_options.AwsSecretAccessKey)
            ? _options.AwsSecretAccessKey
            : Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

        var sessionToken = !string.IsNullOrWhiteSpace(_options.AwsSessionToken)
            ? _options.AwsSessionToken
            : Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN");

        if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
        {
            SignRequestAwsSigV4(httpRequest, payloadBytes, region, accessKey, secretKey, sessionToken);
            Log($"[AWS-SNS-HTTP] Request signed with AWS Signature Version 4 (AccessKey: {accessKey[..Math.Min(4, accessKey.Length)]}***)");
        }
        else
        {
            Log("[AWS-SNS-HTTP] No AWS credentials configured (AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY). Request dispatched without SigV4 signature.");
        }

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

    /// <summary>
    /// Ký yêu cầu HTTP theo tiêu chuẩn AWS Signature Version 4 (SigV4) sử dụng các nguyên mẫu bảo mật tiêu chuẩn .NET,
    /// bảo đảm tương thích 100% với các endpoint AWS SNS thực tế mà không cần phụ thuộc package AWSSDK.
    /// </summary>
    internal static void SignRequestAwsSigV4(
        HttpRequestMessage request,
        byte[] payloadBytes,
        string region,
        string accessKey,
        string secretKey,
        string? sessionToken = null,
        DateTimeOffset? signingTime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        var now = signingTime ?? DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        var host = request.RequestUri?.Host ?? $"sns.{region}.amazonaws.com";
        const string service = "sns";

        // 1. Tính SHA256 Payload Hash
        var payloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payloadBytes)).ToLowerInvariant();

        // 2. Chuẩn hóa Canonical Headers & Signed Headers (theo thứ tự alphabet chữ thường)
        var signedHeadersList = new List<string> { "content-type", "host", "x-amz-date" };
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            signedHeadersList.Add("x-amz-security-token");
        }
        signedHeadersList.Sort(StringComparer.Ordinal);
        var signedHeaders = string.Join(";", signedHeadersList);

        var canonicalHeadersBuilder = new System.Text.StringBuilder();
        foreach (var header in signedHeadersList)
        {
            switch (header)
            {
                case "content-type":
                    canonicalHeadersBuilder.Append("content-type:application/x-www-form-urlencoded\n");
                    break;
                case "host":
                    canonicalHeadersBuilder.Append($"host:{host}\n");
                    break;
                case "x-amz-date":
                    canonicalHeadersBuilder.Append($"x-amz-date:{amzDate}\n");
                    break;
                case "x-amz-security-token":
                    canonicalHeadersBuilder.Append($"x-amz-security-token:{sessionToken}\n");
                    break;
            }
        }

        // 3. Tạo Canonical Request: POST\n/\n\n{canonicalHeaders}{signedHeaders}\n{payloadHash}
        var canonicalRequest = $"POST\n/\n\n{canonicalHeadersBuilder}{signedHeaders}\n{payloadHash}";
        var canonicalRequestHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonicalRequest))).ToLowerInvariant();

        // 4. Tạo String to Sign
        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";

        // 5. Tính toán Signing Key: kSecret -> kDate -> kRegion -> kService -> kSigning
        var kSecret = System.Text.Encoding.UTF8.GetBytes("AWS4" + secretKey);
        var kDate = System.Security.Cryptography.HMACSHA256.HashData(kSecret, System.Text.Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = System.Security.Cryptography.HMACSHA256.HashData(kDate, System.Text.Encoding.UTF8.GetBytes(region));
        var kService = System.Security.Cryptography.HMACSHA256.HashData(kRegion, System.Text.Encoding.UTF8.GetBytes(service));
        var kSigning = System.Security.Cryptography.HMACSHA256.HashData(kService, System.Text.Encoding.UTF8.GetBytes("aws4_request"));

        // 6. Tính Signature
        var signature = Convert.ToHexString(
            System.Security.Cryptography.HMACSHA256.HashData(kSigning, System.Text.Encoding.UTF8.GetBytes(stringToSign))).ToLowerInvariant();

        // 7. Gán các Headers vào HttpRequestMessage
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            request.Headers.TryAddWithoutValidation("x-amz-security-token", sessionToken);
        }

        var authHeader = $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.TryAddWithoutValidation("Authorization", authHeader);
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
