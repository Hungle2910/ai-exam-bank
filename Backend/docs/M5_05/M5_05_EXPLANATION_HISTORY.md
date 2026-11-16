# M5-05: Lịch Sử Triển Khai & Quyết Định Kỹ Thuật (Explanation History)

- **Người thực hiện:** Gia Bảo (@TranGiaBao2005) — Role M5: Reliability & ML Evaluation Engineer
- **Issue:** [#25](https://github.com/Hungle2910/ai-exam-bank/issues/25) · **Sprint:** Tuần W05
- **Tài liệu tham chiếu:** [M5_05_METRICS_DESIGN.md](M5_05_METRICS_DESIGN.md), [M5_05_SNS_RUNBOOK.md](M5_05_SNS_RUNBOOK.md)

---

## 🏛️ 1. Bối Cảnh & Các Quyết Định Kiến Trúc

### 1.1 Tách biệt Logging, Telemetry và Options trong Module Jobs
- **Vấn đề gặp phải:** Dự án sử dụng mô hình Modular Monolith tinh gọn, trong đó các Class Library Modules (như `AiExamBank.Modules.Jobs`) không phụ thuộc trực tiếp vào các package nặng của ASP.NET Core (`Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`).
- **Quyết định kỹ thuật:**
  - `SnsAlertPublisher` được thiết kế độc lập, nhận cấu hình dạng POCO `SnsAlertOptions` và ủy thác delegate ghi log `Action<string>?`.
  - Tại tầng ứng dụng (`AiExamBank.Api/Program.cs` và `AiExamBank.Worker/Program.cs`), DI Container sử dụng factory lambda để inject giá trị từ `IOptions<SnsAlertOptions>` và `ILogger<SnsAlertPublisher>` vào `SnsAlertPublisher`.
  - Kết quả: Module giữ vững nguyên lý Clean Architecture và Dependency Inversion, không bị ô nhiễm bởi các framework dependencies không cần thiết.

### 1.2 Tuân thủ nghiêm ngặt Low-Cardinality Dimensions
- **Vấn đề:** Ghi nhận các trường như User ID, Question ID, hoặc Job ID vào CloudWatch Metric Dimensions sẽ dẫn đến hiện tượng bùng nổ chiều không gian (Dimension Explosion), gây chi phí CloudWatch tăng vọt và làm giảm hiệu năng phân tích.
- **Giải pháp:** Chỉ cho phép 3 nhóm dimension có số lượng giá trị cố định và hữu hạn:
  - `job_type`: Các loại tác vụ chuẩn (ExamGeneration, CsvImport, Sample).
  - `scope_kind`: Loại phạm vi tổ chức (School, MinistryExam).
  - `error_category`: Phân loại lỗi (Transient, Permanent, Terminal).
  - Mọi chuỗi dimension đều được đi qua hàm lọc `SanitizeDimension` để loại bỏ ký tự đặc biệt và giới hạn độ dài an toàn.

### 1.3 Ngăn Chặn Bão Cảnh Báo (Alert Storm) & Diễn Tập An Toàn
- **Giải pháp:**
  - `SnsAlertPublisher` áp dụng bộ đệm trượt `_lastAlertTimestamps` lưu thời điểm phát cảnh báo gần nhất theo bộ khóa `{IncidentType}:{JobType}:{Provider}`.
  - Cửa sổ hạ nhiệt (Throttle Window) 300 giây ngăn chặn việc phát đi hàng trăm thông báo giống hệt nhau khi hệ thống xảy ra sự cố kéo dài.
  - Cảnh báo diễn tập `DRILL_TEST_ALERT` áp dụng cửa sổ giãn cách riêng `DrillThrottleWindowSeconds` (60 giây) để vừa cho phép diễn tập vừa triệt tiêu nguy cơ spam hoặc DoS kênh cảnh báo.

### 1.4 Giải Quyết 4 Phản Hồi Review Kỹ Thuật (PR #94 Hardening)
1. **Phân biệt rõ ràng Chế độ Mô phỏng (Mock Simulation) và Live AWS SNS:**
   - Tạo abstraction `IAwsSnsClient` độc lập với SDK bên thứ ba, bảo vệ ranh giới kiến trúc Domain Modules.
   - Khi `UseMockSimulation == false`: Bắt buộc cấu hình `TopicArn` và `IAwsSnsClient`. Nếu thiếu hoặc gửi thất bại, hàm `PublishIncidentAlertAsync` trả về `PublishAlertResult.Failed(...)` thay vì giả vờ thành công.
2. **Khai báo Metric Circuit Breaker & Xuất bản chuẩn AWS CloudWatch EMF:**
   - Bổ sung `ObservableGauge<int>` cho metric `ai_provider_circuit_breaker_state` (0=Closed, 1=HalfOpen, 2=Open) trong `JobMetrics`, khớp 100% với cấu hình Alarm trong Terraform `main.tf`.
   - Triển khai định dạng log chuẩn AWS CloudWatch Embedded Metric Format (EMF) với namespace `AiExamBank/Jobs`. CloudWatch Logs tự động phân tích và chuyển đổi thành CloudWatch Metrics với zero overhead.
3. **Phân quyền Zero-Trust & Giới hạn tốc độ Endpoint Metrics:**
   - Chặn toàn bộ truy cập từ tài khoản trường học thông thường (`JobScopeKind.School`) vào `/metrics/summary` và `/metrics/test-alert` (trả về `403 Forbidden`).
   - Chỉ Quản trị viên hệ thống (System Administrator / Cấp Bộ `MinistryExam`) mới có quyền xem chỉ số toàn hệ thống và kích hoạt diễn tập.
   - Áp dụng HTTP 429 Too Many Requests khi diễn tập bị throttled.
4. **Nhất quán Số liệu giữa API Host và Worker Host (Consistent View Across Processes):**
   - Bổ sung `GetStoreMetricsSummaryAsync` vào `IJobStore` (`FileJobStore` và `InMemoryJobStore`).
   - `JobService.GetReliabilitySummaryAsync` đọc trực tiếp từ kho lưu trữ bền vững (Job và Attempt thực tế được lưu qua Named Mutex liên tiến trình). Khi Worker chạy ở tiến trình độc lập xử lý hàng nghìn job, Dashboard trên API Host lập tức phản ánh đầy đủ và chính xác 100%.

---

## 🧪 2. Kết Quả Kiểm Thử Toàn Diện

- **Unit Tests:** Đạt **144/144 tests** vượt qua 100% trong `AiExamBank.Modules.Jobs.Tests` (18 test mới cho M5-05 gồm Live SNS, Drill Throttling, CloudWatch EMF, Percentiles, Stale Lease Alert, Circuit Breaker Trip Alert, và Cross-Process Store Metrics).
- **Solution Tests:** Đạt **161/161 tests** vượt qua 100% trên toàn bộ các project trong solution:
  - `AiExamBank.Modules.Exams.Tests`: 3/3 passed.
  - `AiExamBank.Modules.Questions.Tests`: 3/3 passed.
  - `AiExamBank.Modules.Identity.Tests`: 8/8 passed.
  - `AiExamBank.Architecture.Tests`: 3/3 passed (Kiểm định ranh giới kiến trúc và SDKs).
  - `AiExamBank.Modules.Jobs.Tests`: 144/144 passed.
