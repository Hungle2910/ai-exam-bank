namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Bản tổng hợp số liệu thống kê được tính toán trực tiếp từ các thực thể Job và Attempt bền vững trong IJobStore.
/// Đảm bảo tính nhất quán (Consistent View) giữa các tiến trình độc lập (API Host và Worker Host).
/// </summary>
public sealed record JobStoreMetricsSummary
{
    /// <summary>Tổng số lượng tác vụ từng được đưa vào hàng đợi.</summary>
    public long TotalJobs { get; init; }

    /// <summary>Số lượng tác vụ hiện đang ở trạng thái Pending chờ Worker nhận việc.</summary>
    public long PendingJobs { get; init; }

    /// <summary>Số lượng tác vụ hiện đang ở trạng thái Running do Worker xử lý.</summary>
    public long RunningJobs { get; init; }

    /// <summary>Số lượng tác vụ đã hoàn thành thành công (Completed).</summary>
    public long CompletedJobs { get; init; }

    /// <summary>Số lượng tác vụ đã thất bại vĩnh viễn (Failed).</summary>
    public long FailedJobs { get; init; }

    /// <summary>Số lượng tác vụ đã bị hủy (Cancelled).</summary>
    public long CancelledJobs { get; init; }

    /// <summary>Tổng số lượt thực thi (Attempts) được ghi nhận trong kho lưu trữ.</summary>
    public long TotalAttempts { get; init; }

    /// <summary>Số lượt chạy được ghi nhận là cấp cứu lease quá hạn (Stale Lease Recoveries).</summary>
    public long StaleRecoveries { get; init; }

    /// <summary>Thời gian xử lý trung bình của các lượt chạy đã hoàn tất (mili-giây).</summary>
    public double AverageDurationMs { get; init; }

    /// <summary>Phân vị P50 thời gian xử lý (mili-giây).</summary>
    public double P50DurationMs { get; init; }

    /// <summary>Phân vị P90 thời gian xử lý (mili-giây).</summary>
    public double P90DurationMs { get; init; }

    /// <summary>Phân vị P95 thời gian xử lý (mili-giây).</summary>
    public double P95DurationMs { get; init; }

    /// <summary>Tỷ lệ thành công (0 - 100%).</summary>
    public double SuccessRatePercentage { get; init; }

    /// <summary>Tỷ lệ thất bại (0 - 100%).</summary>
    public double FailureRatePercentage { get; init; }
}
