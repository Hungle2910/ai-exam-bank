namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Giao diện tiếp nhận và đẩy yêu cầu tác vụ nền vào hàng đợi (Job Publisher).
/// Đảm bảo tính bất biến và chống trùng lặp nghiệp vụ trong phạm vi của người yêu cầu.
/// </summary>
public interface IJobPublisher
{
    /// <summary>
    /// Đưa một yêu cầu tác vụ vào hàng đợi lưu trữ và trả về mã định danh ổn định của tác vụ.
    /// </summary>
    /// <param name="request">Thông tin yêu cầu tác vụ hợp lệ.</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Tham chiếu định danh bất biến của tác vụ (JobRef).</returns>
    Task<JobRef> EnqueueAsync(JobRequest request, CancellationToken cancellationToken = default);
}
