namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Siêu dữ liệu định nghĩa một yêu cầu tác vụ ngầm bền vững.
/// PayloadReference trỏ tới tham chiếu dữ liệu có kiểm soát truy cập (S3, mã batch), không chứa đề thi thô hay PII.
/// </summary>
public sealed record JobRequest
{
    private JobRequest(string jobType, string payloadReference, string requesterId, JobScope scope, string idempotencyKey, string correlationId)
        => (JobType, PayloadReference, RequesterId, Scope, IdempotencyKey, CorrelationId)
            = (jobType, payloadReference, requesterId, scope, idempotencyKey, correlationId);

    /// <summary>
    /// Loại tác vụ ngầm (ví dụ: "ExamGeneration", "CsvImport", "SampleJob").
    /// </summary>
    public string JobType { get; }

    /// <summary>
    /// Tham chiếu đến tài nguyên dữ liệu đầu vào.
    /// </summary>
    public string PayloadReference { get; }

    /// <summary>
    /// Mã định danh người dùng tạo yêu cầu tác vụ.
    /// </summary>
    public string RequesterId { get; }

    /// <summary>
    /// Phạm vi phân quyền (Trường học hoặc Kỳ thi Bộ).
    /// </summary>
    public JobScope Scope { get; }

    /// <summary>
    /// Khóa chống trùng lặp do client chỉ định.
    /// </summary>
    public string IdempotencyKey { get; }

    /// <summary>
    /// Mã liên kết vết để phục vụ quan sát và điều tra log.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Tạo một phiên bản JobRequest hợp lệ với đầy đủ ràng buộc đầu vào.
    /// </summary>
    /// <param name="jobType">Loại tác vụ ngầm.</param>
    /// <param name="payloadReference">Tham chiếu dữ liệu an toàn.</param>
    /// <param name="requesterId">Mã người yêu cầu.</param>
    /// <param name="scope">Phạm vi phân quyền.</param>
    /// <param name="idempotencyKey">Khóa chống trùng.</param>
    /// <param name="correlationId">Mã liên kết vết.</param>
    /// <returns>Đối tượng JobRequest mới.</returns>
    /// <exception cref="ArgumentException">Ném ra khi thiếu thông tin bắt buộc.</exception>
    public static JobRequest Create(string jobType, string payloadReference, string requesterId, JobScope scope, string idempotencyKey, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(jobType)) throw new ArgumentException("A job type is required.", nameof(jobType));
        if (string.IsNullOrWhiteSpace(payloadReference)) throw new ArgumentException("A payload reference is required.", nameof(payloadReference));
        if (string.IsNullOrWhiteSpace(requesterId)) throw new ArgumentException("A requester ID is required.", nameof(requesterId));
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("A correlation ID is required.", nameof(correlationId));
        return new JobRequest(jobType, payloadReference, requesterId, scope, idempotencyKey, correlationId);
    }
}
