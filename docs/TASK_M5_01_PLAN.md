# Kế hoạch triển khai Task M5-01 (Reliability contracts và telemetry baseline)

Tài liệu này lưu lại phân tích chi tiết cho công việc của **Gia Bảo (M5)** trong Tuần 1. Dùng tài liệu này để làm cơ sở thiết kế và code ngày mai.

## Mục tiêu (Scope: MUST)
Tạo ra nền tảng xử lý ngầm (Background Jobs) và chuẩn ghi log (Telemetry) để các module khác (Import, Generate Exam) có thể sử dụng. Chưa cần code logic nghiệp vụ thật, chỉ cần thiết kế **Schema, Contracts (Interface)** và **Quy tắc**.

---

## 4 Việc Cần Làm

### Việc 1: Thiết kế cấu trúc Database & Code cho Background Job
**1. Schema Database:**
Cần 2 bảng chính:
*   **`BackgroundJobs`**: `Id`, `JobType` (GenerateExam, ImportCsv), `Payload` (JSON), `Status` (Pending/InProgress/Completed/Failed), `RequesterId`, `IdempotencyKey`.
*   **`JobAttempts`** (Dùng cho Retry): `Id`, `JobId`, `AttemptNumber`, `WorkerId` (người thầu job), `HeartbeatAt` (nhịp tim), `ErrorDetails`.

**2. Cơ chế cốt lõi:**
*   **Lease (Khóa):** Worker nhận Job sẽ ghi tên vào `WorkerId` và đổi trạng thái thành `InProgress`.
*   **Heartbeat (Nhịp tim):** Worker đang chạy phải update `HeartbeatAt` liên tục. Nếu Worker chết (timeout quá 5 phút), hệ thống tự nhả khóa để Worker khác vào làm lại.
*   **Idempotency (Luỹ đẳng):** Dùng `IdempotencyKey` để chống tình trạng user bấm đúp tạo ra 2 job giống hệt nhau.

**3. C# Contracts (Interface mẫu):**
Các file này sẽ được tạo trong thư mục `src/` để M1 và M2 xài chung:
```csharp
// Dùng để M1, M2 ném việc vào hàng đợi
public interface IJobPublisher
{
    Task<Guid> EnqueueAsync(string jobType, string payload, string requesterId, string idempotencyKey);
}

// Dùng để lấy % tiến độ hiển thị lên UI
public interface IJobTracker
{
    Task<JobStatusDto> GetJobStatusAsync(Guid jobId, string requesterId);
}

// Logic thực thi công việc mà M1, M2 sẽ phải viết
public interface IJobHandler
{
    string JobType { get; }
    int MaxRetries { get; } 
    Task ExecuteAsync(string payload, CancellationToken cancellationToken);
}
```

### Việc 2: Thiết kế chuẩn Ghi Log (Telemetry Baseline)
*   Quy định phải truyền **Correlation ID** xuyên suốt các API và Worker.
*   Quy định **Error taxonomy** (phân loại lỗi).
*   **Security:** Tuyệt đối không log mật khẩu, token, hoặc nội dung chi tiết của đề thi.

### Việc 3: Giao diện theo dõi Job (Reliability UI)
*   Phác thảo (Wireframe) màn hình danh sách các tiến trình đang chạy ngầm, trạng thái % và nút Retry nếu lỗi.

### Việc 4: Khung dữ liệu ML (Dataset Spec)
*   Định nghĩa cấu trúc Rubric để chuẩn bị cho việc AI (SageMaker) đánh giá độ khó câu hỏi vào Tuần 7. (Ví dụ: json schema chứa text câu hỏi và điểm số độ khó).

---
**Đầu ra nghiệm thu:**
1. Code C# của các Entity và Interface trên.
2. Tài liệu Ma trận lỗi (Nếu timeout thì sao? Nếu DB sập thì sao?).
3. Wireframe UI.
4. M1 và M2 review code và đồng ý với thiết kế Interface.
