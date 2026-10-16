namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Loại phạm vi phân quyền quản lý của tác vụ (Trường học hoặc Kỳ thi cấp Bộ).
/// </summary>
public enum JobScopeKind
{
    /// <summary>Phạm vi nội bộ của một trường học cụ thể.</summary>
    School,
    /// <summary>Phạm vi kỳ thi quy mô lớn do Bộ GD&amp;ĐT quản lý.</summary>
    MinistryExam
}

/// <summary>
/// Phạm vi phân quyền gắn với tác vụ ngầm, được lưu trữ và kiểm tra lại mỗi khi truy vấn trạng thái hoặc yêu cầu hủy.
/// </summary>
public sealed record JobScope
{
    private JobScope(JobScopeKind kind, string resourceId) => (Kind, ResourceId) = (kind, resourceId);

    /// <summary>
    /// Loại phạm vi (School hoặc MinistryExam).
    /// </summary>
    public JobScopeKind Kind { get; }

    /// <summary>
    /// Mã định danh của tài nguyên thuộc phạm vi (mã trường hoặc mã kỳ thi Bộ).
    /// </summary>
    public string ResourceId { get; }

    /// <summary>
    /// Khởi tạo một phiên bản JobScope hợp lệ.
    /// </summary>
    /// <param name="kind">Loại phạm vi phân quyền.</param>
    /// <param name="resourceId">Mã định danh tài nguyên trong phạm vi (không được rỗng).</param>
    /// <returns>Đối tượng JobScope hợp lệ.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Ném ra khi loại phạm vi không xác định.</exception>
    /// <exception cref="ArgumentException">Ném ra khi mã tài nguyên rỗng.</exception>
    public static JobScope Create(JobScopeKind kind, string resourceId)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("A scope resource ID is required.", nameof(resourceId));
        return new JobScope(kind, resourceId);
    }
}
