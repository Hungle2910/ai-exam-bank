namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Định danh bất biến và ổn định của một tác vụ ngầm (Job ID), dùng cho tra cứu trạng thái, thử lại và ghi log.
/// </summary>
public sealed record JobRef
{
    private JobRef(Guid value) => Value = value;

    /// <summary>
    /// Giá trị GUID duy nhất của tác vụ.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Tạo một phiên bản JobRef từ giá trị GUID hợp lệ.
    /// </summary>
    /// <param name="value">Mã GUID của tác vụ (không được rỗng).</param>
    /// <returns>Đối tượng JobRef mới.</returns>
    /// <exception cref="ArgumentException">Ném ra khi GUID rỗng.</exception>
    public static JobRef Create(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("A job ID is required.", nameof(value));
        return new JobRef(value);
    }
}
