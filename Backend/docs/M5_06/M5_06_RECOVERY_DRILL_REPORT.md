# Báo Cáo Diễn Tập Phục Hồi Sự Cố (M5-06 Recovery Drill Report — Local Resilience & Simulated Handler Suite)

- **Mã công việc:** M5-06 (`Issue #30` — Partial Slice)
- **Phụ trách:** Gia Bảo (Role M5 — Reliability & ML Evaluation)
- **Bộ kiểm thử tự động:** `Backend/tests/Modules/Jobs.Tests/RecoveryDrillAndUxMvpTests.cs`
- **Công cụ chạy diễn tập:** `python Backend/tools/run_failure_drills.py`
- **Tệp bằng chứng JSON:** `Backend/docs/M5_06/m5_06_drill_evidence.json`
- **Phạm vi kiểm chứng hiện tại:** **Local Job Engine (`InMemoryJobStore` / `FileJobStore`) & Simulated Handler Contracts** *(Chưa bao gồm diễn tập trên môi trường Staging/AWS thật và chưa nối DB persistence thật của M1-06/M2-04)*.

---

## 1. Tóm Tắt Phạm Vi & Kết Quả Kiểm Chứng Cục Bộ (Executive Summary)

Báo cáo này ghi nhận kết quả chạy tự động **5 kịch bản sự cố chuẩn** và **1 kịch bản kiểm chứng tính nhất quán sau khi khởi động lại (`Restart Consistency`)** trên bộ máy Job Engine cục bộ:
- **Khôi phục tác vụ trong `IJobStore` khi Worker sập/restart:** Tác vụ đang `Running` khi hết hạn lease được thu hồi về `Pending`, bảo toàn trạng thái trong `FileJobStore` qua các lần khởi động lại tiến trình.
- **Hợp đồng `Check-Before-Write` mức Handler (Simulated):** Ở lượt thử lại (`AttemptNumber > 1`), `ExamGenerationJobHandler` và `CsvImportJobHandler` trả về lại mã kết quả tiền định mô phỏng (`exam-snap-<jobId>` và `import-batch-<jobId>`). Việc kiểm chứng chống nhân đôi bản ghi DB thực tế sẽ được thực hiện khi tích hợp xong persistence của M1-06 và M2-04.
- **Khử thông tin nhạy cảm (`CWE-209`):** Toàn bộ AWS Access Keys, IAM ARNs, đường dẫn nội bộ và stack trace trong bài kiểm thử đều được lọc sạch qua `AwsErrorMapping.SanitizeForClient`.
- **Chống chặn nhầm cảnh báo khi gửi SNS thất bại:** `SnsAlertPublisher` chỉ ghi nhận lịch sử cảnh báo vào bộ nhớ và `IJobStore` sau khi gửi thành công, đảm bảo nếu lần gửi AWS SNS gặp lỗi mạng thì lần thử lại kế tiếp từ tiến trình khác không bị chặn bởi bộ lọc chống bão (throttling).

---

## 2. Chi Tiết 5 Kịch Bản Sự Cố Đã Kiểm Chứng Cục Bộ

### Kịch bản 1: Worker Sập Giữa Chừng & Thu Hồi Stale Lease (`M5-06-DRILL-01`)
- **Thiết lập:** Worker 1 (`worker-crash-01`) nhận tác vụ `ExamGeneration`, cập nhật tiến độ lên `50%`, sau đó ngừng gửi tín hiệu Heartbeat (mô phỏng tiến trình bị dừng đột ngột).
- **Cơ chế xử lý:**
  1. Sau khi `LeaseExpiresAt < UtcNow`, `RecoverExpiredLeasesAsync` chuyển lượt chạy #1 sang trạng thái `Abandoned`.
  2. Tác vụ được trả về `Pending` với mã `STALE_LEASE_RECOVERED` và `RetryReason = "Tự động phục hồi sau khi Worker bị mất tín hiệu Heartbeat."`.
  3. Worker 2 (`worker-healthy-02`) nhận lượt chạy #2 với `LeaseToken` mới và hoàn tất tác vụ.
  4. **Zombie Worker Fencing:** Khi Worker 1 gọi `CompleteJobAsync` muộn bằng `LeaseToken` cũ, hệ thống từ chối ghi (`false`), bảo vệ kết quả của Worker 2.
- **Hiển thị trên Jobs UX:** Huy hiệu `⚡ Phục hồi (Lượt #2)` và nút `🔗 Mở KQ` điều hướng tới `/jobs/?jobId=<jobId>&resultId=exam-snap-<jobId>&jobType=ExamGeneration` để mở ngay bảng chi tiết kết quả trên trang `/jobs/`.

