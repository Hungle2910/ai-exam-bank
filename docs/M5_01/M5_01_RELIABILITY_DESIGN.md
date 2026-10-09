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

## 2. Telemetry Baseline (Chuẩn Ghi Log)

Để đảm bảo khả năng dò tìm lỗi (traceability), hệ thống quy định các chuẩn ghi log sau:
*   **Correlation ID:** Mọi request (API, Background Job) đều phải sinh ra hoặc truyền tiếp một `CorrelationId`. ID này phải được attach vào mọi dòng log liên quan đến request đó.
*   **Error Taxonomy (Phân loại lỗi):**
    *   `TransientError`: Lỗi tạm thời (network timeout, database lock). Sẽ được tự động Retry.
    *   `FatalError`: Lỗi nghiêm trọng (sai format dữ liệu, sai schema). Không Retry, đánh dấu Job là Failed ngay lập tức.
*   **Security & Privacy:** Tuyệt đối **không** ghi log các thông tin nhạy cảm (PII) như: Mật khẩu, Token, Email người dùng, hoặc nội dung chi tiết của đề thi (Question Text, Answer). Chỉ log Metadata (JobId, Status, ErrorCode).

## 3. Error Matrix & UI Wireframe

### 3.1. Error Matrix (Ma trận xử lý lỗi)

| Kịch bản Lỗi (Failure Scenario) | Phân loại (Type) | Xử lý ở Backend (Hệ thống ngầm) | Trạng thái hiển thị ở UI |
| :--- | :--- | :--- | :--- |
| **Mất kết nối Database tạm thời** | Transient | Job `Running` -> Ghi log cảnh báo -> Thử lại sau 1 phút. | Đang xử lý (Warning: Connection delay) |
| **Worker xử lý bị sập (Crash)** | Transient | Quá 2 phút không có Heartbeat -> Worker khác cướp Lease làm lại. | Đang xử lý (Retrying...) |
| **Amazon SageMaker API Limit** | Transient | Backoff retry (chờ 5 phút, 15 phút). Tối đa 3 lần. | Đang xử lý (Đợi AI phản hồi...) |
| **File CSV up lên sai định dạng** | Fatal | Đánh dấu Job `Failed`. Lưu `ErrorMessage`. Không Retry. | **Thất bại:** File sai định dạng. [Tải lại file] |
| **Hết 3 lần Retry vẫn lỗi** | Fatal | Đánh dấu Job `Failed`. Gửi cảnh báo cho Admin. | **Thất bại:** Hệ thống quá tải. [Bấm để Thử lại] |

### 3.2. Reliability UI Wireframe (Phác thảo Giao diện)

Giao diện người dùng (ví dụ: màn hình Quản lý tiến trình Import Câu hỏi) sẽ dựa vào API polling trạng thái của Job để hiển thị:

```text
+-----------------------------------------------------------------------------+
|  Tiến trình Background Jobs                                                 |
|-----------------------------------------------------------------------------|
|  [📄 Import File Toán Học (Job: QuestionImport)]                            |
|  Trạng thái: 🟢 Completed                  Tiến độ: [████████████] 100%   |
|-----------------------------------------------------------------------------|
|  [🤖 AI Đánh giá độ khó (Job: ExamGeneration)]                              |
|  Trạng thái: 🟡 Running (Attempt 2)        Tiến độ: [██████░░░░░░] 50%    |
|  (i) Đang kết nối lại với Amazon SageMaker...                               |
|-----------------------------------------------------------------------------|
|  [📄 Import File Lịch Sử (Job: QuestionImport)]                             |
|  Trạng thái: 🔴 Failed                     Tiến độ: [██░░░░░░░░░░] 15%    |
|  Lỗi: Cột 'Đáp án' bị trống ở dòng 45.                                      |
|  [ Nút: Xem Chi Tiết ]  [ Nút: Thử lại (Retry) ]                            |
+-----------------------------------------------------------------------------+
```

## 4. ML Dataset Spec (Khung dữ liệu cho AI)

Định nghĩa cấu trúc Rubric tiêu chuẩn (JSON Format) để chuẩn bị cho Tuần 7 (khi tích hợp Amazon SageMaker đánh giá độ khó tự động):

```json
{
  "QuestionId": "uuid-1234",
  "Subject": "Toán học",
  "Content": "Giải phương trình bậc 2: x^2 - 4x + 4 = 0",
  "Options": ["x=2", "x=1", "x=-2", "Vô nghiệm"],
  "CorrectAnswer": "x=2",
  "MlAssessment": {
    "DifficultyScore": null,  // AI sẽ điền vào (0.0 đến 1.0)
    "CognitiveLevel": null,   // AI sẽ điền vào (Remember, Understand, Apply, Analyze)
    "Tags": []                // AI tự động trích xuất từ khóa
  }
}
```
*Ghi chú: Cấu trúc này đảm bảo tách biệt phần dữ liệu thô và phần AI phân tích, giúp Job xử lý dễ dàng.*
