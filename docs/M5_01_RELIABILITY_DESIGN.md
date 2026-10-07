# M5-01: Reliability Contracts & Telemetry Baseline

## 1. Job Contracts & Schema

### 1.1. Database Schema
Để quản lý các tác vụ chạy ngầm (Background Jobs) an toàn trong môi trường phân tán (nhiều worker), chúng ta sử dụng thiết kế 2 bảng: `BackgroundJob` và `JobAttempt`.

#### Bảng `BackgroundJob` (Bảng chính - Lưu Yêu cầu)
Lưu trữ thông tin tổng quan và trạng thái cuối cùng của một yêu cầu.

| Column Name | Type | Description |
| :--- | :--- | :--- |
| `Id` | UUID (PK) | Định danh duy nhất của Job |
| `Type` | String | Loại công việc (VD: `QuestionImport`, `ExamGeneration`) |
| `Status` | Enum | `Pending`, `Running`, `Completed`, `Failed`, `Cancelled` |
| `Payload` | JSONB | Dữ liệu đầu vào (Ví dụ: Tham số sinh đề AI, URL file Excel) |
| `Result` | JSONB | Kết quả đầu ra (Ví dụ: ID của đề thi vừa sinh, số câu lỗi) |
| `CreatedBy` | String | UserID của người tạo yêu cầu (Phục vụ Security/Phân quyền) |
| `IdempotencyKey` | String | Khóa chống trùng lặp (Ràng buộc UNIQUE) |
| `CreatedAt` | DateTime | Thời gian tạo |
| `UpdatedAt` | DateTime | Thời gian cập nhật trạng thái cuối |

#### Bảng `JobAttempt` (Bảng phụ - Lưu Lịch sử chạy)
Mỗi lần hệ thống "bắt tay" vào làm một Job, nó tạo ra 1 record ở đây. Nếu thất bại, Job có thể được Retry (thử lại), tạo ra 1 Attempt mới.

| Column Name | Type | Description |
| :--- | :--- | :--- |
| `Id` | UUID (PK) | Định danh duy nhất của lần chạy này |
| `JobId` | UUID (FK) | Liên kết với bảng `BackgroundJob` |
| `AttemptNumber` | Int | Lần thử thứ mấy (1, 2, 3...) |
| `Status` | Enum | `Started`, `Completed`, `Failed`, `Abandoned` |
| `WorkerId` | String | Tên/ID của máy chủ (server) đang thực thi job này |
| `StartedAt` | DateTime | Lúc bắt đầu xử lý |
| `HeartbeatAt` | DateTime | Dấu thời gian báo "máy chủ vẫn đang sống, chưa bị treo" |
| `FinishedAt` | DateTime | Lúc kết thúc (thành công hoặc lỗi) |
| `ErrorMessage` | String | Trích xuất lỗi ngắn gọn nếu Failed |
| `ErrorTrace` | Text | Log chi tiết mã lỗi (phục vụ lập trình viên debug) |

### 1.2. Reliability Policies (Chính sách tin cậy)

*   **Idempotency (Chống trùng lặp):** Cột `IdempotencyKey` ở bảng `BackgroundJob` có ràng buộc UNIQUE. Nếu API nhận được yêu cầu trùng Key, nó trả về Job cũ đang chạy thay vì tạo Job mới. Tránh việc user bấm đúp nút Submit tạo ra 2 đề thi giống nhau.
*   **Lease & Heartbeat (Cơ chế khóa & điểm danh):** Khi một máy chủ (Worker) nhận 1 Job, nó điền `WorkerId` vào Attempt và cập nhật `HeartbeatAt` mỗi 30 giây. Nếu một Job đang ở trạng thái `Running` nhưng `HeartbeatAt` đã không được cập nhật quá 2 phút (chứng tỏ máy chủ kia đã bị sập do cúp điện/hết RAM), một máy chủ khác được quyền "cướp" Job đó và tạo một Attempt mới để làm lại.
*   **Bounded Retry (Thử lại có giới hạn):** Nếu gặp lỗi từ hệ thống khác (như API AI của Amazon chập chờn), hệ thống tự động thử lại tối đa 3 lần (tạo ra tối đa 3 Attempts). Khoảng thời gian chờ giữa các lần thử sẽ giãn dần ra (1 phút, 5 phút, 15 phút) để tránh làm nghẽn mạng.
