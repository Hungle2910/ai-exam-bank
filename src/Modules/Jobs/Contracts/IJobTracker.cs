namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Giao diện tra cứu và quản lý trạng thái tác vụ ngầm (Job Tracker).
/// Bắt buộc kiểm tra phân quyền phạm vi (Scope Authorization) trước khi trả về dữ liệu hoặc tiếp nhận yêu cầu hủy.
/// </summary>
public interface IJobTracker
{
    /// <summary>
    /// Lấy thông tin trạng thái an toàn của tác vụ sau khi kiểm tra người yêu cầu thuộc đúng phạm vi.
    /// </summary>
    /// <param name="job">Tham chiếu định danh tác vụ JobRef.</param>
    /// <param name="requesterId">Mã người dùng gửi yêu cầu.</param>
    /// <param name="requesterScopeId">Mã trường hoặc kỳ thi của người yêu cầu (nếu truyền sẽ kiểm tra từ chối truy cập chéo trường).</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Đối tượng JobStatusDto chứa trạng thái an toàn, tiến độ, hoặc mã lỗi đã lọc dữ liệu nhạy cảm.</returns>
    Task<JobStatusDto> GetJobStatusAsync(JobRef job, string requesterId, string? requesterScopeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yêu cầu hủy một tác vụ đang chờ (Pending) hoặc đang chạy (Running) trong phạm vi được phân quyền.
    /// </summary>
    /// <param name="job">Tham chiếu định danh tác vụ JobRef.</param>
    /// <param name="requesterId">Mã người dùng gửi yêu cầu.</param>
    /// <param name="requesterScopeId">Mã trường hoặc kỳ thi của người yêu cầu.</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>True nếu yêu cầu hủy thành công; False nếu tác vụ đã hoàn thành hoặc thất bại trước đó.</returns>
    Task<bool> CancelJobAsync(JobRef job, string requesterId, string? requesterScopeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách trạng thái các tác vụ thuộc phạm vi trường học hoặc kỳ thi Bộ.
    /// </summary>
    /// <param name="scope">Phạm vi cần lọc (School hoặc MinistryExam).</param>
    /// <param name="requesterId">Mã người dùng gửi yêu cầu.</param>
    /// <param name="statusFilter">Bộ lọc trạng thái (tuỳ chọn).</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Danh sách JobStatusDto thỏa điều kiện.</returns>
    Task<IReadOnlyList<JobStatusDto>> ListJobsAsync(JobScope scope, string requesterId, JobStatus? statusFilter = null, CancellationToken cancellationToken = default);
}
