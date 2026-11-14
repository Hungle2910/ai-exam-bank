# M5-05: Application Metrics, Alarms và SNS - Tài Liệu Thiết Kế Kỹ Thuật

- **Tác giả:** Gia Bảo (@TranGiaBao2005) — Role M5: Reliability & ML Evaluation Engineer
- **Mã Task / Issue:** [#25](https://github.com/Hungle2910/ai-exam-bank/issues/25) · **Sprint:** Tuần W05
- **Trạng thái:** Hoàn tất triển khai mã nguồn, telemetry emitter, CloudWatch/SNS Terraform IaC, UI Telemetry Bar, và 100% kiểm thử (139/139 unit tests). · **Nhánh Git:** `feat/M5-05-metrics-alarms-sns`
- **Phiên bản:** Release `v1.3.0-rc1` · Telemetry Meter `AiExamBank.Jobs/1.0.0`
- **Tài liệu tham chiếu:** [M5_05_SNS_RUNBOOK.md](M5_05_SNS_RUNBOOK.md), [M5_05_EXPLANATION_HISTORY.md](M5_05_EXPLANATION_HISTORY.md), [ADR 0007](../adr/0007-failure-handling-stale-lease-recovery-resilience.md)

---

## 🎯 1. Mục Tiêu & Phạm Vi (Goals & Scope)

### 1.1 Vấn đề cốt lõi cần giải quyết
1. **Thiếu khả năng quan sát vận hành thời gian thực (Zero Observability):** Đội ngũ vận hành không nắm được tỷ lệ thành công/thất bại, thời gian hoàn thành (P50, P95 latency) của các tác vụ AI và dữ liệu lớn.
2. **Nguy cơ bão thông báo sự cố (Alert Storm):** Khi nhà cung cấp AI gặp sự cố ngắt kết nối, hàng nghìn job thất bại liên tiếp có thể làm nghẽn kênh SNS/Email, gây chi phí cao và làm tê liệt kênh liên lạc trực ca.
3. **Rò rỉ thông tin nhạy cảm qua kênh cảnh báo (CWE-209):** Thông điệp sự cố gửi qua SNS/CloudWatch nếu chứa raw stack trace, secret key (AKIA...) hoặc chuỗi kết nối DB sẽ vi phạm nghiêm trọng chính sách bảo mật dữ liệu.
4. **Không có kênh kích hoạt diễn tập chủ động (No Drill Trigger):** Khó xác minh tính toàn vẹn của chuỗi cảnh báo (Alert Delivery Chain) nếu không có cơ chế mô phỏng an toàn.

### 1.2 Giải pháp kiến trúc
1. **Telemetry Metrics Emitter (`JobMetrics`):**
   - Tích hợp chuẩn `System.Diagnostics.Metrics.Meter` ("AiExamBank.Jobs").
   - Ghi nhận: Enqueued, Started, Completed, Failed, Retries, Stale Lease Recoveries, Provider Errors, Execution Duration.
   - Tính toán phân vị độ trễ (P50, P90, P95) và tỷ lệ thành công qua cửa sổ trượt (Sliding Window 500 mẫu).
   - Đảm bảo **Low-cardinality Dimensions**: Chỉ lưu `job_type`, `scope_kind`, `error_category` — tuyệt đối không đưa user ID, question ID, token vào metric dimensions.
2. **Dịch vụ Định tuyến & Bắn Cảnh báo Amazon SNS (`SnsAlertPublisher`):**
   - Hỗ trợ chế độ Mock Simulation cho môi trường Local/Staging và tích hợp AWS Topic ARN thực tế.
   - Cơ chế **Throttling chống Alert Storm**: Khóa cooldown theo cặp `{IncidentType}:{JobType}:{Provider}` (mặc định 300 giây).
   - Cho phép bypass cooldown với loại sự cố diễn tập `DRILL_TEST_ALERT`.
   - Khử thông tin nhạy cảm bắt buộc qua `AwsErrorMapping.SanitizeForClient` (CWE-209).
3. **Bảng Điều Khiển Telemetry trên Status UI (`/jobs/`):**
   - KPI Bar thời gian thực: Tỷ lệ thành công (%), Thời gian trung bình, Phân vị P95, Số ca cấp cứu stale lease, Trạng thái Circuit Breaker (Closed/Open/HalfOpen), và Lịch sử cảnh báo gần nhất.
   - Nút bấm và Modal "Diễn Tập SNS" hỗ trợ kích hoạt test alert có kiểm soát.
4. **Hạ Tầng Dưới Dạng Mã Nguồn (Terraform IaC):**
   - Module `Backend/infrastructure/modules/observability/`: Tạo SNS Topic `ai-exam-bank-job-engine-incidents` và 3 CloudWatch Metric Alarms (CircuitBreakerTrip, HighFailureRate, StaleWorkerLeases).

---

## 🏛️ 2. Danh Mục Metrics (Metric Taxonomy & Dimensions)

| Tên Metric (CloudWatch/OTel) | Loại Nhạc Cụ (Instrument) | Đơn Vị | Dimensions Chuẩn Hóa | Mô Tả |
|---|---|---|---|---|
| `jobs_enqueued_total` | Counter | Count | `job_type`, `scope_kind` | Tổng số tác vụ được xếp hàng vào hệ thống |
| `jobs_started_total` | Counter | Count | `job_type` | Tổng số tác vụ đã được Worker bắt đầu xử lý |
| `jobs_completed_total` | Counter | Count | `job_type` | Số tác vụ xử lý thành công |
| `jobs_failed_total` | Counter | Count | `job_type`, `error_category`, `error_code` | Số tác vụ thất bại theo phân loại lỗi |
| `jobs_retried_total` | Counter | Count | `job_type`, `is_manual`, `attempt` | Số lần thử lại (tự động hoặc thủ công) |
| `job_stale_lease_recoveries_total` | Counter | Count | `job_type` | Số lần phát hiện và cấp cứu worker lease bị kẹt |
| `job_execution_duration_seconds` | Histogram | Seconds | `job_type`, `status` | Phân bố thời gian thực thi của tác vụ |
| `provider_errors_total` | Counter | Count | `provider`, `error_code`, `is_transient` | Số lượng lỗi ghi nhận từ AWS Bedrock, S3, v.v. |

---

## 🚨 3. Phân Loại Sự Cố Cần Hành Động (Actionable Incidents)

| Loại Sự Cố (`IncidentType`) | Mức Độ | Ngưỡng Kích Hoạt | Kênh Cảnh Báo | Hành Động Xử Lý |
|---|---|---|---|---|
| `CIRCUIT_BREAKER_OPEN` | **CRITICAL** | 5 lỗi AI liên tiếp trong 60s | SNS -> Email/Ops | Kiểm tra AWS Bedrock service health, quota và IAM credentials |
| `STALE_LEASE_DETECTED` | **WARNING** | >= 3 stale leases trong 1 lần scan | SNS -> Slack/DevOps | Kiểm tra worker container/EC2 CPU, OOM killer, hoặc phân vùng mạng |
| `RETRY_BUDGET_EXHAUSTED` | **WARNING** | Job chạm mốc 3 attempts thất bại | SNS -> Support | Kiểm tra dữ liệu đầu vào và format file import |
| `HIGH_FAILURE_RATE` | **WARNING** | Tỷ lệ lỗi > 10% trong 5 phút | CloudWatch Alarm -> SNS | Rà soát lỗi ứng dụng và dịch vụ phụ thuộc |
| `DRILL_TEST_ALERT` | **INFO** | Kích hoạt thủ công từ UI / API | SNS Mock Dispatch | Xác nhận đường truyền và định dạng payload cảnh báo |
