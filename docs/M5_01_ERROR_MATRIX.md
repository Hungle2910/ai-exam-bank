# M5-01: Reliability Error Matrix & Telemetry Baseline

## 1. Error Taxonomy & Matrix (Ma trận lỗi)

Ma trận lỗi định nghĩa các sự cố tiềm ẩn có thể xảy ra trong quá trình thực thi các Background Jobs và hướng xử lý/phục hồi cho từng loại sự cố.

| Sự cố (Failure Scenario) | Loại lỗi (Error Category) | Hướng xử lý của hệ thống (System Action) | Mức độ ảnh hưởng (Severity) |
| :--- | :--- | :--- | :--- |
| **Worker Timeout / Treo máy**<br>(Heartbeat không được cập nhật quá 2 phút) | `InfrastructureError`<br>`WorkerCrash` | Worker khác sẽ tự động "cướp" (Lease) Job này và thử chạy lại. Cập nhật trạng thái Attempt cũ thành `Abandoned` và tạo Attempt mới. | Medium |
| **Lỗi mạng kết nối tới API AI của AWS**<br>(Network Timeout, 503 Service Unavailable) | `TransientError`<br>`DependencyTimeout` | Retry tự động theo cơ chế **Bounded Retry** (Tối đa 3 lần, giãn khoảng cách 1m, 5m, 15m). Nếu hết số lần retry mà vẫn lỗi thì cập nhật trạng thái Job thành `Failed`. | Medium |
| **Lỗi logic (Ví dụ: File Excel Import bị sai định dạng, dữ liệu thiếu)** | `BusinessLogicError`<br>`ValidationError` | **Không Retry**. Đánh dấu Attempt và Job thành `Failed` ngay lập tức, lưu trữ thông tin lỗi (`ErrorDetails`) để hiển thị cho User sửa file. | Low |
| **Cơ sở dữ liệu (Database) không phản hồi** | `InfrastructureError`<br>`DatabaseOutage` | Hệ thống hàng đợi (Message Queue) sẽ giữ lại thông điệp (nếu dùng) hoặc hàm `EnqueueAsync` trả về HTTP 500 cho UI để báo người dùng thử lại sau. | High (Critical) |
| **Lỗi Out of Memory (OOM) trong quá trình xử lý** | `ResourceError`<br>`OOM` | Worker bị crash, Heartbeat dừng hoạt động. Tương tự như lỗi treo máy, Worker khác sẽ tiếp quản sau khi timeout. | High |
| **Bấm đúp Submit tạo 2 Job trùng lặp** | `ClientError`<br>`DuplicateRequest` | Nhờ ràng buộc `IdempotencyKey`, DB sẽ reject yêu cầu thứ 2. Hệ thống trả về trạng thái của Job thứ 1 đang chạy (HTTP 200 OK kèm thông tin Job). | None |

## 2. Thiết kế chuẩn Ghi Log (Telemetry Baseline)

Để đảm bảo khả năng truy vết lỗi trong một hệ thống phân tán, chuẩn Telemetry được quy định như sau:

### 2.1. Correlation ID
*   **Yêu cầu bắt buộc:** Một `CorrelationId` (Guid) phải được tạo ra từ lúc request bắt đầu ở API Gateway hoặc API Controller.
*   **Truyền tải:** `CorrelationId` phải được gắn vào Headers của tất cả các HTTP calls giữa các services, và được đính kèm vào phần Payload của Background Job để theo dõi từ API đến Worker.
*   **Logging:** Mọi log event (Information, Warning, Error) đều phải chứa thuộc tính `{CorrelationId}`.

### 2.2. Error Taxonomy (Phân loại lỗi)
Tất cả các exception cần được bọc (wrap) hoặc map về các loại chuẩn để cảnh báo (Alert) dễ dàng:
*   `ValidationException`: Lỗi do dữ liệu đầu vào. (Mức độ: Warning)
*   `DependencyException`: Lỗi do gọi external API, DB. (Mức độ: Error)
*   `SystemException`: Lỗi do OOM, NullReference. (Mức độ: Critical)

### 2.3. Security & Data Privacy
*   **Cấm Log Sensitive Data:** Tuyệt đối KHÔNG log mật khẩu, token, PII (Personally Identifiable Information).
*   **Không log nội dung Payload lớn:** Không log nội dung file Excel hay kết quả sinh đề thi AI trực tiếp ra hệ thống Log tập trung vì sẽ gây tràn dung lượng và rò rỉ đề thi. Chỉ log ID, kích thước file, hoặc metadata.
