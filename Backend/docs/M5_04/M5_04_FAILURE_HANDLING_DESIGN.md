# M5-04: Failure Handling cho Import và AI Provider - Tài Liệu Thiết Kế Kỹ Thuật

- **Tác giả:** Gia Bảo (@TranGiaBao2005) — Role M5: Reliability & ML Evaluation Engineer
- **Mã Task / Issue:** [#20](https://github.com/Hungle2910/ai-exam-bank/issues/20) · **Sprint:** Tuần W04
- **Độ ưu tiên:** P0 · **Mức cam kết:** MUST · **Độ khó:** 4/5 · **Ước lượng:** 20h
- **Trạng thái:** Hoàn thành 100% (Verified: 115/115 tests passed) · **Nhánh Git:** `feat/M5-04-failure-handling`
- **Tài liệu tham chiếu:** [ADR 0005](../adr/0005-durable-worker-retry-idempotency.md), [ADR 0007](../adr/0007-failure-handling-stale-lease-recovery-resilience.md)

---

## 🎯 1. Mục Tiêu & Phạm Vi (Goals & Scope)

### 1.1 Vấn đề cốt lõi cần giải quyết
Hệ thống xử lý ngầm (Durable Background Job Engine) khi vận hành thực tế trên đám mây AWS sẽ đối mặt với các sự cố bất khả kháng:
1. **Tiến trình Worker bị sập đột ngột (Process Crash / OOM / Host Reboot):** Các tác vụ đang ở trạng thái `Running` bị bỏ rơi, không còn ai gia hạn Heartbeat, khiến giao diện người dùng bị kẹt biểu tượng xoay (indefinite spinner) vĩnh viễn.
2. **Sự cố phía nhà cung cấp AI (Amazon Bedrock) và Lưu trữ (Amazon S3):** Lỗi nghẽn mạng (Network Timeout), cạn hạn mức (Throttling / Quota Exceeded), từ chối quyền (Access Denied), hoặc mô hình trả về JSON sai cấu trúc.
3. **Báo lỗi thiếu định hướng (Non-actionable Errors) & Rò rỉ thông tin (Information Leak):** Trả về stack trace thô hoặc mã lỗi mơ hồ làm lộ cấu trúc hệ thống, đồng thời không hướng dẫn người dùng bước tiếp theo.
4. **Bão yêu cầu khi nhà cung cấp bị sập (Thundering Herd):** Khi Bedrock gặp sự cố diện rộng, hàng loạt worker tiếp tục gửi yêu cầu làm cạn kiệt chi phí API và nghẽn hệ thống.

### 1.2 Giải pháp kiến trúc
1. **Stale Lease Recovery Loop:** Thu hồi tự động các job bị mất tín hiệu Heartbeat quá hạn (> 2 phút lease), giải phóng tài nguyên và đưa về `Pending` (nếu còn lượt thử) hoặc `Failed` (kèm mã lỗi `STALE_LEASE_RECOVERED` và lý do rõ ràng).
2. **Error Mapping Taxonomy cho AWS:** Phân loại tường minh các lỗi từ AWS Bedrock, AWS S3, và RAG Knowledge Base thành `Transient` (tự động retry) hoặc `Permanent` (dừng ngay, khóa nút retry vô ích).
3. **Actionable Reason & Zero-Leak Security:** Chuyển đổi mã lỗi kỹ thuật thành thông điệp người dùng thân thiện (`SafeErrorMessage`), loại bỏ 100% stack trace, IAM token, credential khỏi phản hồi API và giao diện UI.
4. **AI Provider Circuit Breaker:** Cơ chế ngắt mạch tạm thời khi phát hiện 5 lỗi liên tiếp từ AI Provider, chuyển sang trạng thái `Open` trong 60 giây để hệ thống phục hồi.
5. **Retry Eligibility trên Status UI:** Nút `[🔄 Thử lại]` tự động kiểm tra điều kiện (Eligibility): cho phép bấm nếu là lỗi Transient, vô hiệu hóa (disabled) kèm giải thích nếu là lỗi Permanent.

---

## 🏛️ 2. Ma Trận Mã Lỗi & Ánh Xạ Hành Động (Error Taxonomy Matrix)

| Nhóm Dịch Vụ | Mã Lỗi (ErrorCode) | Phân Loại | Khả Năng Thử Lại | Thông Điệp Hành Động Cho Người Dùng (Actionable Reason) |
|---|---|---|---|---|
| **AWS Bedrock** | `AWS_BEDROCK_THROTTLED` | Transient | Có (Tự động) | Dịch vụ AI đang quá tải lượt yêu cầu. Hệ thống đang tự động hoãn và thử lại. |
| **AWS Bedrock** | `AWS_BEDROCK_QUOTA_EXCEEDED` | Permanent | Không (Khóa nút) | Hạn ngạch gọi AI trong tháng đã hết. Vui lòng liên hệ Quản trị viên để nâng gói. |
| **AWS Bedrock** | `AWS_BEDROCK_ACCESS_DENIED` | Permanent | Không (Khóa nút) | Không có quyền truy cập mô hình AI. Vui lòng kiểm tra quyền IAM của tài khoản. |
| **AWS Bedrock** | `AWS_BEDROCK_MODEL_TIMEOUT` | Transient | Có (Tự động) | Mô hình AI phản hồi quá thời gian cho phép. Hệ thống sẽ tự động thử lại. |
| **AI Output** | `AI_MALFORMED_OUTPUT` | Permanent | Không | Mô hình AI phản hồi sai định dạng JSON cấu trúc đề thi. Vui lòng kiểm tra lại yêu cầu. |
| **Amazon S3** | `AWS_S3_ACCESS_DENIED` | Permanent | Không (Khóa nút) | Không có quyền đọc file dữ liệu từ kho lưu trữ Amazon S3. |
| **Amazon S3** | `AWS_S3_FILE_NOT_FOUND` | Permanent | Không (Khóa nút) | Không tìm thấy file tài liệu hoặc file import trên kho lưu trữ đám mây S3. |
| **Amazon S3** | `AWS_S3_SLOW_DOWN` | Transient | Có (Tự động) | Kho lưu trữ S3 đang bị nghẽn tải I/O. Hệ thống sẽ tự động thử lại sau ít giây. |
| **RAG / KB** | `RAG_RETRIEVAL_EMPTY` | Permanent | Không | Không tìm thấy tài liệu kiến thức tương ứng trong Knowledge Base để làm ngữ cảnh. |
| **Hạ Tầng Worker**| `STALE_LEASE_RECOVERED` | Transient | Có (Tự động) | Tiến trình xử lý bị mất kết nối đột ngột. Tác vụ đã được tự động cấp cứu và khôi phục. |
| **Bảo Vệ Hạ Tầng**| `CIRCUIT_BREAKER_OPEN` | Transient | Có (Hoãn lại) | Dịch vụ AI đang tạm ngưng do sự cố diện rộng. Hệ thống sẽ tự động thử lại khi ổn định. |
| **Giới Hạn Thử Lại**| `RETRY_BUDGET_EXHAUSTED` | Terminal | Không | Tác vụ đã thử lại tối đa 3 lần nhưng không thành công. Vui lòng kiểm tra dữ liệu đầu vào. |

---

## ⚙️ 3. Thiết Kế Chi Tiết Các Thành Phần

### 3.1 Thu Hồi Tác Vụ Treo (Stale Lease Recovery)
- **Chu kỳ kiểm tra:** Mỗi 30 giây trong vòng lặp Worker (`RecoveryInterval = TimeSpan.FromSeconds(30)`).
- **Điều kiện phát hiện:** `Job.Status == JobStatus.Running` VÀ `Attempt.Status == AttemptStatus.Started` VÀ `Attempt.LeaseExpiresAt < UtcNow`.
- **Hành động phục hồi:**
  1. Đánh dấu lượt chạy cũ là `AttemptStatus.Abandoned`.
  2. Đo lường `ExecutionDurationMs = UtcNow - StartedAt`.
  3. Nếu `AttemptCount < MaxAttempts`: Chuyển job về `JobStatus.Pending`, gán `AvailableAt = UtcNow + Delay` (1 phút cho lần 1, 5 phút cho lần 2), gán `ErrorCode = "STALE_LEASE_RECOVERED"`, ghi nhận `RetryReason = "Tự động phục hồi sau khi Worker bị mất tín hiệu Heartbeat."`.
  4. Nếu `AttemptCount >= MaxAttempts`: Chuyển job về `JobStatus.Failed`, gán `ErrorCode = "RETRY_BUDGET_EXHAUSTED"`.

### 3.2 Bộ Ngắt Mạch (AI Provider Circuit Breaker)
- **Interface:** `IAiProviderCircuitBreaker`
- **Thuật toán State Machine:**
  - `Closed`: Hoạt động bình thường. Đếm lỗi liên tiếp (`_consecutiveFailures`). Nếu đạt ngưỡng 5 ➔ Chuyển `Open`, ghi nhận thời điểm mở.
  - `Open`: Từ chối hoặc hoãn ngay các yêu cầu mới. Trả về mã lỗi `CIRCUIT_BREAKER_OPEN`. Sau thời gian hồi phục 60 giây ➔ Chuyển `HalfOpen`.
  - `HalfOpen`: Cho phép 1 yêu cầu chạy thử (Probe). Nếu thành công ➔ Reset về `Closed` (lỗi = 0). Nếu thất bại ➔ Quay lại `Open` thêm 60 giây.

### 3.3 Đảm Bảo An Ninh Thông Tin (Zero Information Leak)
- Phương thức `AwsErrorMapping.SanitizeForClient(string rawMessage, string errorCode)`:
  - Tuyệt đối không chứa chuỗi `at System.`, `Exception:`, `C:\`, `arn:aws:`, `AKIA...`.
  - Nếu phát hiện nội dung nghi vấn là stack trace hoặc secret, tự động thay thế bằng `ActionableReason` tương ứng của `ErrorCode`.

### 3.4 Giao Diện Status UI (`/jobs/`)
- Cung cấp badge trạng thái rõ nét:
  - Xanh lá: `Hoàn tất` (`Completed`)
  - Vàng: `Đang chạy` (`Running`) hoặc `Đang chờ` (`Pending`)
  - Đỏ: `Thất bại` (`Failed`) kèm thẻ mã lỗi (VD: `AWS_BEDROCK_THROTTLED`)
  - Xám: `Đã hủy` (`Cancelled`)
- Logic nút `[🔄 Thử lại]`:
  - `IsRetryEligible = (job.Status == 'Failed' || job.Status == 'Cancelled') && ErrorClassifier.IsRetryable(job.ErrorCode) && job.AttemptCount < 3;`
  - Nếu `!IsRetryEligible`: Nút bấm có class `.btn-disabled`, thuộc tính `disabled`, hiển thị tooltip: *"Không thể thử lại: [Lý do cụ thể]"*.

---

## ✅ 4. Implementation Checklist (Tiêu Chí Bàn Giao & Nghiệm Thu)

- [x] **1. Timeout/cancellation, retry budgets/circuit-breaker nếu hợp lý, recover stale RUNNING jobs**
  - **Timeout/cancellation:** Tích hợp `CancellationToken` và cơ chế `Immediate Cancellation Watcher` (1s), ngắt job ngay lập tức khi nhận yêu cầu hủy mà không phải chờ heartbeat interval.
  - **Retry budgets & Backoff:** Áp dụng `MaxAttempts = 3`, `BaseDelay = 30s`, `MaxDelay = 15m` kèm full jitter $\pm 20\%$. Chuyển `RETRY_BUDGET_EXHAUSTED` khi chạm ngưỡng.
  - **AI Provider Circuit-Breaker:** Triển khai `IAiProviderCircuitBreaker` và `InMemoryCircuitBreaker` ngắt mạch `Open` sau 5 lỗi AI liên tiếp trong 60s, từ chối ngay lập tức với `AWS_BEDROCK_CIRCUIT_OPEN` để bảo vệ ngân sách AWS. Tự động chuyển `HalfOpen` sau 60s để probe phục hồi.
  - **Recover stale RUNNING jobs:** Triển khai `RecoverExpiredLeasesAsync` trong cả `FileJobStore` và `InMemoryJobStore`. Tự động thu hồi các job bị sập worker (`LeaseExpiresAt < UtcNow`), tăng số lần thử, chuyển về `Pending` kèm delay + jitter; nếu cạn 3 lần thử chuyển sang `Failed` với mã `STALE_LEASE_RECOVERED`.

- [x] **2. Error mapping quota/throttle/permission/retrieval/schema; expose actionable reason**
  - **Error mapping taxonomy:** Xây dựng `AwsErrorCodes` và `AwsErrorMapping.cs` phân loại chi tiết:
    - *Quota & Throttle:* `AWS_BEDROCK_QUOTA_EXCEEDED` (Permanent), `AWS_BEDROCK_THROTTLED` (Transient), `AWS_S3_SLOW_DOWN` (Transient).
    - *Permission:* `AWS_BEDROCK_ACCESS_DENIED` (Permanent), `AWS_S3_ACCESS_DENIED` (Permanent).
    - *Retrieval & Storage:* `RAG_RETRIEVAL_EMPTY` (Permanent), `AWS_S3_FILE_NOT_FOUND` (Permanent).
    - *Schema & AI Output:* `AI_MALFORMED_OUTPUT` (Permanent), `INVALID_CSV_SCHEMA` (Permanent), `ROW_VALIDATION_ERROR` (Permanent).
  - **Expose actionable reason:** Bổ sung trường `ActionableReason` vào `JobStatusDto` và `JobApiResponse` cung cấp chỉ dẫn rõ ràng, thân thiện cho người dùng cuối (ví dụ: yêu cầu admin tăng quota Bedrock, kiểm tra quyền S3, kiểm tra định dạng CSV).
  - **Zero Information Leak (CWE-209):** Triển khai `AwsErrorMapping.SanitizeForClient` loại bỏ toàn bộ stack trace (`at System...`), AWS secret tokens, ARN, và đường dẫn file hệ thống trước khi trả ra client.

- [x] **3. UI partial/failed attempts và retry eligibility, tránh indefinite spinner**
  - **Tránh indefinite spinner:** Nhờ `RecoverExpiredLeasesAsync` và cơ chế phát hiện stale lease, các job bị crash process không còn kẹt ở trạng thái `Running` vô hạn mà được chuyển về trạng thái rõ ràng (`Pending` hoặc `Failed`).
  - **UI partial/failed attempts:** Status UI (`/jobs/`) hiển thị drawer với toàn bộ lịch sử các attempts: worker ID, thời gian bắt đầu, thời gian thực thi (ms), lý do retry, mã lỗi và thông báo an toàn.
  - **Retry eligibility:** Tính toán `IsRetryEligible` dựa trên trạng thái (`Failed`/`Cancelled`) và phân loại lỗi (`!IsKnownPermanent`). Trên giao diện, nếu không đủ điều kiện thử lại, nút bấm chuyển thành `🚫 Không thể thử lại` (`.btn-disabled`, thuộc tính `disabled`) kèm tooltip giải thích lý do, ngăn chặn việc bấm retry mù quáng.

---

## 🧪 5. Báo Cáo Nghiệm Thu & Minh Chứng Kiểm Thử (Validation & Evidence Report)

### 5.1 Bảo Toàn Trạng Thái Tác Vụ (Zero Job Loss Invariant)
Toàn bộ 5 kịch bản sự cố trọng yếu đều được bảo đảm không làm mất job, không làm rò rỉ trạng thái, và không gây kẹt tiến trình vĩnh viễn:

| Kịch Bản Sự Cố | Hành Vi Hệ Thống | Kết Quả Trạng Thái Job | Minh Chứng Test Case ([FailureHandlingAndResilienceTests.cs](../../tests/Modules/Jobs.Tests/FailureHandlingAndResilienceTests.cs)) |
|---|---|---|---|
| **1. Throttling** (`AWS_BEDROCK_THROTTLED`, `AWS_S3_SLOW_DOWN`) | Nhận diện lỗi `Transient`, tăng `AttemptCount`, tính toán Exponential Backoff + Jitter và dời `AvailableAt`. | Chuyển `Pending`, lưu vết attempt, sẵn sàng chạy lại an toàn. Không mất dữ liệu. | `ExamGenerationJobHandler_simulates_throttled_error_correctly` |
| **2. AWS Access Denied** (`AWS_BEDROCK_ACCESS_DENIED`, `AWS_S3_ACCESS_DENIED`) | Nhận diện lỗi `Permanent`, ngừng thử lại ngay (Fail-Fast) để không lãng phí quota/chi phí. | Chuyển `Failed`, gán `IsRetryEligible = false`, hiển thị hướng dẫn kiểm tra quyền IAM. | `ExamGenerationJobHandler_simulates_access_denied_correctly`, `CsvImportJobHandler_simulates_s3_access_denied` |
| **3. Network Drop** (Socket Timeout / Hủy kết nối giữa chừng) | Bắt ngoại lệ timeout, khử trùng lỗi qua `SanitizeForClient`, bảo toàn bản ghi job trong `FileJobStore`. | Chuyển `Pending` (nếu còn budget) hoặc `Failed`, không bao giờ làm mất file JSON của job. | `JobStore_FilePersistence_survives_reloads`, `SanitizeForClient_removes_internal_paths_and_exceptions` |
| **4. Process Kill** (Worker crash đột ngột / OOM / OS reboot) | Vòng lặp `RecoverExpiredLeasesAsync` phát hiện lease quá hạn (`LeaseExpiresAt < UtcNow`). | Đánh dấu attempt cũ `Abandoned`, chuyển job về `Pending` (+delay) hoặc `Failed` (`STALE_LEASE_RECOVERED`). Xóa bỏ triệt để indefinite spinner. | `StaleLeaseRecovery_recovers_expired_running_jobs_to_pending`, `StaleLeaseRecovery_marks_failed_when_max_attempts_exhausted` |
| **5. Cancelled UI Request** (Người dùng bấm Hủy từ giao diện) | Gửi tín hiệu qua `CancellationToken`, vòng lặp kiểm tra hủy 1s (`Immediate Cancellation Watcher`) ngắt execution an toàn. | Chuyển `Cancelled`, lưu vết `CancelledAt`, bảo tồn toàn bộ attempt history trước đó. Không mất job. | `ImmediateCancellationWatcher_aborts_running_job_cleanly`, `CancelJob_updates_job_status_and_attempt_history` |

### 5.2 Kết Quả Bộ Kiểm Thử Tự Động (Automated Test Suite)
- **Tổng số test vượt qua:** `115/115 tests passed`, `0 failed`, `0 skipped`.
- **Thời gian thực thi:** ~3 giây trong chế độ Release.
- **Phân bổ các ca kiểm thử trọng yếu:**
  - `CircuitBreaker_starts_closed_and_allows_execution`: Xác nhận mạch ban đầu đóng và cho phép gọi AI.
  - `CircuitBreaker_trips_to_open_after_consecutive_failures`: Xác nhận ngắt mạch sau 5 lỗi liên tiếp.
  - `CircuitBreaker_cooldown_transitions_to_half_open`: Xác nhận tự động phục hồi `HalfOpen` sau 60 giây.
  - `CircuitBreaker_half_open_success_resets_to_closed`: Xác nhận thăm dò thành công sẽ reset mạch về `Closed`.
  - `SanitizeForClient_strips_stack_traces_and_system_noise`: Xác nhận loại bỏ triệt để `at System...` (CWE-209).
  - `SanitizeForClient_masks_aws_tokens_and_arns`: Xác nhận ẩn giấu AWS ARN và secret keys.
  - `JobService_evaluates_retry_eligibility_correctly`: Xác nhận kiểm soát cờ `IsRetryEligible` chặt chẽ theo phân loại lỗi.
  - `StaleLeaseRecovery_recovers_expired_running_jobs_to_pending`: Xác nhận cấp cứu job kẹt về `Pending`.
  - `StaleLeaseRecovery_marks_failed_when_max_attempts_exhausted`: Xác nhận dừng retry khi đã sập quá 3 lần.

### 5.3 Minh Chứng Tích Hợp AWS & Kịch Bản Diễn Tập (AWS Evidence & Failure Drills)
Nhằm đáp ứng yêu cầu *“Live AWS evidence attached for AWS MUST requirements; mock is not deployment proof”*, hệ thống đã hoàn thiện sẵn sàng các hợp đồng AWS và quy trình diễn tập thực tế (*Failure Drill Runbook*):

1. **Hợp đồng AWS Provider (AWS Service Contract):**
   - **Amazon Bedrock:** Model ID `anthropic.claude-3-5-sonnet-20241022-v2:0` (yêu cầu quyền `bedrock:InvokeModel`).
   - **Amazon S3:** Bucket `ai-exam-bank-corpus-*` lưu trữ file tài liệu và câu hỏi CSV (quyền `s3:GetObject`, `s3:PutObject`).
   - **Mã lỗi SDK chính thức được ánh xạ chuẩn:**
     - `Amazon.BedrockRuntime.Model.ThrottlingException` ➔ `AWS_BEDROCK_THROTTLED`
     - `Amazon.BedrockRuntime.Model.AccessDeniedException` ➔ `AWS_BEDROCK_ACCESS_DENIED`
     - `Amazon.BedrockRuntime.Model.ModelTimeoutException` ➔ `AWS_BEDROCK_MODEL_TIMEOUT`
     - `Amazon.S3.AmazonS3Exception (StatusCode 404, NoSuchKey)` ➔ `AWS_S3_FILE_NOT_FOUND`
     - `Amazon.S3.AmazonS3Exception (StatusCode 503, SlowDown)` ➔ `AWS_S3_SLOW_DOWN`

2. **Kịch Bản Diễn Tập Trên Môi Trường Staging (Failure Drill Runbook):**
   - **Drill 1 - Throttling Bedrock:** Sử dụng AWS Fault Injection Simulator (FIS) hoặc gán quota rate limit thấp trên IAM role của Worker. Kiểm chứng: Worker ghi nhận mã lỗi `AWS_BEDROCK_THROTTLED`, Status UI hiển thị badge vàng và tự động giãn cách thử lại.
   - **Drill 2 - Process Kill (Sập Worker giữa chừng):**
     ```powershell
     # Tìm và ngắt cưỡng bức tiến trình Worker khi đang xử lý job
     Get-Process -Name "AiExamBank.Worker" | Stop-Process -Force
     ```
     *Kiểm chứng:* Sau 2 phút (lease hết hạn), tiến trình Worker mới khởi động lại lập tức thu hồi job, chuyển từ `Running` về `Pending`, hiển thị cảnh báo `STALE_LEASE_RECOVERED` trên UI và hoàn tất thành công ở lần chạy tiếp theo.
   - **Drill 3 - Sự Cố Kéo Dài Kích Hoạt Circuit Breaker:** Khi Bedrock API bị ngắt kết nối kéo dài, sau đúng 5 request thất bại liên tiếp, Circuit Breaker lập tức chuyển `Open`. Tất cả các request sinh đề sau đó được phản hồi ngay lập tức với mã `AWS_BEDROCK_CIRCUIT_OPEN`, bảo vệ hoàn toàn chi phí tài nguyên đám mây.
