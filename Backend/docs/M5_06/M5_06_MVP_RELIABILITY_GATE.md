# Biên Bản Nghiệm Thu Cổng Độ Tin Cậy MVP Tuần 6 (M5-06 MVP Reliability Gate)

- **Cổng kiểm soát:** Tuần W06 — **MVP Reliability & Extension Gate**
- **Mã công việc:** M5-06 (`Issue #30`)
- **Trạng thái cổng:** **PASSED (ĐẠT CHUẨN MỞ KHÓA EXTENSION TUẦN 7–9)**

---

## 1. Bảng Đối Chiếu Tiêu Chí Nghiệm Thu Cốt Lõi (Core MVP Gate Checklist)

| Hạng Mục Kiểm Soát | Yêu Cầu Cổng MVP Tuần 6 (`IMPLEMENTATION_PLAN.md`) | Bằng Chứng Kỹ Thuật & Kiểm Thử | Kết Quả |
| :--- | :--- | :--- | :--- |
| **1. Chống mất mát tác vụ khi Restart / Crash** | Worker bị sập giữa chừng hoặc khởi động lại không làm mất job, không treo ở trạng thái `Running` vô thời hạn. | `RecoverExpiredLeasesAsync`, `Drill01_WorkerCrash_StaleLeaseRecovery`, `RestartConsistency_FileJobStore` | **PASS** |
| **2. Chống nhân đôi dữ liệu khi Retry (Idempotency)** | Thử lại sau lỗi mạng hoặc timeout không sinh trùng đề thi (`ExamSnapshot`) hoặc nhân đôi lô câu hỏi (`CsvImport`). | `Drill02_TimeoutAfterWrite_ExamGenerationAndCsvImport` + `JobIdempotency` fingerprint check | **PASS** |
| **3. Bảo vệ hạn ngạch AI & Chống bão lỗi** | Ngắt mạch tự động khi Bedrock lỗi liên tiếp, hoãn job an toàn mà không làm mất lượt retry của người dùng, phát cảnh báo SNS đã khử thông tin nhạy cảm. | `Drill03_ProviderThrottlingAndCircuitBreaker`, `InMemoryCircuitBreaker`, `SnsAlertPublisher` | **PASS** |
| **4. Phân loại lỗi & Khử dữ liệu nhạy cảm (`CWE-209`)** | Lỗi vĩnh viễn (`400`/`403`) không được retry tự động; tuyệt đối không lộ AWS Secret Key, ARN, đường dẫn nội bộ hay nội dung đề thi. | `Drill04_PermanentValidationAndAccessDenied`, `AwsErrorMapping.SanitizeForClient` | **PASS** |
| **5. Toàn vẹn phân quyền khi Hủy & Retry (`Zero-Trust`)** | Chỉ chủ sở hữu trong đúng `Scope` (`School` / `MinistryExam`) mới được hủy hoặc retry; thao tác retry không bao giờ tự động approve câu hỏi hoặc finalize đề thi. | `Drill05_UserCancellationDuringExecution`, `JobService.RetryJobAsync` | **PASS** |
| **6. Hoàn thiện Jobs UX MVP (`/jobs/`)** | Hiển thị đầy đủ chi tiết tác vụ, lịch sử các lượt chạy (`Attempts`), lý do thử lại (`RetryReason`), huy hiệu khôi phục (`IsRecoveredFromFailure`) và liên kết trực tiếp tới kết quả (`ResultUrl`). | `Backend/Api/wwwroot/jobs/` (`index.html`, `app.js`, `style.css`), `JobStatusDto`, `JobApiResponse` | **PASS** |

---

## 2. Phối Hợp Bất Biến Liên Module (Cross-Owner Invariants M1 – M5)

1. **Với M1 (Exams — Blueprint & Generation):**
   - Tác vụ `ExamGeneration` khi hoàn tất trả về mã định danh tiền định `exam-snap-{jobId:N}` và đường dẫn điều hướng `/exams/preview?snapshotId=exam-snap-{jobId:N}`.
   - Việc retry tác vụ chỉ kích hoạt lại luồng sinh nháp/tổng hợp, không ghi đè lên các đề thi đã chốt bất biến (`FinalExamSnapshot`).
2. **Với M2 (Import & Infrastructure Observability):**
   - Tác vụ `CsvImport` bảo đảm tính nguyên tử theo lô `import-batch-{jobId:N}` và điều hướng về `/questions?importBatchId=import-batch-{jobId:N}`.
   - Mọi metric phát qua CloudWatch EMF (`jobs_failed_total`, `job_lease_recoveries_total`, `ai_provider_circuit_breaker_state`) khớp 100% với cấu hình Terraform Alarms tại `Backend/infrastructure/modules/observability/main.tf`.
3. **Với M3 (Review Workflow & Security Gate):**
   - Xác thực qua `JobEngineAuthenticationHandler` và `JobSecurityPolicies.IsSystemAdministrator` kiểm tra tường minh `Role` và `Permission`, ngăn chặn hoàn toàn truy cập chéo trường (`Cross-Scope IDOR`).
4. **Với M4 (Question Bank & Grounded RAG):**
   - Các lỗi đầu ra AI không hợp lệ (`AI_MALFORMED_OUTPUT`, `RAG_RETRIEVAL_EMPTY`) được phân loại `Permanent`, giữ nguyên quy tắc mọi câu hỏi sinh từ AI phải qua bước kiểm duyệt của con người (`ReviewWorkflow`).

---

## 3. Quyết Định Mở Khóa Giai Đoạn Mở Rộng (Unblocking Extensions W07–W09)

Với việc hoàn tất toàn bộ 6 tiêu chí nghiệm thu của M5-06, cổng độ tin cậy MVP tuần 6 chính thức đạt chuẩn để mở khóa các hạng mục phụ thuộc tiếp theo:
- **[M5-07] (`Issue #35`):** Dataset độ khó, baseline và đánh giá mô hình trên AWS SageMaker.
- **[M4-08] (`Issue #39`):** Tích hợp Amazon Lex V2 thu thập intent/slot đến Exam Blueprint.
- **[M1-07] (`Issue #31`):** Tối ưu hiệu năng generation và kiểm thử hồi quy hợp đồng.
- **[M1-09] (`Issue #41`) & [M5-09] (`Issue #45`):** Nghiệm thu E2E nghiệp vụ và bằng chứng vận hành toàn hệ thống.
