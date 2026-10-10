using System.Threading;
using System.Threading.Tasks;

namespace AiExamBank.Contracts.BackgroundJobs
{
    /// <summary>
    /// Khuôn mẫu (Quyển công thức) bắt buộc M1/M2 phải làm theo khi muốn hệ thống ngầm chạy chức năng của họ.
    /// </summary>
    public interface IJobHandler
    {
        /// <summary>
        /// Tên loại công việc mà Handler này giải quyết (VD: "ImportCsv", "GenerateExam").
        /// Dùng để hệ thống biết "gọi đúng thợ, giao đúng việc".
        /// </summary>
        string JobType { get; }

        /// <summary>
        /// Số lần tối đa được phép chạy lại (Retry) nếu gặp sự cố. 
        /// (VD: 3 lần. Nếu lỗi quá 3 lần thì hệ thống đánh dấu Failed luôn).
        /// </summary>
        int MaxRetries { get; } 

        /// <summary>
        /// Nơi chứa code logic THỰC SỰ (nạp DB, gọi AI...).
        /// </summary>
        /// <param name="payload">Dữ liệu lấy từ Database (do EnqueueAsync ném vào lúc nãy).</param>
        /// <param name="cancellationToken">Lệnh Dừng Khẩn Cấp (Ví dụ: Server sập hoặc user bấm Hủy, hàm này phải biết để dừng lại ngay).</param>
        Task ExecuteAsync(string payload, CancellationToken cancellationToken);
    }
}
