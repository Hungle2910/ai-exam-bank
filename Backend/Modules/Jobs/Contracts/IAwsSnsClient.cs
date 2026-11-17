namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Hợp đồng trừu tượng hóa cho việc gửi thông điệp tới Amazon Simple Notification Service (Amazon SNS).
/// Giúp Module Jobs tuân thủ nguyên tắc Ports and Adapters, hoàn toàn không phụ thuộc trực tiếp vào AWSSDK.
/// </summary>
public interface IAwsSnsClient
{
    /// <summary>
    /// Xuất bản (Publish) một thông điệp tới SNS Topic.
    /// </summary>
    /// <param name="topicArn">Mã ARN của SNS Topic đích.</param>
    /// <param name="message">Nội dung thông điệp (thường là JSON định dạng sự cố).</param>
    /// <param name="subject">Tiêu đề thông điệp (tùy chọn).</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>MessageId duy nhất do Amazon SNS trả về khi nhận thông điệp.</returns>
    Task<string> PublishAsync(string topicArn, string message, string? subject = null, CancellationToken cancellationToken = default);
}
