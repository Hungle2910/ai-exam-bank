using AiExamBank.Modules.Jobs.Domain;

namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Giao diện tra cứu và quản lý trạng thái tác vụ ngầm (Job Tracker).
/// Bắt buộc kiểm tra phân quyền phạm vi (Scope Authorization) trước khi trả về dữ liệu hoặc tiếp nhận yêu cầu hủy.
/// </summary>
public interface IJobTracker
{
    /// <summary>
    /// Lấy thông tin trạng thái an toàn của tác vụ sau khi xác thực người yêu cầu thuộc đúng phạm vi.
    /// </summary>
    /// <param name="job">Tham chiếu định danh tác vụ JobRef.</param>
    /// <param name="requesterId">Mã người dùng gửi yêu cầu.</param>
    /// <param name="requesterScope">Phạm vi của người yêu cầu (bắt buộc cả Kind và ResourceId để ngăn chặn truy cập trái phép chéo phạm vi).</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Đối tượng JobStatusDto chứa trạng thái an toàn, tiến độ, hoặc mã lỗi đã lọc dữ liệu nhạy cảm.</returns>
    Task<JobStatusDto> GetJobStatusAsync(JobRef job, string requesterId, JobScope requesterScope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yêu cầu hủy một tác vụ đang chờ (Pending) hoặc đang chạy (Running) trong phạm vi được phân quyền.
    /// </summary>
    /// <param name="job">Tham chiếu định danh tác vụ JobRef.</param>
    /// <param name="requesterId">Mã người dùng gửi yêu cầu.</param>
    /// <param name="requesterScope">Phạm vi của người yêu cầu (bắt buộc cả Kind và ResourceId để xác thực quyền hủy).</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>True nếu yêu cầu hủy thành công; False nếu tác vụ đã hoàn thành hoặc thất bại trước đó.</returns>
    Task<bool> CancelJobAsync(JobRef job, string requesterId, JobScope requesterScope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách trạng thái các tác vụ thuộc phạm vi trường học hoặc kỳ thi Bộ sau khi xác thực quyền truy cập phạm vi.
    /// </summary>
    /// <param name="scope">Phạm vi cần lọc (School hoặc MinistryExam).</param>
    /// <param name="requesterId">Mã người dùng gửi yêu cầu.</param>
    /// <param name="requesterScope">Phạm vi của người yêu cầu để đối chiếu với phạm vi truy vấn.</param>
    /// <param name="statusFilter">Bộ lọc trạng thái (tuỳ chọn).</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Danh sách JobStatusDto thỏa điều kiện trong phạm vi được phân quyền.</returns>
    Task<IReadOnlyList<JobStatusDto>> ListJobsAsync(JobScope scope, string requesterId, JobScope requesterScope, JobStatus? statusFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yêu cầu thử lại một tác vụ ngầm đã thất bại (Failed) hoặc bị hủy (Cancelled) trong phạm vi phân quyền.
    /// </summary>
    /// <param name="job">Tham chiếu định danh tác vụ JobRef.</param>
    /// <param name="requesterId">Mã người dùng gửi yêu cầu.</param>
    /// <param name="requesterScope">Phạm vi của người yêu cầu (phải khớp chính xác với Scope của Job).</param>
    /// <param name="reason">Lý do thử lại (tùy chọn).</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Đối tượng JobStatusDto chứa thông tin trạng thái mới sau khi kích hoạt thử lại.</returns>
    Task<JobStatusDto> RetryJobAsync(JobRef job, string requesterId, JobScope requesterScope, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách toàn bộ các lượt thực thi (Attempts History) của một tác vụ trong phạm vi phân quyền.
    /// </summary>
    /// <param name="job">Tham chiếu định danh tác vụ JobRef.</param>
    /// <param name="requesterId">Mã người dùng gửi yêu cầu.</param>
    /// <param name="requesterScope">Phạm vi của người yêu cầu.</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Danh sách các bản ghi JobAttempt.</returns>
    Task<IReadOnlyList<JobAttempt>> GetJobAttemptsAsync(JobRef job, string requesterId, JobScope requesterScope, CancellationToken cancellationToken = default);
}
