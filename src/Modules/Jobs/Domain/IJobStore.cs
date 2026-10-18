using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Domain;

/// <summary>
/// Kết quả khi một Worker chiếm giữ (Claim) thành công quyền xử lý một tác vụ ngầm.
/// Chứa thông tin Job, lượt chạy hiện tại (Attempt) và LeaseToken độc quyền dùng cho cơ chế Fencing.
/// </summary>
/// <param name="Job">Thông tin tác vụ nền được claim.</param>
/// <param name="Attempt">Thông tin lượt thực thi hiện tại gắn với Worker.</param>
/// <param name="LeaseToken">Mã phiên độc quyền (Lease Token). Worker bắt buộc phải truyền mã này khi gia hạn Heartbeat hoặc cập nhật kết quả.</param>
public sealed record JobClaimResult(
    BackgroundJob Job,
    JobAttempt Attempt,
    Guid LeaseToken);

/// <summary>
/// Giao diện kho lưu trữ và điều phối vòng đời tác vụ ngầm bền vững (Durable Job Store).
/// Quản lý tính bất biến Idempotency, điều phối hàng đợi, cơ chế chiếm quyền thực thi nguyên tử (Atomic Lease),
/// gia hạn Heartbeat (30s), và thu hồi Lease quá hạn (2 phút) để tái thực thi theo chính sách giới hạn 3 lần thử.
/// </summary>
public interface IJobStore
{
    /// <summary>
    /// Đưa một yêu cầu tác vụ mới vào hàng đợi hoặc trả về tác vụ đã có nếu trùng lặp (Idempotent Enqueue).
    /// </summary>
    /// <param name="request">Thông tin yêu cầu tác vụ từ client.</param>
    /// <param name="requestFingerprint">Mã băm nội dung payload để kiểm tra tính toàn vẹn và chống xung đột.</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Thực thể BackgroundJob mới hoặc bản ghi hiện hữu nếu cùng fingerprint.</returns>
    /// <exception cref="JobConflictException">Ném ra nếu trùng IdempotencyKey nhưng khác Fingerprint.</exception>
    Task<BackgroundJob> EnqueueAsync(JobRequest request, string requestFingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thông tin chi tiết của một tác vụ theo Job ID.
    /// </summary>
    /// <param name="jobId">Mã định danh duy nhất của tác vụ.</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Thực thể BackgroundJob nếu tìm thấy, hoặc null nếu không tồn tại.</returns>
    Task<BackgroundJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách các tác vụ thuộc phạm vi (Scope) xác định của trường học hoặc kỳ thi Bộ.
    /// Dùng cho Status UI Dashboard và API tra cứu có phân quyền.
    /// </summary>
    /// <param name="scope">Phạm vi phân quyền (School hoặc MinistryExam).</param>
    /// <param name="statusFilter">Bộ lọc trạng thái (Pending, Running, Completed, Failed, Cancelled); để null nếu lấy tất cả.</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Danh sách tác vụ thỏa điều kiện lọc trong phạm vi.</returns>
    Task<IReadOnlyList<BackgroundJob>> ListByScopeAsync(JobScope scope, JobStatus? statusFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chiếm giữ nguyên tử (Atomic Lease Claim) một tác vụ khả dụng tiếp theo để Worker bắt đầu xử lý.
    /// Tác vụ khả dụng là tác vụ Pending có thời điểm AvailableAt đã đến (AvailableAt &lt;= UtcNow)
    /// hoặc tác vụ Running bị bỏ hoang do Worker trước đó hết hạn Lease mà không Heartbeat.
    /// </summary>
    /// <param name="workerId">Mã định danh của Worker tiến hành nhận việc.</param>
    /// <param name="leaseDuration">Thời hạn hiệu lực của Lease (mặc định 2 phút).</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Đối tượng JobClaimResult nếu nhận việc thành công, hoặc null nếu hàng đợi trống.</returns>
    Task<JobClaimResult?> ClaimNextJobAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gia hạn thời gian thuê thực thi (Heartbeat) định kỳ (mỗi 30 giây) cho Worker đang xử lý tác vụ.
    /// Cơ chế Fencing: Chỉ thành công khi tác vụ vẫn đang Running và LeaseToken khớp chính xác.
    /// </summary>
    /// <param name="jobId">Mã định danh của tác vụ.</param>
    /// <param name="leaseToken">Mã LeaseToken độc quyền được cấp khi claim.</param>
    /// <param name="leaseDuration">Khoảng thời gian gia hạn thêm tính từ thời điểm hiện tại.</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>True nếu gia hạn thành công; False nếu Worker đã bị tước quyền (Fenced) hoặc Job đã bị hủy.</returns>
    Task<bool> HeartbeatAsync(Guid jobId, Guid leaseToken, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật tiến độ phần trăm thực tế (0 - 100%) của tác vụ do Handler báo cáo.
    /// </summary>
    /// <param name="jobId">Mã định danh của tác vụ.</param>
    /// <param name="leaseToken">Mã LeaseToken hợp lệ của Worker hiện tại.</param>
    /// <param name="progressPercentage">Giá trị tiến độ thực tế (0 - 100).</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>True nếu cập nhật thành công; False nếu LeaseToken không còn hợp lệ.</returns>
    Task<bool> UpdateProgressAsync(Guid jobId, Guid leaseToken, int progressPercentage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu tác vụ hoàn thành thành công (Completed) và lưu ResultId (nếu có).
    /// Áp dụng Fencing bằng LeaseToken để ngăn chặn Worker quá hạn ghi đè kết quả.
    /// </summary>
    /// <param name="jobId">Mã định danh của tác vụ.</param>
    /// <param name="leaseToken">Mã LeaseToken hợp lệ của Worker.</param>
    /// <param name="resultId">Định danh tài nguyên nghiệp vụ được tạo ra (ví dụ: ID đề thi).</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>True nếu ghi nhận thành công; False nếu bị chặn bởi Fencing.</returns>
    Task<bool> CompleteJobAsync(Guid jobId, Guid leaseToken, string? resultId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhận tác vụ gặp lỗi và điều phối thử lại (Retry) theo chính sách M5-01:
    /// - Tối đa 3 lần chạy tổng cộng (Attempt 1, 2, 3).
    /// - Nếu là lỗi tạm thời (Transient) và số lần chạy &lt; 3:
    ///   + Sau lần 1: Hẹn giờ chạy lại sau 1 phút (AvailableAt = UtcNow + 1m).
    ///   + Sau lần 2: Hẹn giờ chạy lại sau 5 phút (AvailableAt = UtcNow + 5m).
    ///   + Trạng thái chuyển về Pending.
    /// - Nếu là lỗi nghiệp vụ (Non-transient) hoặc đã chạm trần 3 lần thử:
    ///   + Trạng thái chuyển thành Failed vĩnh viễn với mã lỗi tương ứng (ví dụ: RETRY_BUDGET_EXHAUSTED).
    /// </summary>
    /// <param name="jobId">Mã định danh của tác vụ.</param>
    /// <param name="leaseToken">Mã LeaseToken hợp lệ.</param>
    /// <param name="errorCode">Mã lỗi an toàn (ví dụ: VALIDATION_FAILED, PROVIDER_TIMEOUT).</param>
    /// <param name="safeErrorMessage">Thông điệp lỗi an toàn hiển thị cho người dùng trên Status UI.</param>
    /// <param name="isTransient">True nếu là lỗi tạm thời có thể retry; False nếu là lỗi vĩnh viễn.</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>True nếu cập nhật thành công; False nếu bị chặn bởi Fencing.</returns>
    Task<bool> FailJobAsync(Guid jobId, Guid leaseToken, string errorCode, string safeErrorMessage, bool isTransient, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi yêu cầu hủy một tác vụ (Cancellation Request).
    /// - Nếu tác vụ đang ở trạng thái Pending: Hủy ngay lập tức (chuyển sang Cancelled).
    /// - Nếu tác vụ đang ở trạng thái Running: Đánh dấu Cancelled và vô hiệu hóa LeaseToken để Worker dừng lại an toàn.
    /// - Nếu tác vụ đã Cancelled: Thao tác có tính chất Idempotent (trả về true).
    /// - Nếu tác vụ đã ở trạng thái kết thúc (Completed hoặc Failed): Không thể hủy (trả về false).
    /// </summary>
    /// <param name="jobId">Mã định danh của tác vụ.</param>
    /// <param name="requesterId">ID người gửi yêu cầu hủy để kiểm tra phân quyền.</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>True nếu tác vụ được hủy hoặc đã bị hủy trước đó; False nếu tác vụ đã hoàn tất hoặc thất bại trước đó.</returns>
    Task<bool> RequestCancellationAsync(Guid jobId, string requesterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quét và thu hồi các tác vụ đang ở trạng thái Running nhưng đã bị bỏ hoang (LeaseExpiresAt &lt; UtcNow).
    /// Tự động chuyển về Pending để chờ retry hoặc đánh dấu Failed nếu đã hết hạn mức 3 lần thử.
    /// </summary>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    /// <returns>Số lượng tác vụ đã được thu hồi và khôi phục xử lý.</returns>
    Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default);
}
