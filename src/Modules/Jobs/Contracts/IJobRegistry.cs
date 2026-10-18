namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Hợp đồng quản lý danh mục và phân phối các bộ xử lý tác vụ (Job Handlers).
/// Cho phép các module (M1 Sinh đề, M2 Import CSV, M4 RAG) đăng ký Handler của mình vào Worker.
/// </summary>
public interface IJobRegistry
{
    /// <summary>
    /// Đăng ký một Job Handler vào hệ thống.
    /// Nếu JobType đã tồn tại, handler mới sẽ ghi đè hoặc ném ngoại lệ tùy cấu hình.
    /// </summary>
    /// <param name="handler">Đối tượng xử lý tác vụ implement IJobHandler.</param>
    void Register(IJobHandler handler);

    /// <summary>
    /// Lấy bộ xử lý tương ứng với tên loại tác vụ (JobType).
    /// </summary>
    /// <param name="jobType">Tên định danh loại tác vụ (ví dụ: "ExamGeneration", "CsvImport").</param>
    /// <returns>Đối tượng IJobHandler tương ứng hoặc null nếu chưa được đăng ký.</returns>
    IJobHandler? GetHandler(string jobType);

    /// <summary>
    /// Kiểm tra xem một loại tác vụ đã có Handler xử lý trong hệ thống hay chưa.
    /// </summary>
    /// <param name="jobType">Tên định danh loại tác vụ.</param>
    /// <returns>True nếu đã đăng ký; False nếu chưa.</returns>
    bool HasHandler(string jobType);

    /// <summary>
    /// Danh sách toàn bộ các loại tác vụ (JobTypes) hiện đang được hỗ trợ trong hệ thống.
    /// </summary>
    IReadOnlyCollection<string> RegisteredTypes { get; }
}
