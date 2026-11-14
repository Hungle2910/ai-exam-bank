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
  - Trường hợp đặc biệt: Sự cố diễn tập `DRILL_TEST_ALERT` được cấp đặc quyền bypass kiểm tra cooldown để kỹ sư và kiểm thử viên có thể diễn tập liên tục mà không bị nuốt thông điệp.

---

## 🧪 2. Kết Quả Kiểm Thử Toàn Diện

- **Unit Tests:** Đạt **139/139 tests** vượt qua 100% trong `AiExamBank.Modules.Jobs.Tests` (13 test mới cho M5-05 gồm tính toán P50/P90/P95, khử thông tin nhạy cảm CWE-209, throttling, và tích hợp Circuit Breaker/JobStore).
- **Solution Tests:** Đạt **156/156 tests** vượt qua 100% trên toàn bộ các project trong solution:
  - `AiExamBank.Modules.Exams.Tests`: 3/3 passed.
  - `AiExamBank.Modules.Questions.Tests`: 3/3 passed.
  - `AiExamBank.Modules.Identity.Tests`: 8/8 passed.
  - `AiExamBank.Architecture.Tests`: 3/3 passed.
  - `AiExamBank.Modules.Jobs.Tests`: 139/139 passed.
