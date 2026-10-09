using System;
using System.Threading.Tasks;

namespace AiExamBank.Contracts.BackgroundJobs
{
    /// <summary>
    /// Interface (Adapter) cho M1/M2 đẩy tác vụ nặng vào hệ thống chạy ngầm để không làm treo UI.
    /// </summary>
    public interface IJobPublisher
    {
        /// <summary>
        /// Gửi yêu cầu chạy ngầm và nhận lại Mã vé (Guid) ngay lập tức.
        /// </summary>
        /// <param name="jobType">Loại tác vụ. VD: "ImportCsv", "GenerateExam"</param>
        /// <param name="payload">Dữ liệu đầu vào (JSON). VD: Link S3 file csv, cấu hình sinh đề</param>
        /// <param name="requesterId">ID của Giáo viên thao tác (để sau này gửi Notification)</param>
        /// <param name="idempotencyKey">Khóa chống đúp lệnh (chặn user bấm submit 2 lần)</param>
        /// <returns>Mã JobId (Guid) để UI dùng tracking tiến độ (%), không phải chờ xử lý xong.</returns>
        Task<Guid> EnqueueAsync(string jobType, string payload, string requesterId, string idempotencyKey);
    }
}
