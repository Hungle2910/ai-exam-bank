# Báo Cáo Cổng Độ Tin Cậy MVP Tuần 6 (M5-06 MVP Reliability Gate — Local Slice & Handoff)

- **Cổng kiểm soát:** Tuần W06 — **MVP Reliability & Extension Gate**
- **Mã công việc:** M5-06 (`Issue #30` — Partial Slice)
- **Trạng thái cổng hiện tại:** **PARTIAL (ĐÃ KIỂM CHỨNG LOCAL JOB ENGINE & SIMULATED HANDLERS — CHƯA ĐÓNG GATE MVP / CHỜ TÍCH HỢP DB THẬT M1–M4 & DIỄN TẬP STAGING/AWS)**

> **Lưu ý quan trọng (Caveat):** Tài liệu này ghi nhận phạm vi đã kiểm chứng ở mức **Job Engine cục bộ (`InMemoryJobStore` / `FileJobStore`), giao diện `/jobs/` và hợp đồng Handler mô phỏng**. Đây **chưa phải** là biên bản đóng cổng MVP cuối cùng. Issue `#30` sẽ chỉ được đóng sau khi:
> 1. Các handler `ExamGenerationJobHandler` (M1-06) và `CsvImportJobHandler` (M2-04) được nối với tầng Persistence thực tế để kiểm chứng chống ghi trùng bản ghi DB thật khi retry.
> 2. Chạy diễn tập 5 kịch bản sự cố trực tiếp trên môi trường Staging/AWS thật (sau khi M2-04 và M4-06 hoàn tất triển khai).

---

## 1. Bảng Đối Chiếu Tiêu Chí Nghiệm Thu & Phạm Vi Đã Kiểm Chứng

| Hạng Mục Kiểm Soát | Yêu Cầu Cổng MVP Tuần 6 (`IMPLEMENTATION_PLAN.md`) | Phạm Vi Đã Kiểm Chứng Trong PR Này | Phần Còn Thiếu Trước Khi Đóng Issue #30 | Trạng Thái |
| :--- | :--- | :--- | :--- | :--- |
| **1. Chống mất mát tác vụ khi Restart / Crash** | Worker bị sập giữa chừng hoặc khởi động lại không làm mất job, không treo ở trạng thái `Running` vô thời hạn. | `RecoverExpiredLeasesAsync`, fencing token chống Zombie Worker, và khôi phục trạng thái `FileJobStore` trên đĩa qua `RestartConsistency_FileJobStore`. | Diễn tập kill/restart tiến trình Worker trên môi trường Staging thực tế. | **LOCAL PASS / STAGING PENDING** |
| **2. Chống nhân đôi dữ liệu khi Retry (Idempotency)** | Thử lại sau lỗi mạng hoặc timeout không sinh trùng đề thi (`ExamSnapshot`) hoặc nhân đôi lô câu hỏi (`CsvImport`). | Kiểm chứng khóa chống trùng `JobIdempotency` khi enqueue và hợp đồng `Check-Before-Write` mức handler với mã kết quả mô phỏng (`exam-snap-<jobId>`, `import-batch-<jobId>`). | Tích hợp ghi DB thật từ M1-06 (`FinalExamSnapshot`) và M2-04 (`QuestionImport`) để xác minh không tạo bản ghi DB trùng lặp. | **SIMULATED CONTRACT PASS / REAL DB PENDING** |
| **3. Bảo vệ hạn ngạch AI & Chống bão lỗi** | Ngắt mạch tự động khi Bedrock lỗi liên tiếp, hoãn job an toàn mà không làm mất lượt retry của người dùng, phát cảnh báo SNS đã khử thông tin nhạy cảm. | `InMemoryCircuitBreaker`, hoàn lại `AttemptCount` khi `CIRCUIT_BREAKER_OPEN`, và `SnsAlertPublisher` chỉ lưu lịch sử throttling sau khi gửi thành công. | Kiểm chứng phát cảnh báo lên AWS SNS Topic thật và CloudWatch Alarm trên Staging. | **LOCAL PASS / LIVE AWS PENDING** |
| **4. Phân loại lỗi & Khử dữ liệu nhạy cảm (`CWE-209`)** | Lỗi vĩnh viễn (`400`/`403`) không được retry tự động; tuyệt đối không lộ AWS Secret Key, ARN, đường dẫn nội bộ hay nội dung đề thi. | `Drill04_PermanentValidationAndAccessDenied` + `AwsErrorMapping.SanitizeForClient` khóa `IsRetryEligible = false` và trả `409 Conflict` khi cố retry. | Kiểm tra chéo trên log CloudWatch Staging. | **LOCAL PASS** |
| **5. Toàn vẹn phân quyền khi Hủy & Retry (`Zero-Trust`)** | Chỉ chủ sở hữu trong đúng `Scope` (`School` / `MinistryExam`) mới được hủy hoặc retry; thao tác retry không tự động approve câu hỏi hoặc finalize đề thi. | `Drill05_UserCancellationDuringExecution`, kiểm tra chéo `Scope` và `CreatedBy` trong `JobService`. | Kiểm thử E2E liên thông với luồng duyệt của M3 trên Staging. | **LOCAL PASS** |
| **6. Hoàn thiện Jobs UX MVP (`/jobs/`)** | Hiển thị đầy đủ chi tiết tác vụ, lịch sử các lượt chạy (`Attempts`), lý do thử lại (`RetryReason`), huy hiệu khôi phục và mở xem kết quả trực tiếp. | Bảng và Drawer tại `/jobs/` hiển thị `AttemptCount`, `RetryReason`, `IsRecoveredFromFailure` và nút `🔗 Mở KQ` điều hướng trực tiếp tới `/jobs/?jobId=...&resultId=...` (mở ngay bảng kết quả trên trang `/jobs/` hiện có, không lỗi 404). | Nối sang trang xem đề thi/câu hỏi chuyên biệt khi UI của M1/M4 hoàn thiện route riêng. | **PASS** |

---

## 2. Điều Kiện Tiên Quyết Để Đóng Issue #30 & Mở Khóa Hoàn Toàn Cổng MVP

Trước khi chuyển trạng thái cổng từ `PARTIAL` sang `PASSED` và đóng Issue `#30`, nhóm cần hoàn thành các bước tích hợp sau trên nhánh `dev` và môi trường Staging:
1. **Hợp nhất luồng ghi DB thật của M1-06 & M2-04 vào Job Handlers:** Thay thế mã kết quả mô phỏng trong `ExamGenerationJobHandler` và `CsvImportJobHandler` bằng lời gọi service ghi snapshot/import thật và kiểm tra idempotency trực tiếp trên SQL Server.
2. **Thực thi diễn tập trên Staging/AWS thật:** Chạy 5 kịch bản diễn tập trên môi trường Staging đã triển khai (M2-04/M2-06) và đính kèm bằng chứng CloudWatch Metrics/Alarms + SNS delivery thực tế.
3. **Ký nghiệm thu liên module (M1 – M5):** Xác nhận toàn bộ invariants trên bản dựng Release Candidate trước khi mở rộng sang các tác vụ phụ thuộc (M5-07 SageMaker, M4-08 Lex V2, M1-07, M1-09, M5-09).
