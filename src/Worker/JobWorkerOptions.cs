namespace AiExamBank.Worker;

/// <summary>
/// Các tham số cấu hình cho tiến trình chạy ngầm JobWorkerService.
/// Tuân thủ nghiêm ngặt các hạn mức thời gian và chu kỳ định nghĩa trong M5-01 / M5-02.
/// </summary>
public sealed class JobWorkerOptions
{
    /// <summary>
    /// Định danh duy nhất của Worker instance này (mặc định sinh theo tên máy và GUID).
    /// </summary>
    public string WorkerId { get; set; } = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}"[..32];

    /// <summary>
    /// Chu kỳ thăm dò hàng đợi cơ sở (mặc định 1 giây).
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Thời gian thăm dò tối đa khi hàng đợi liên tục trống (Backoff tối đa 5 giây).
    /// </summary>
    public TimeSpan MaxPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Chu kỳ gửi tín hiệu sống (Heartbeat) định kỳ khi đang xử lý tác vụ (30 giây theo M5-01).
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Thời hạn hiệu lực của một Lease độc quyền (2 phút theo M5-01).
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Chu kỳ quét thu hồi các tác vụ quá hạn lease bị bỏ hoang (30 giây).
    /// </summary>
    public TimeSpan RecoveryInterval { get; set; } = TimeSpan.FromSeconds(30);
}
