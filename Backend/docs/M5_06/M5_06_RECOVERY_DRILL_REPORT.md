# Báo Cáo Diễn Tập Phục Hồi Sự Cố (M5-06 Recovery Drill Report)

- **Mã công việc:** M5-06 (`Issue #30`)
- **Phụ trách:** Gia Bảo (Role M5 — Reliability & ML Evaluation)
- **Bộ kiểm thử tự động:** `Backend/tests/Modules/Jobs.Tests/RecoveryDrillAndUxMvpTests.cs`
- **Công cụ chạy diễn tập:** `python Backend/tools/run_failure_drills.py`
- **Tệp bằng chứng JSON:** `Backend/docs/M5_06/m5_06_drill_evidence.json`

---

## 1. Tóm Tắt Kết Quả Diễn Tập (Executive Summary)

Toàn bộ **5 kịch bản sự cố chuẩn** và **1 kịch bản kiểm chứng tính nhất quán sau khi khởi động lại (`Restart Consistency`)** đều vượt qua 100% bài kiểm thử tích hợp:
- **Mất mát dữ liệu (RPO):** `0` (Không có tác vụ hoặc câu hỏi nào bị thất lạc khi Worker sập hoặc khởi động lại).
- **Nhân đôi dữ liệu khi Retry:** `0` (Cơ chế `Check-Before-Write` tái sử dụng `ResultId` tiền định cho cả `ExamGeneration` và `CsvImport`).
- **Rò rỉ thông tin nhạy cảm (`CWE-209`):** `0` (Toàn bộ AWS Access Keys, IAM ARNs, đường dẫn nội bộ và stack trace đều bị khử sạch qua `AwsErrorMapping.SanitizeForClient`).

---

## 2. Chi Tiết 5 Kịch Bản Sự Cố & Kiểm Chứng Phục Hồi

### Kịch bản 1: Worker Sập Giữa Chừng & Thu Hồi Stale Lease (`M5-06-DRILL-01`)
- **Thiết lập:** Worker 1 (`worker-crash-01`) nhận tác vụ `ExamGeneration`, cập nhật tiến độ lên `50%`, sau đó ngừng gửi tín hiệu Heartbeat (mô phỏng tiến trình bị `SIGKILL` / OOM).
- **Cơ chế xử lý:**
  1. Sau khi `LeaseExpiresAt < UtcNow`, tiến trình quét `RecoverExpiredLeasesAsync` chuyển lượt chạy #1 sang trạng thái `Abandoned`.
  2. Tác vụ được trả về `Pending` với mã `STALE_LEASE_RECOVERED` và `RetryReason = "Tự động phục hồi sau khi Worker bị mất tín hiệu Heartbeat."`.
  3. Worker 2 (`worker-healthy-02`) nhận lượt chạy #2 với `LeaseToken` mới và hoàn tất tác vụ.
  4. **Zombie Worker Fencing:** Khi Worker 1 sống lại muộn và gọi `CompleteJobAsync` bằng `LeaseToken` cũ, hệ thống từ chối ghi (`false`), bảo vệ toàn vẹn kết quả của Worker 2.
- **Hiển thị trên Jobs UX:** Huy hiệu `⚡ Phục hồi (Lượt #2)` và nút điều hướng trực tiếp `/exams/preview?snapshotId=exam-snap-<jobId>`.

### Kịch bản 2: Timeout Sau Khi Đã Ghi Kết Quả (`M5-06-DRILL-02`)
- **Thiết lập:** Tác vụ `ExamGeneration` và `CsvImport` đã ghi xong dữ liệu ở lượt chạy #1 nhưng mất kết nối mạng trước khi phản hồi `CompleteJobAsync`.
- **Cơ chế xử lý:**
  - Khi thực thi lại ở `AttemptNumber = 2`, cả `ExamGenerationJobHandler` và `CsvImportJobHandler` thực hiện kiểm tra `Check-Before-Write` và lập tức trả về `exam-snap-<jobId>` / `import-batch-<jobId>` đã tồn tại.