### Kịch bản 2: Timeout Sau Khi Ghi Kết Quả — Hợp Đồng Handler Mô Phỏng (`M5-06-DRILL-02`)
- **Thiết lập:** Mô phỏng kịch bản `simulate-timeout-after-write` cho `ExamGeneration` và `CsvImport` khi thực thi lại ở `AttemptNumber = 2`.
- **Cơ chế xử lý:**
  - Cả `ExamGenerationJobHandler` và `CsvImportJobHandler` kiểm tra nhánh `Check-Before-Write` khi `AttemptNumber > 1` và tái sử dụng mã định danh mô phỏng `exam-snap-<jobId>` / `import-batch-<jobId>`.
- **Giới hạn hiện tại:** Mới kiểm chứng hợp đồng ở tầng Handler mô phỏng; cần nối với DB transaction thật của M1-06 và M2-04 để nghiệm thu chống ghi trùng dữ liệu nghiệp vụ trên Staging.

### Kịch bản 3: Quá Tải AI Provider (`429`) & Ngắt Mạch Circuit Breaker (`M5-06-DRILL-03`)
- **Thiết lập:** Gửi 5 yêu cầu liên tiếp gặp lỗi `AWS_BEDROCK_THROTTLED` trong cửa sổ 60 giây.
- **Cơ chế xử lý:**
  1. `InMemoryCircuitBreaker` chuyển trạng thái `AWS_BEDROCK` từ `Closed` sang `Open`, phát metric `ai_provider_circuit_breaker_state = 2` và kích hoạt cảnh báo `CIRCUIT_BREAKER_OPEN`.
  2. Tác vụ thứ 6 đến trong lúc mạch đang `Open` được hoãn an toàn về `Pending` và **hoàn lại `AttemptCount = 0`** (không làm hao hụt ngân sách 3 lần thử của người dùng).
  3. Sau 60 giây hạ nhiệt, mạch chuyển sang `HalfOpen`, cho phép 1 yêu cầu thăm dò (probe) đi qua và đóng mạch về `Closed` khi thành công.

### Kịch bản 4: Lỗi Nghiệp Vụ Vĩnh Viễn & Từ Chối Quyền Truy Cập (`M5-06-DRILL-04`)
- **Thiết lập:** Tác vụ `CsvImport` gặp lỗi `AWS_S3_ACCESS_DENIED` kèm thông báo ngoại lệ thô chứa khóa `AKIAIOSFODNN7EXAMPLE`, ARN và đường dẫn tệp máy chủ.
- **Cơ chế xử lý:**
  1. `ErrorClassifier` phân loại `Permanent` (`IsRetryable = false`).
  2. Tác vụ chuyển thẳng sang `Failed`, khóa nút thử lại (`IsRetryEligible = false`).
  3. Thông tin nhạy cảm bị cắt bỏ khỏi `SafeMessage`.
  4. Cố tình gọi `POST /api/v1/jobs/{id}/retry` bị chặn bởi `JobNotRetryEligibleException` (HTTP `409 Conflict`).

### Kịch bản 5: Người Dùng Hủy Tác Vụ & Kiểm Soát Quyền Thử Lại (`M5-06-DRILL-05`)
- **Thiết lập:** Tác vụ đang ở trạng thái `Running` tại trường `school-hn-01`.
- **Cơ chế xử lý:**
  1. Yêu cầu hủy từ trường khác (`school-hcm-02`) bị từ chối (`UnauthorizedAccessException` — HTTP `403 Forbidden`).
  2. Chủ sở hữu hợp lệ hủy tác vụ $\rightarrow$ trạng thái chuyển sang `Cancelled`, lượt chạy hiện tại đóng lại an toàn.
  3. Khi chủ sở hữu bấm `Thử lại`, tác vụ quay về `Pending` trong đúng phạm vi `school-hn-01`, không tự động nâng trạng thái duyệt câu hỏi hoặc chốt đề thi.

---

## 3. Kiểm Chứng Tính Nhất Quán `FileJobStore` Sau Khi Khởi Động Lại (`Restart Consistency`)

Bài kiểm thử `RestartConsistency_FileJobStore_PreservesAllJobStatesAttemptsResultLinksAndTelemetry` xác nhận:
- Sau khi hủy instance `FileJobStore` cũ và khởi tạo instance mới trên cùng tệp JSON (mô phỏng khởi động lại tiến trình API/Worker cục bộ), toàn bộ danh sách tác vụ, lịch sử attempts (`Abandoned` ở lượt #1, `Completed` ở lượt #2), đường dẫn `ResultUrl` (`/jobs/?jobId=...&resultId=...`), và bộ đếm `StaleLeaseRecoveries` đều được nạp lại chính xác.
