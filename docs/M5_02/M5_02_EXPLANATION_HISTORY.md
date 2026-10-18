# M5-02 — Cẩm Nang Giải Trình & Lịch Sử Phản Biện Kỹ Thuật (Explanation & Defense History)

**Tác giả:** Trần Gia Bảo (M5 / @TranGiaBao2005) — Reliability & ML Evaluation Engineer  
**Nhiệm vụ:** [Issue #10: M5-02 — Durable Job Engine & Status UI](https://github.com/Hungle2910/ai-exam-bank/issues/10)  
**Nhánh:** `feat/M5-02-durable-job-engine`  
**Phiên bản Engine:** `v1.0.0-rc1`  
**Tài liệu kỹ thuật chính:** [M5_02_JOB_ENGINE_DESIGN.md](M5_02_JOB_ENGINE_DESIGN.md)

---

## 1. Bối cảnh & Mục đích của Cẩm Nang này

Tài liệu này lưu trữ lại toàn bộ **lịch sử trao đổi, quyết định kỹ thuật và cẩm nang trả lời phản biện** khi báo cáo nghiệm thu Task M5-02 trước Solution Architect (Gia Hưng M1), DevOps Lead (Hữu Phước M2), Giảng viên hướng dẫn hoặc Hội đồng đánh giá dự án AWS FCAJ.

Tài liệu giúp Gia Bảo giải thích gãy gọn, tự tin và chuẩn xác về mặt kiến trúc tại sao hệ thống lại được thiết kế như vậy.

---

## 2. Bộ Câu Hỏi & Trả Lời Phản Biện Chuyên Sâu (Q&A Defense Guide)

### Câu 1: Tại sao hệ thống cần một "Durable Background Job Engine" mà không dùng `Task.Run` hoặc `IHostedService` trong bộ nhớ?

- **Trả lời:**
    - Các tác vụ như **sinh đề thi AI qua AWS Bedrock** hoặc **nhập hàng nghìn câu hỏi từ file CSV** mất từ 30 giây đến vài phút để xử lý. Nếu dùng `Task.Run`:
        1. Khi server API bị restart, ngắt điện hoặc scale down, toàn bộ tác vụ đang chạy trong RAM sẽ **bị mất vĩnh viễn (Lost work)** mà không có cơ chế khôi phục.
        2. Không có cơ chế báo cáo tiến độ thời gian thực (Progress %) về cho người dùng.
        3. Không thể mở rộng sang mô hình đa máy chủ (Multi-worker) vì các tiến trình không chia sẻ được trạng thái.
    - **Durable Job Engine** giải quyết triệt để: Mọi trạng thái tác vụ được lưu trữ bền vững (Durable Store / PostgreSQL), hỗ trợ chiếm quyền thực thi có thời hạn (Leasing), tự động khôi phục khi worker chết, và cung cấp API tra cứu tiến độ an toàn.

---

### Câu 2: Cơ chế "Atomic Claim" và "Lease Fencing" hoạt động như thế nào để chống xung đột giữa 2 Worker?

- **Trả lời:**
    - **Atomic Claim (Chiếm quyền nguyên tử):** Khi 2 Worker cùng quét hàng đợi cùng một tích tắc, hệ thống sử dụng cơ chế khóa nguyên tử (trong SQL là `UPDATE jobs ... WHERE status = 'Pending' ... FOR UPDATE SKIP LOCKED` hoặc `lock` trong Store). Chỉ duy nhất **1 Worker** chiếm được Job và được cấp một chuỗi ngẫu nhiên gọi là `LeaseToken`. Worker còn lại nhận kết quả rỗng và nhường quyền.
    - **Lease Fencing (Hàng rào chặn Worker cũ):** Thời hạn chiếm quyền (`LeaseDuration`) là 2 phút. Nếu Worker A bị lag mạng hoặc treo máy quá 2 phút, hệ thống sẽ thu hồi quyền và cấp Job cho Worker B với một `LeaseToken` mới. Khi Worker A tỉnh dậy và cố ghi đè kết quả, hệ thống kiểm tra token của Worker A không khớp với token hiện hành $\rightarrow$ Thao tác cập nhật trạng thái/kết quả job trong Job Store bị chặn đứng ngay lập tức (`Fenced`), ngăn ngừa lỗi **Split-Brain** và không làm sai lệch dữ liệu trạng thái job.
    - *Lưu ý quan trọng:* Lease fencing bảo vệ trạng thái của Job trong kho lưu trữ dữ liệu (Store), nhưng không thể ngăn chặn tiến trình Handler cũ tiếp tục chạy hoặc tạo side effect ra môi trường bên ngoài (ví dụ gọi Bedrock hoặc bên thứ ba). Do đó, các Handler nghiệp vụ thực tế bắt buộc phải hiện thực tính lũy quyền (idempotency) hoặc dựa vào cơ chế downstream deduplication.
    - 👉 _Đã kiểm chứng thành công qua Unit test `Competing_workers_only_one_claims_job`, `Heartbeat_renews_for_active_lease_and_fences_stale_tokens` và `Fencing_prevents_stale_worker_from_completing_job`._

---

### Câu 3: Cơ chế "Heartbeat" và "Tự động thu hồi Lease" (Lease Expiry Recovery) là gì?

- **Trả lời:**
    - Trong suốt quá trình thực thi tác vụ nặng, Worker định kỳ gửi một tín hiệu nhịp tim **Heartbeat mỗi 30 giây** để gia hạn thêm 2 phút cho `LeaseExpiresAt`.
    - Nếu Worker bị crash đột ngột (mất điện, kill process), nhịp tim sẽ dừng lại.
    - Một tiến trình quét ngầm (`RecoverExpiredLeasesAsync`) chạy mỗi 30 giây sẽ phát hiện các Job đang `Running` nhưng đã quá hạn `LeaseExpiresAt`:
        - Nếu `AttemptCount < 3`: Thu hồi về trạng thái `Pending` (có backoff) để Worker khác nhận lại.
        - Nếu `AttemptCount >= 3`: Đánh dấu `Failed` vĩnh viễn với mã lỗi `RETRY_BUDGET_EXHAUSTED`.
    - 👉 _Đã kiểm chứng qua test `Stale_lease_recovery_reclaims_abandoned_job`._

---

### Câu 4: Tại sao lại chọn cơ chế "Bounded Retries" với độ trễ 1 phút, 5 phút và tối đa 3 lần?

- **Trả lời:**
    - **Không retry vô hạn (Bounded):** Nếu retry vô hạn, một payload lỗi (ví dụ file CSV hỏng hoặc câu lệnh AI vi phạm quy chuẩn) sẽ làm nghẽn toàn bộ hàng đợi của hệ thống (Poison Pill).
    - **Khoảng trễ lũy tiến (+1m, +5m):** Các lỗi tạm thời của Cloud (ví dụ AWS Bedrock bị Throttling/Rate Limit, hoặc mạng RDS chập chờn) thường cần từ vài chục giây đến vài phút để hệ thống hạ tầng tự phục hồi. Thử lại ngay lập tức chỉ làm tăng tải và tiếp tục thất bại.
    - **Phân loại lỗi thông minh:**
        - Lỗi tạm thời (`IsTransient = true`): Áp dụng ngân sách 3 lần thử.
        - Lỗi nghiệp vụ cố định (`IsTransient = false`, ví dụ `ROW_VALIDATION_ERROR`): Thất bại ngay lập tức mà không thử lại để tiết kiệm tài nguyên.

---

### Câu 5: Hệ thống bảo vệ tính cô lập dữ liệu giữa các trường học (Scoped Authorization) và kiểm soát chủ sở hữu (Ownership) ra sao?

- **Trả lời:**
    - Theo kiến trúc Multi-School của dự án ([ADR 0004](../../docs/adr/0004-multi-school-ministry-mvp.md)), dữ liệu đề thi và câu hỏi của trường học A tuyệt đối không được để lộ cho giáo viên trường học B.
    - Tại [JobService.cs](../../src/Modules/Jobs/Infrastructure/JobService.cs), mọi yêu cầu xem trạng thái (`GetJobStatusAsync`), danh sách hoặc hủy job đều đối chiếu `requesterScope` với `job.Scope` (cả `Kind` và `ResourceId`).
    - Nếu phát hiện truy cập chéo trường: Hệ thống ném ngoại lệ `UnauthorizedAccessException` và API trả về ngay mã lỗi HTTP **403 Forbidden** với mã mở rộng `SCOPED_ACCESS_DENIED` theo chuẩn **RFC 7807 Problem Details**.
    - **Kiểm soát quyền sở hữu tác vụ (Ownership Enforcement):** Trong cùng một phạm vi trường học, hệ thống áp dụng nguyên tắc cô lập nghiêm ngặt theo chủ sở hữu (Strict Owner Isolation): chỉ chính chủ sở hữu (`job.CreatedBy == requesterId`) mới được xem chi tiết, hủy hoặc liệt kê tác vụ của mình. Quyền admin bypass chưa được kích hoạt trong phạm vi M5-02; mọi truy cập từ tài khoản khác trong cùng trường đều bị từ chối truy cập.
    - Đồng thời, toàn bộ payload đề thi, đáp án, và stack trace nội bộ đều được ẩn (Log Redaction) và chỉ trả về thông báo lỗi an toàn `SafeErrorMessage`.

---

### Câu 6: Giao diện Status UI Dashboard có điểm gì nổi bật?

- **Trả lời:**
    - **Thẩm mỹ Dark Mode Glassmorphism:** Thiết kế hiện đại, cao cấp với phông chữ Inter, hiệu ứng làm mờ kính và đổ bóng chiều sâu.
    - **5 Thẻ KPI Thống kê:** Nhảy số thời gian thực theo từng trạng thái (Tổng số, Đang chạy, Chờ xử lý, Thành công, Thất bại).
    - **Adaptive Polling (Thăm dò thích ứng):**
        - Thăm dò mỗi 2 giây khi có tác vụ đang chạy.
        - Tự động giãn cách thành 5 giây khi rảnh rỗi (idle).
        - Tự động giãn thành 8 giây khi mất kết nối mạng.
    - **Dọn dẹp tài nguyên (Resource Cleanup):** Lắng nghe sự kiện `visibilitychange`. Khi người dùng chuyển tab hoặc thu nhỏ trình duyệt, polling tự động dừng hoàn toàn (`clearInterval`) để không làm hao pin và tốn CPU máy người dùng; khi mở lại tab sẽ tự động cập nhật ngay.

---

### Câu 7: Tại sao Task M5-02 ở Tuần 2 chưa đính kèm "Live AWS Evidence" mà vẫn đạt chuẩn nghiệm thu?

- **Trả lời:**
    - Theo phân công trách nhiệm nhóm ([TEAM.md](../../docs/TEAM.md)) và kế hoạch tổng thể ([IMPLEMENTATION_PLAN.md:L269-274](../../docs/IMPLEMENTATION_PLAN.md#L269-L274)):
        - Nhiệm vụ tạo máy chủ EC2, VPC mạng riêng, và database PostgreSQL thật trên AWS RDS thuộc về bạn **Hữu Phước (M2 - AWS DevOps)** trong các gói M2-01 (W01) và M2-03 (W03).
        - Task **M5-02 (Tuần W02)** là gói công việc tầng Application Engine & UI, có tiêu chí nghiệm thu là: _"durable job skeleton có test, local API & UI chạy thực tế, và worker sẵn sàng cho EC2"_.
        - M5-02 đã hoàn tất 100% độ sẵn sàng cho AWS:
            1. Đóng gói Worker độc lập tại [src/Worker/AiExamBank.Worker.csproj](../../src/Worker/AiExamBank.Worker.csproj) sẵn sàng chạy `systemd service` trên Amazon Linux EC2.
            2. Tích hợp W3C Correlation ID (`X-Correlation-Id`) sẵn sàng cho AWS CloudWatch Logs.
            3. Viết script DDL PostgreSQL [001_create_jobs_and_job_attempts.sql](../../src/Persistence/Migrations/001_create_jobs_and_job_attempts.sql) sẵn sàng migrate lên Amazon RDS.
        - **Live AWS Evidence thực tế của M5** (CloudWatch Metrics, Alarms, và AWS SNS Notification) sẽ được thực hiện và đính kèm ở **Tuần W05 (M5-05)** và **Tuần W06 (M5-06)** theo đúng lộ trình dự án.

---

### Câu 9: Giải quyết phản hồi CodeRabbit Review về chia sẻ kho lưu trữ giữa API & Worker, kiểm soát phân quyền Scope Authorization và hàng rào Fencing như thế nào?

- **Trả lời:**
    1. **Chia sẻ lưu trữ & Thực thi chéo Process (Store Sharing & Durability Across Restart):**
        - Đã bổ sung `FileJobStore` bền vững với khóa đồng bộ liên tiến trình (`Mutex`) và lược đồ JSON đối chiếu 1-1 với bảng PostgreSQL (`jobs`, `job_attempts`). Dữ liệu hàng đợi không bị mất khi tiến trình khởi động lại hoặc crash.
        - Đăng ký `JobWorkerHostedService` ngay trong tiến trình `AiExamBank.Api` giúp các tác vụ đẩy qua API/Dashboard được xử lý tức thời đến hoàn tất mà không cần khởi động Worker riêng lẻ trong môi trường Dev.
        - Cấu hình Worker độc lập (`AiExamBank.Worker`) dùng chung `FileJobStore`, loại bỏ đăng ký DI đệ quy của `IJobRegistry`.
    2. **Bảo mật phân quyền bắt buộc & Lược đồ xác thực danh tính (Authentication Scheme & Zero-Trust Scoped Authorization — CWE-639, CWE-862, CWE-306):**
        - Cấu hình lược đồ xác thực `JobEngineAuthenticationHandler` (`JobEngineAuth`) đăng ký trong `Program.cs` và áp dụng `RequireAuthorization()` cho nhóm route `/api/v1/jobs`.
        - Các yêu cầu không có thông tin xác thực hoặc chỉ gửi header giả mạo từ client lập tức bị từ chối với HTTP **401 Unauthorized**.
        - Ràng buộc tạo và truy cập tác vụ với danh tính đã xác thực (`ResolveAuthenticatedScope`): Trích xuất đầy đủ cặp phạm vi (`JobScopeKind ScopeKind`, `string ResourceId`) từ Claims (`scope_kind`, `scope_id`, `school_id`, `scope`, `tenant_id`) của `ClaimsPrincipal`, loại bỏ hoàn toàn việc tin cậy các header do client tự khai báo (`X-Scope-Id`, `X-User-Scope-Id`).
        - Truyền trực tiếp `JobScope` vào các phương thức `GetJobStatusAsync`, `CancelJobAsync`, `ListJobsAsync` của `IJobTracker` và `JobService`: Bắt buộc kiểm tra đồng thời cả hai trường `Scope.Kind` và `Scope.ResourceId`, triệt tiêu lỗ hổng bypass IDOR khi caller chỉ khớp ResourceId nhưng thuộc loại phạm vi khác (ví dụ: token của trường học không thể tạo, đọc, hủy hoặc liệt kê job thuộc kỳ thi cấp Bộ `MinistryExam`).
        - Bắt buộc kiểm soát quyền sở hữu: Trong cùng một scope, chỉ người tạo (`job.CreatedBy == requesterId`) mới có quyền tra cứu chi tiết, hủy và liệt kê job.
        - Bắt buộc truyền tham số `JobScope requesterScope` (loại bỏ giá trị mặc định `= null`) trong hợp đồng `IJobTracker` và `JobService` để trình biên dịch phát hiện ngay lập tức các lời gọi thiếu phạm vi.
        - Tại `POST /api/v1/jobs`: Từ chối ngay với HTTP **403 Forbidden** (`SCOPED_ACCESS_DENIED`) nếu người gọi thiếu scope xác thực hoặc cố tình tạo job với `scopeKind` hoặc `scopeResourceId` không thuộc quyền của mình.
    3. **Hàng rào Fencing dựa trên Lease Token & Trạng thái Attempt:**
        - Các phương thức `HeartbeatAsync`, `UpdateProgressAsync`, `CompleteJobAsync`, `FailJobAsync` kiểm tra tính hợp lệ của `LeaseToken` và trạng thái `AttemptStatus.Started`. Trước khi recovery chạy, commit của worker vẫn được hoàn tất; sau khi recovery đánh dấu `Abandoned` và cấp phát lại lượt thử mới, token cũ lập tức bị fencing từ chối hoàn toàn.
    4. **Đồng bộ đa tiến trình an toàn & Ghi đĩa nguyên tử:**
        - `FileJobStore.ExecuteWithLock` ném `TimeoutException` nếu không lấy được mutex sau 5s, ngăn ngừa chạy trùng và mất mát dữ liệu snapshot.
        - Dưới khóa Mutex, luôn gọi `LoadFromDisk()` để tránh bỏ sót các lần ghi cùng tick. Lan truyền ngoại lệ rõ ràng khi đọc hoặc deserialize snapshot thất bại, ngăn chặn `SaveToDisk()` ghi đè phá hủy dữ liệu hiện có.
        - Phương thức `SaveToDisk` ghi JSON vào file tạm `*.tmp` rồi dùng `File.Move(..., overwrite: true)` để đảm bảo tính nguyên tử (atomic) trên file system, không nuốt lỗi ghi đĩa.
        - Bắt buộc cấu hình đường dẫn tuyệt đối dùng chung `Jobs:StorePath` cho cả API và Worker, từ chối khởi động nếu thiếu hoặc không phải đường dẫn tuyệt đối để đảm bảo hai host truy cập cùng kho job.
        - Hỗ trợ cờ cấu hình `Jobs:DisableEmbeddedWorker=true` cho API, cho phép tắt `JobWorkerHostedService` trong tiến trình Web API khi triển khai cụm Worker độc lập hoặc phục vụ kiểm thử cô lập chuyên biệt.
        - Chuẩn hóa `Scope.ResourceId.ToUpperInvariant()` khi băm Fingerprint và tạo khóa IdempotencyKey ở cả `FileJobStore` và `InMemoryJobStore`, bảo đảm các yêu cầu chỉ khác nhau chữ hoa/thường không tạo ra nhiều job trùng lặp. Đồng thời, `ListByScopeAsync` lọc `ResourceId` không phân biệt chữ hoa chữ thường (`OrdinalIgnoreCase`) ở cả hai kho lưu trữ.
        - Triển khai cơ chế di trú (migration) có phiên bản (`SchemaVersion = 2`) trong `FileJobStore.LoadFromDisk()`: tự động phát hiện snapshot từ phiên bản cũ (thiếu `SchemaVersion` hoặc phiên bản 1), nâng cấp toàn bộ khóa `IdempotencyIndex` và tính lại `RequestFingerprint` theo thuật toán chuẩn hóa hoa/thường mới nhất, sau đó ghi đè snapshot đã nâng cấp lên đĩa để bảo toàn tính lũy quyền (idempotency) và ngăn chặn phát sinh `JobConflictException`.
    5. **Xác thực dữ liệu đầu vào chuẩn RESTful:**
        - Kiểm tra `Enum.IsDefined` cho `JobScopeKind`, xử lý `ArgumentException` từ `JobScope.Create` để trả về HTTP **400 Bad Request** khi tham số rỗng hoặc enum không xác định (ví dụ `scopeKind=999` hoặc `scopeResourceId` chỉ có khoảng trắng).
    6. **Bổ sung kiểm thử tích hợp End-to-End & Regression Security:**
        - Smoke test `tests/smoke/test_api_health.py` tự động kiểm tra liveness, mã lỗi 401/403 cho các trường hợp không xác thực, giả mạo header, tạo job chéo trường / chéo loại phạm vi (`scopeKind`), Problem Details 404, và luồng đẩy job qua HTTP API -> Worker trong API nhận xử lý -> kiểm chứng đạt trạng thái `Completed`.
        - Bổ sung kiểm thử tích hợp xác nhận tiến trình Worker độc lập (out-of-process Worker) chia sẻ cùng `Jobs:StorePath` tự động tiếp nhận và xử lý hoàn tất job do API tạo trong môi trường API đã tắt embedded worker (`Jobs:DisableEmbeddedWorker=true`), kiểm tra tiến trình Worker độc lập vẫn sống đến khi hoàn tất.
        - Bổ sung ca hồi quy xác nhận báo lỗi ngay nếu thiếu assembly của Worker hoặc Worker độc lập thoát sớm trước khi job được xử lý.
        - Tối ưu hóa kiểm thử Worker thoát sớm trong `test_api_health.py`: dừng Worker trước khi enqueue job để loại bỏ hoàn toàn hiện tượng race condition; bổ sung ca hồi quy mô phỏng Worker thoát siêu nhanh (0ms) xác nhận trạng thái job luôn giữ nguyên `Pending` và không phụ thuộc vào thời điểm terminate.
        - Cố định đồng bộ cả `ASPNETCORE_ENVIRONMENT` và `DOTNET_ENVIRONMENT` cho các tiến trình test; bổ sung kiểm thử hồi quy xác nhận tiến trình Production từ chối token dev và endpoint dev trả về 404 bất kể biến môi trường của tiến trình cha.
    7. **Bảo mật cơ chế xác thực Token theo nguyên tắc Zero-Trust (CWE-287, CWE-862, CWE-798):**
        - Loại bỏ hoàn toàn việc phân tích chuỗi Bearer tự do từ client (`userId:scopeId`).
        - Triển khai cơ chế xác thực Bearer Token do máy chủ kiểm soát: trong môi trường phát triển (Development), sử dụng danh mục token nội bộ cách ly kèm theo loại phạm vi (`ScopeKind`); trong môi trường triển khai (Production), bắt buộc đọc thông tin từ cấu hình bí mật (`JobEngine:TrustedTokens`), tuyệt đối từ chối token phát triển cố định với HTTP **401 Unauthorized**.
        - Giao diện Status UI Dashboard không nhúng cứng bearer token trong JavaScript tĩnh, hỗ trợ tự động lấy token phiên từ endpoint dev an toàn hoặc cho phép người dùng tự nhập. Bổ sung kiểm thử tự động ngăn chặn truy cập trái phép và vượt quyền giữa các phạm vi trường học (HTTP 401/403).

---

## 3. Tổng Hợp Bằng Chứng Kiểm Thử Tự Động (Test Verification Evidence)

- **Kiểm thử tự động Module Jobs:** 43 phương thức kiểm thử (45 test executions: 42 executions trong [JobEngineExecutionTests.cs](../../tests/Modules/Jobs.Tests/JobEngineExecutionTests.cs) và 3 executions trong [JobContractTests.cs](../../tests/Modules/Jobs.Tests/JobContractTests.cs)) **PASSED 100%**:
    - `Enqueue_returns_same_job_on_identical_fingerprint`: PASS
    - `Enqueue_throws_conflict_on_mismatched_fingerprint`: PASS
    - `Competing_workers_only_one_claims_job`: PASS
    - `Heartbeat_renews_for_active_lease_and_fences_stale_tokens`: PASS
    - `Fencing_prevents_stale_worker_from_completing_job`: PASS
    - `Transient_failure_schedules_bounded_retries_and_exhausts_at_attempt_3`: PASS
    - `Successful_retry_clears_job_errors_and_preserves_attempt_history` (2 DataRows): PASS
    - `Job_status_and_list_preserve_job_type` (2 DataRows): PASS
    - `Cancellation_marks_pending_job_cancelled_immediately`: PASS
    - `Stale_lease_recovery_reclaims_abandoned_job`: PASS
    - `Job_service_enqueue_and_tracker_list_respects_scope`: PASS
    - `Same_scope_different_user_cannot_read_or_cancel_job`: PASS
    - `Worker_service_executes_sample_handler_to_completion`: PASS
    - `Cross_school_read_is_denied_with_unauthorized_access_exception`: PASS
    - `Missing_scope_on_read_or_cancel_or_list_throws_unauthorized_access_exception`: PASS
    - `Cross_scope_kind_access_with_matching_resource_id_is_rejected`: PASS
    - `Case_insensitive_resource_id_in_store_list_returns_matching_jobs`: PASS
    - `Case_insensitive_resource_id_creates_same_idempotency_and_fingerprint`: PASS
    - `Commit_with_expired_lease_succeeds_before_recovery_and_fences_after_recovery`: PASS
    - `FileJobStore_commit_with_expired_lease_succeeds_before_recovery_and_fences_after_recovery`: PASS
    - `FileJobStore_preserves_queued_jobs_across_restart_and_instances`: PASS
    - `Exam_generation_and_csv_import_handlers_are_registered_and_executable`: PASS
    - `Job_scope_create_throws_argument_exception_on_whitespace_or_undefined_enum`: PASS
    - `Job_service_list_cross_school_throws_unauthorized_access_exception`: PASS
    - `Legacy_snapshot_is_migrated_and_preserves_idempotency_via_job_service`: PASS
    - `Postgres_migration_script_enforces_case_insensitive_unique_idempotency_index_and_scope_index`: PASS
    - `Legacy_handler_overload_is_invoked_and_returns_success_through_default_interface_implementation`: PASS
    - `Worker_commits_job_as_completed_even_when_host_token_is_cancelled_immediately_after_handler_success`: PASS
    - `FileJobStore_claim_on_expired_lease_applies_backoff_cooldown`: PASS
    - `Handler_without_overrides_fails_and_does_not_report_completed`: PASS
    - `TaskCanceledException_when_job_token_not_cancelled_fails_as_transient`: PASS
    - `Worker_fails_job_as_permanent_failure_even_when_host_token_is_cancelled`: PASS
    - `CancelJobAsync_throws_key_not_found_exception_when_job_does_not_exist`: PASS
    - `Worker_id_is_unique_and_preserves_guid_even_with_long_machine_name`: PASS
    - `Cancellation_watcher_cancels_running_job_when_handler_does_not_report_progress`: PASS
    - `Cancellation_watcher_survives_transient_read_fault_and_propagates_cancellation`: PASS
    - `Heartbeat_kept_alive_during_terminal_write_prevents_lease_expiration_and_duplicate_execution`: PASS
    - `Worker_does_not_fail_job_as_unhandled_exception_when_complete_step_throws_exception`: PASS
    - `FileJobStore_reloads_changes_across_instances_even_when_mtime_is_reset`: PASS
    - `Progress_report_failure_does_not_fail_or_retry_job`: PASS
    - `Scope_requires_a_valid_kind_and_resource`: PASS
    - `Request_preserves_scope_and_correlation_id`: PASS
    - `Request_rejects_missing_idempotency_key`: PASS
- **Chất lượng toàn Solution:** 54/54 test executions toàn hệ thống PASSED (45 Jobs + 3 Questions + 3 Exams + 3 Architecture).
- **Biên dịch nghiêm ngặt:** `dotnet build AiExamBank.slnx --warnaserror` đạt 0 Warning, 0 Error.
- **Vệ sinh Repository:** `python tools/check_repository.py` đạt 0 Issues trên toàn bộ files.
- **Tích hợp API & Worker Smoke Test:** `python tests/smoke/test_api_health.py` PASSED (bao gồm liveness, Problem Details 404, kiểm tra phạm vi 400/403, kiểm tra JobType và API Job Enqueue -> Worker Complete).

---

## 4. Hướng Dẫn Vận Hành & Lệnh Thực Thi Nhanh

Trong mỗi terminal PowerShell, bắt đầu từ thư mục gốc repository và đặt cùng đường dẫn tuyệt đối trước khi chạy host hoặc chuyển thư mục:

```powershell
$repoRoot = (Get-Location).Path
$env:Jobs__StorePath = Join-Path $repoRoot "bin/jobs-store.json"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
```

Khi chạy Worker độc lập ở terminal thứ hai, đặt `$env:Jobs__DisableEmbeddedWorker = "true"` trong terminal API trước khi chạy API.

| Mục đích                     | Lệnh thực thi từ thư mục gốc                   | Ghi chú                                          |
| ---------------------------- | ---------------------------------------------- | ------------------------------------------------ |
| **Chạy Web API & Dashboard** | `dotnet run --project src/Api --urls http://localhost:5000` | Mở trình duyệt vào `http://localhost:5000/jobs/` |
| **Chạy Worker độc lập**     | `dotnet run --project src/Worker`             | Dùng cùng `Jobs__StorePath` như API               |
| **Tắt Server**               | Bấm `Ctrl + C` trong Terminal                  | Hoặc gõ `Stop-Process -Name dotnet`              |
| **Chạy toàn bộ 54 Tests**    | `dotnet test AiExamBank.slnx`                  | Kiểm tra tất cả module của cả nhóm               |
| **Chạy riêng 45 Tests Jobs** | `dotnet test tests/Modules/Jobs.Tests`         | Kiểm tra riêng tính năng Background Jobs         |
| **Kiểm tra độ sạch Repo**    | `python tools/check_repository.py`             | Đảm bảo không dính secret key, link hỏng         |
