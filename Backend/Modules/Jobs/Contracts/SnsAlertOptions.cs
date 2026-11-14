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

    /// <summary>Tên môi trường hiện tại (Local, Staging, Production).</summary>
    public string EnvironmentName { get; set; } = "Development";
}

