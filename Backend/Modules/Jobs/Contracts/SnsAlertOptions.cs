namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Tùy chọn cấu hình định tuyến thông báo sự cố qua Amazon SNS.
/// </summary>
public class SnsAlertOptions
{
    /// <summary>Amazon Resource Name (ARN) của SNS Topic nhận cảnh báo.</summary>
    public string? TopicArn { get; set; }

    /// <summary>Vùng AWS đặt SNS Topic (ví dụ: ap-southeast-1).</summary>
    public string Region { get; set; } = "ap-southeast-1";

    /// <summary>Bật/tắt việc phát cảnh báo qua SNS.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Sử dụng chế độ mô phỏng (Mock Simulation) cho môi trường Local / Testing / Staging khi chưa có quyền AWS thực tế.
    /// Giúp diễn tập quy trình phát thông báo và kiểm định payload mà không gây lỗi hoặc phát sinh chi phí.
    /// </summary>
    public bool UseMockSimulation { get; set; } = true;

    /// <summary>
    /// Khoảng thời gian (giây) chặn các thông báo trùng lặp cùng một loại sự cố (IncidentType) để ngăn ngừa bão thông báo (Alert Storm).
    /// Mặc định: 300 giây (5 phút).
    /// </summary>
    public int ThrottleWindowSeconds { get; set; } = 300;

    /// <summary>
    /// Khoảng thời gian giãn cách (giây) tối thiểu giữa hai lần kích hoạt cảnh báo diễn tập (DRILL_TEST_ALERT).
    /// Ngăn chặn việc gửi dồn dập hoặc spam gây nghẽn đường truyền và vượt hạn mức AWS.
    /// Mặc định: 60 giây.
    /// </summary>
    public int DrillThrottleWindowSeconds { get; set; } = 60;

    /// <summary>Tên môi trường hiện tại (Local, Staging, Production).</summary>
    public string EnvironmentName { get; set; } = "Development";

    /// <summary>
    /// AWS Access Key ID dùng để ký AWS Signature Version 4 cho request Publish tới Amazon SNS.
    /// Nếu để trống, hệ thống sẽ đọc từ biến môi trường chuẩn AWS_ACCESS_KEY_ID.
    /// </summary>
    public string? AwsAccessKeyId { get; set; }

    /// <summary>
    /// AWS Secret Access Key dùng để ký AWS Signature Version 4 cho request Publish tới Amazon SNS.
    /// Nếu để trống, hệ thống sẽ đọc từ biến môi trường chuẩn AWS_SECRET_ACCESS_KEY.
    /// </summary>
    public string? AwsSecretAccessKey { get; set; }

    /// <summary>
    /// AWS Session Token (tùy chọn) khi sử dụng thông tin xác thực tạm thời từ AWS STS hoặc IAM Role.
    /// Nếu để trống, hệ thống sẽ đọc từ biến môi trường chuẩn AWS_SESSION_TOKEN.
    /// </summary>
    public string? AwsSessionToken { get; set; }
}

