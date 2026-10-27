# AI Exam Bank - Persistent Memory & Task Ledger

Tài liệu này ghi lại toàn bộ tiến độ công việc, các quyết định kiến trúc và cột mốc kỹ thuật đã hoàn thành của kỹ sư Gia Bảo (Role M5: Reliability & ML Evaluation Engineer).

---

## 🕒 Lịch sử cập nhật tiến độ (Progress Timeline)

### [2026-10-27] M5-04: Failure Handling cho Import và AI Provider - HOÀN THÀNH 100%
- **Issue:** [#20](https://github.com/Hungle2910/ai-exam-bank/issues/20)
- **Nhánh triển khai:** `feat/M5-04-failure-handling` (tạo từ `dev`).
- **Ước lượng:** 20h · **Thời gian thực tế (Actual Hours):** 18h · **Tiêu chí:** Hoàn tất 100% 4/4 nhóm Checklist.
- **Phạm vi kỹ thuật đã bàn giao:**
  1. **Stale Lease Recovery & Crash Resilience:**
     - Tự động phát hiện và khôi phục các job bị kẹt `Running` do worker sập, crash process hoặc network partition.
     - Cơ chế `RecoverExpiredLeasesAsync` trong cả `FileJobStore` và `InMemoryJobStore`: tăng `AttemptCount`, áp dụng delay có jitter, và nếu chạm ngưỡng `MaxAttempts` (3) sẽ chuyển hẳn sang `Failed` với mã lỗi `STALE_LEASE_RECOVERED`.
  2. **AWS Error Mapping Taxonomy & Actionable Guidance:**
     - Tạo `AwsErrorCodes` và `AwsErrorMapping.cs` ánh xạ chi tiết các mã lỗi AWS Bedrock, S3, RAG và Stale Lease thành `Transient` vs `Permanent`.
     - Cung cấp `ActionableReason` rõ ràng, có thể hành động cho từng mã lỗi thay vì thông điệp kỹ thuật khó hiểu.
  3. **Zero Information Leak (CWE-209):**
     - Triển khai `AwsErrorMapping.SanitizeForClient` loại bỏ toàn bộ stack trace (`at System...`), AWS ARN (`arn:aws:...`), đường dẫn ổ đĩa cục bộ (`C:\...`) và AWS credentials (`AKIA...`).
  4. **AI Provider Circuit Breaker (Resilience):**
     - Thiết kế interface `IAiProviderCircuitBreaker` và cài đặt `InMemoryCircuitBreaker`:
       - Tự động ngắt mạch (Open) sau 5 lỗi AI liên tiếp trong cửa sổ 60s.
       - Trả về mã lỗi `AWS_BEDROCK_CIRCUIT_OPEN` ngay lập tức để bảo vệ ngân sách AWS và tránh quá tải Bedrock.
       - Tự động chuyển sang trạng thái `HalfOpen` sau thời gian cooldown để thăm dò phục hồi.
  5. **Nâng cấp Status UI (`/jobs/`) & Quản lý Retry Eligibility:**
     - Bổ sung trường `IsRetryEligible` và `ActionableReason` vào `JobStatusDto` và `JobApiResponse`.
     - Status UI hiển thị cảnh báo `actionable-advice-banner` và tự động vô hiệu hóa nút `[🔄 Thử lại]` đối với lỗi vĩnh viễn (Permanent) hoặc khi đã cạn số lượt retry.
  6. **Tài liệu, Runbook Diễn Tập & ADR:**
     - Soạn thảo tài liệu thiết kế: `Backend/docs/M5_04/M5_04_FAILURE_HANDLING_DESIGN.md`.
     - Soạn thảo runbook diễn tập: `Backend/docs/M5_04/M5_04_FAILURE_DRILL_RUNBOOK.md` & `drill_evidence.json`.
     - Tool chạy tự động: `Backend/tools/run_failure_drills.py`.
     - Bổ sung quyết định kiến trúc: `Backend/docs/adr/0007-failure-handling-stale-lease-recovery-resilience.md`.
  7. **Kiểm thử toàn diện:**
     - 115 unit tests trong `AiExamBank.Modules.Jobs.Tests` vượt qua 100% (bổ sung 31 test mới trong `FailureHandlingAndResilienceTests.cs`).
     - Script kiểm tra repo `python Backend/tools/check_repository.py` đạt 0 lỗi trên 433 files.

### [2026-10-27] M5-03: Retry, Backoff, Idempotency & Integration Adapters - HOÀN THÀNH 100% (PR #74 MERGED)
- **Issue:** [#15](https://github.com/Hungle2910/ai-exam-bank/issues/15)
- **Pull Request:** [#74](https://github.com/Hungle2910/ai-exam-bank/pull/74)
- **Nhánh triển khai:** `feat/M5-03-retry-idempotency` ➔ **Đã merge vào `dev`** (Commit `9c16959`).
- **Phạm vi kỹ thuật đã bàn giao:**
  1. **Phân loại lỗi (Error Taxonomy):**
     - Tạo `ErrorCategory.cs` và `ErrorClassifier.cs` phân loại rõ 3 nhóm:
       - `Transient`: Lỗi tạm thời (Network timeout, 503, lock conflict) ➔ Tự động tính toán retry.
       - `Permanent`: Lỗi cố định (400 Bad Request, Validation, Auth 401/403) ➔ Dừng ngay, chuyển Failed.
       - `Terminal`: Trạng thái kết thúc của job.
  2. **Bounded Exponential Backoff + Full Jitter:**
     - Tạo `BackoffJitterCalculator.cs` theo chuẩn kiến trúc AWS:
       - `BaseDelay = 30s`, `MaxDelay = 15m`, `MaxAttempts = 3`.
       - Jitter +/-20% ngăn ngừa hiện tượng giông bão yêu cầu (Thundering Herd).
  3. **Đo lường thời gian & Lịch sử Attempts:**
     - Mở rộng domain model `BackgroundJob` và entity `JobAttempt`:
       - Ghi nhận `ExecutionDurationMs`, `RetryReason`, `IsManualRetry`, `LastRetryRequestedBy`, `LastRetryRequestedAt`.
       - Lưu vết chi tiết từng lần chạy (Worker ID, thời gian bắt đầu, kết thúc, mã lỗi).
  4. **Khôi phục Timeout-After-Write (Idempotency):**
     - Tích hợp kỹ thuật Check-Before-Write cho 2 adapter nghiệp vụ:
       - `ExamGenerationJobHandler`: Kiểm tra snapshot đề thi đã ghi trước đó, tái sử dụng ID nếu có retry sau sự cố mạng.
       - `CsvImportJobHandler`: Quản lý batch ID phân vùng, tránh import trùng lặp dòng dữ liệu câu hỏi.
  5. **API Endpoint Thử lại & Tra cứu Lịch sử:**
     - `POST /api/v1/jobs/{id}/retry`: Cho phép thử lại thủ công các job bị lỗi hoặc bị hủy.
     - `GET /api/v1/jobs/{id}/attempts`: Trả về toàn bộ danh sách lịch sử attempts kèm metrics thời gian thực thi.
     - Kiểm soát Zero-Trust Authorization: chặn truy cập chéo trường/bộ (`Forbidden`), chặn retry job đang chạy (409 Conflict) hoặc job đã thành công (400 Bad Request).
  6. **Cập nhật Giao diện Status UI (`/jobs/`):**
     - Bổ sung nút bấm `[🔄 Thử lại]` cho các task thất bại/đã hủy.
     - Bổ sung Drawer hiển thị timeline attempts, thời gian thực thi (ms) và lý do retry trực quan.
  7. **Tài liệu & ADR:**
     - Soạn thảo tài liệu thiết kế: `Backend/docs/M5_03/M5_03_RETRY_IDEMPOTENCY_DESIGN.md`.
     - Bổ sung quyết định kiến trúc: `Backend/docs/adr/0005-durable-worker-retry-backoff-and-idempotency.md`.
  8. **Kiểm thử:**
     - 84 unit tests trong `AiExamBank.Modules.Jobs.Tests` vượt qua 100%.
     - Smoke test `Backend/tests/smoke/test_api_health.py` đạt 19/19 assertions.
     - Vệ sinh repo `python Backend/tools/check_repository.py` đạt 0 lỗi.

---

### [2026-10-25] M5-02: Durable Job Engine & Status UI - HOÀN THÀNH 100% (PR #68 MERGED)
- **Issue:** [#10](https://github.com/Hungle2910/ai-exam-bank/issues/10)
- **Pull Request:** [#68](https://github.com/Hungle2910/ai-exam-bank/pull/68)
- **Nhánh triển khai:** `feat/M5-02-durable-job-engine` ➔ **Đã merge vào `dev`**.
- **Giải quyết toàn diện nhận xét CodeRabbit Review `5561326141`:**
  1. **Lưu trữ bền vững & Đa tiến trình (`FileJobStore`):** Sử dụng Windows Named Mutex toàn cục (`Global\AiExamBank_FileJobStore_Mutex`), ghi đĩa nguyên tử qua file `.tmp` + `File.Move(overwrite: true)`.
  2. **In-process Worker & Status UI cho API host:** Khởi chạy worker nhẹ song song trong API host cho môi trường phát triển; Status UI dạng Static Files tại `/jobs/`.
  3. **Zero-Trust Auth & Scope Validation:** Chặn triệt để header giả mạo từ client, sử dụng whitelist dictionary `TrustedTokens` trên server.
  4. **Immediate Cancellation Watcher Loop:** Vòng lặp thăm dò 1s kiểm tra cờ hủy tức thì, ngắt job ngay mà không phụ thuộc vào chu kỳ Heartbeat.
  5. **Duy trì Heartbeat suốt quá trình ghi Terminal State:** Đảm bảo không bị tranh chấp lock làm mất lease dẫn tới chạy lại job ngoài ý muốn.
  6. **Cấu hình CodeRabbit:** Bổ sung cấu hình `allow_author_approval: true` trong `.coderabbit.yaml`.

---

### [2026-10-18] M5-01: Reliability Baseline & Error Taxonomy - HOÀN THÀNH
- **Thiết kế nền tảng:**
  - Định nghĩa interface `IJobHandler<T>` và vòng đời trạng thái job (`Pending` ➔ `Running` ➔ `Completed` / `Failed` / `Cancelled`).
  - Soạn thảo bộ tài liệu nền tảng tại `Backend/docs/M5_01/`:
    - `M5_01_RELIABILITY_DESIGN.md`
    - `M5_01_ERROR_MATRIX.md`
    - `M5_01_METRICS_SLO.md`
    - `M5_01_EXPLANATION_HISTORY.md`

---

## 🎯 Kế hoạch tiếp theo (Next Steps / Backlog)
1. **Theo dõi định kỳ & Tích hợp liên module:**
   - Phối hợp với Module M1 (Questions), M2 (Exams), M4 (Import) khi tích hợp trực tiếp vào database persistence SQL Server qua Entity Framework Core.
2. **Nâng cao khả năng quan sát (Observability & OpenTelemetry):**
   - Đóng gói OpenTelemetry metrics exporter cho Prometheus / AWS CloudWatch.
   - Bổ sung Health Check endpoint cho Worker daemon.