- **Kết quả:** Không gọi lại Bedrock lần 2, không parse hoặc chèn trùng câu hỏi vào ngân hàng.

### Kịch bản 3: Quá Tải AI Provider (`429`) & Ngắt Mạch Circuit Breaker (`M5-06-DRILL-03`)
- **Thiết lập:** Gửi 5 yêu cầu liên tiếp gặp lỗi `AWS_BEDROCK_THROTTLED` trong cửa sổ 60 giây.
- **Cơ chế xử lý:**
  1. `InMemoryCircuitBreaker` chuyển trạng thái `AWS_BEDROCK` từ `Closed` sang `Open`, phát metric EMF `ai_provider_circuit_breaker_state = 2` và gửi cảnh báo SNS `CIRCUIT_BREAKER_OPEN`.
  2. Tác vụ thứ 6 đến trong lúc mạch đang `Open` được hoãn an toàn về `Pending` và **hoàn lại `AttemptCount = 0`** (không làm hao hụt ngân sách 3 lần thử của người dùng).
  3. Sau 60 giây hạ nhiệt, mạch chuyển sang `HalfOpen`, cho phép 1 yêu cầu thăm dò (probe) đi qua và đóng mạch về `Closed` khi thành công.

### Kịch bản 4: Lỗi Nghiệp Vụ Vĩnh Viễn & Từ Chối Quyền Truy Cập (`M5-06-DRILL-04`)
- **Thiết lập:** Tác vụ `CsvImport` gặp lỗi `AWS_S3_ACCESS_DENIED` kèm thông báo ngoại lệ thô chứa khóa `AKIAIOSFODNN7EXAMPLE`, ARN và đường dẫn tệp máy chủ.
- **Cơ chế xử lý:**
  1. `ErrorClassifier` phân loại `Permanent` (`IsRetryable = false`).
  2. Tác vụ chuyển thẳng sang `Failed`, khóa nút thử lại (`IsRetryEligible = false`).
  3. Mọi thông tin nhạy cảm bị cắt bỏ hoàn toàn khỏi `SafeMessage`.
  4. Cố tình gọi `POST /api/v1/jobs/{id}/retry` bị chặn bởi `JobNotRetryEligibleException` (HTTP `409 Conflict`).

### Kịch bản 5: Người Dùng Hủy Tác Vụ & Kiểm Soát Quyền Thử Lại (`M5-06-DRILL-05`)
- **Thiết lập:** Tác vụ đang ở trạng thái `Running` tại trường `school-hn-01`.
- **Cơ chế xử lý:**
  1. Yêu cầu hủy từ trường khác (`school-hcm-02`) bị từ chối ngay lập tức (`UnauthorizedAccessException` — HTTP `403 Forbidden`).
  2. Chủ sở hữu hợp lệ hủy tác vụ $\rightarrow$ trạng thái chuyển sang `Cancelled`, lượt chạy hiện tại đóng lại an toàn.
  3. Khi chủ sở hữu bấm `Thử lại`, tác vụ chỉ quay về `Pending` trong đúng phạm vi `school-hn-01`, tuyệt đối không tự động nâng trạng thái duyệt câu hỏi hoặc chốt đề thi.

---

## 3. Kiểm Chứng Tính Nhất Quán Sau Khi Khởi Động Lại (`Restart Consistency`)

Bài kiểm thử `RestartConsistency_FileJobStore_PreservesAllJobStatesAttemptsResultLinksAndTelemetry` xác nhận:
- Sau khi hủy instance `FileJobStore` cũ và khởi tạo instance mới trên cùng tệp đĩa (mô phỏng khởi động lại API/Worker container), toàn bộ:
  - Danh sách tác vụ (`BackgroundJob`), trạng thái `Completed`, `AttemptCount`, `RetryReason`.
  - Lịch sử các lượt chạy (`Abandoned` ở lượt #1, `Completed` ở lượt #2).
  - Liên kết kết quả trực tiếp (`ResultId` và `ResultUrl`).
  - Bộ đếm `StaleLeaseRecoveries` và trạng thái `CircuitBreakerStates`.
  đều được khôi phục chính xác 100%.
