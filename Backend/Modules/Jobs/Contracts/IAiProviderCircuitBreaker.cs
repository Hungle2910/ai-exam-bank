namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Trạng thái của bộ ngắt mạch bảo vệ dịch vụ ngoài (Circuit Breaker State).
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>Mạch đóng (Bình thường): Mọi yêu cầu được phép gửi đến nhà cung cấp AI.</summary>
    Closed,

    /// <summary>Mạch mở (Ngắt kết nối): Phát hiện chuỗi lỗi liên tiếp, từ chối hoặc hoãn ngay các yêu cầu mới.</summary>
    Open,

    /// <summary>Mạch hé mở (Thử nghiệm): Cho phép 1 yêu cầu chạy thử nghiệm để kiểm tra xem dịch vụ đã bình phục chưa.</summary>
    HalfOpen
}

/// <summary>
/// Giao diện bộ ngắt mạch bảo vệ hệ thống trước sự cố diện rộng của các nhà cung cấp AI / Cloud (Circuit Breaker).
/// Ngăn chặn hiện tượng giông bão yêu cầu (Thundering Herd) và lãng phí chi phí API khi AWS Bedrock gặp sự cố.
/// </summary>
public interface IAiProviderCircuitBreaker
{
    /// <summary>
    /// Kiểm tra xem yêu cầu gọi đến nhà cung cấp có được phép thực thi hay không.
    /// </summary>
    /// <param name="provider">Tên định danh nhà cung cấp (mặc định: "AWS_BEDROCK").</param>
    /// <returns>True nếu được phép gọi; False nếu mạch đang ngắt (Open).</returns>
    bool CanExecute(string provider = "AWS_BEDROCK");

    /// <summary>
    /// Ghi nhận một lần gọi thành công đến nhà cung cấp để reset chuỗi lỗi về 0 hoặc chuyển mạch từ HalfOpen về Closed.
    /// </summary>
    /// <param name="provider">Tên định danh nhà cung cấp.</param>
    void RecordSuccess(string provider = "AWS_BEDROCK");

    /// <summary>
    /// Ghi nhận một lần gọi thất bại. Nếu số lần lỗi liên tiếp vượt ngưỡng (mặc định 5), mạch sẽ tự động ngắt (Open).
    /// </summary>
    /// <param name="provider">Tên định danh nhà cung cấp.</param>
    /// <param name="errorCode">Mã lỗi trả về từ nhà cung cấp.</param>
    void RecordFailure(string provider = "AWS_BEDROCK", string? errorCode = null);

    /// <summary>
    /// Lấy trạng thái hiện tại của bộ ngắt mạch cho nhà cung cấp xác định.
    /// </summary>
    /// <param name="provider">Tên định danh nhà cung cấp.</param>
    /// <returns>Trạng thái Closed, Open, hoặc HalfOpen.</returns>
    CircuitBreakerState GetState(string provider = "AWS_BEDROCK");

    /// <summary>
    /// Thiết lập lại mạch về trạng thái ban đầu (Closed).
    /// </summary>
    /// <param name="provider">Tên định danh nhà cung cấp.</param>
    void Reset(string provider = "AWS_BEDROCK");
}
