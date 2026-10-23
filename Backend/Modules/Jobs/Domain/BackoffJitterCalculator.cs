namespace AiExamBank.Modules.Jobs.Domain;

/// <summary>
/// Bộ tính toán thời gian lùi lũy thừa kết hợp độ lệch ngẫu nhiên (Bounded Exponential Backoff with Jitter).
/// Áp dụng theo chuẩn kiến trúc độ tin cậy AWS nhằm ngăn ngừa hiện tượng bão yêu cầu (Thundering Herd).
/// </summary>
public static class BackoffJitterCalculator
{
    /// <summary>
    /// Thời gian chờ cơ sở cho lần thử lại đầu tiên (30 giây).
    /// </summary>
    public static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Thời gian chờ tối đa trần cho một lần thử lại (15 phút).
    /// </summary>
    public static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Số lần thử lại tự động tối đa trước khi cạn ngân sách (3 lần).
    /// </summary>
    public const int DefaultMaxAttempts = 3;

    /// <summary>
    /// Tính toán khoảng thời gian chờ (TimeSpan) cho một lần thử lại cụ thể.
    /// Công thức: BaseDelay * 2^(attempt - 1), sau đó nhân với hệ số Jitter ngẫu nhiên [0.8, 1.2].
    /// </summary>
    /// <param name="attempt">Thứ tự lần thử lại (1, 2, 3...).</param>
    /// <param name="baseDelay">Thời gian cơ sở (mặc định 30s).</param>
    /// <param name="maxDelay">Thời gian trần tối đa (mặc định 15m).</param>
    /// <param name="jitterFactor">Hệ số Jitter cố định truyền vào phục vụ kiểm thử đơn vị; nếu null sẽ sinh ngẫu nhiên.</param>
    /// <returns>Khoảng thời gian cần chờ trước khi thử lại.</returns>
    public static TimeSpan CalculateDelay(
        int attempt,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        double? jitterFactor = null)
    {
        var baseMs = (baseDelay ?? DefaultBaseDelay).TotalMilliseconds;
        var maxMs = (maxDelay ?? DefaultMaxDelay).TotalMilliseconds;

        // Giới hạn attempt từ 1 trở lên
        var normalizedAttempt = Math.Max(1, attempt);

        // Tính thời gian lũy thừa: base * 2^(attempt - 1)
        // attempt = 1 -> 30s * 1 = 30s
        // attempt = 2 -> 30s * 2 = 60s
        // attempt = 3 -> 30s * 4 = 120s
        var expFactor = Math.Pow(2, normalizedAttempt - 1);
        var rawDelayMs = Math.Min(maxMs, baseMs * expFactor);

        // Sinh hệ số Jitter trong khoảng [0.8, 1.2] (biên độ +/- 20%)
        // Nếu truyền jitterFactor từ test thì ghim chặt để so khớp kết quả
        double jitter;
        if (jitterFactor.HasValue)
        {
            jitter = Math.Clamp(jitterFactor.Value, 0.8, 1.2);
        }
        else
        {
            // Random.Shared sinh giá trị trong [0.0, 1.0) -> map sang [0.8, 1.2)
            jitter = 0.8 + (Random.Shared.NextDouble() * 0.4);
        }

        var finalDelayMs = Math.Min(maxMs, rawDelayMs * jitter);
        return TimeSpan.FromMilliseconds(finalDelayMs);
    }

    /// <summary>
    /// Tính toán thời điểm AvailableAt (UTC) mà tác vụ được phép chạy lại tiếp theo.
    /// </summary>
    /// <param name="attempt">Thứ tự lần chạy.</param>
    /// <param name="fromUtc">Thời điểm mốc (mặc định là DateTimeOffset.UtcNow).</param>
    /// <param name="jitterFactor">Hệ số Jitter kiểm thử (nếu có).</param>
    /// <returns>Thời điểm UTC cho AvailableAt.</returns>
    public static DateTimeOffset CalculateAvailableAt(
        int attempt,
        DateTimeOffset? fromUtc = null,
        double? jitterFactor = null)
    {
        var now = fromUtc ?? DateTimeOffset.UtcNow;
        var delay = CalculateDelay(attempt, jitterFactor: jitterFactor);
        return now.Add(delay);
    }
}
