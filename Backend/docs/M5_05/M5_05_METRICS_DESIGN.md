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
| `ai_provider_circuit_breaker_state` | ObservableGauge | {state} | `provider`, `Environment` | Trạng thái bộ ngắt mạch AI Bedrock (0=Closed, 1=HalfOpen, 2=Open) |
| `jobs_pending` | ObservableGauge | Count | — | Số lượng tác vụ hiện đang chờ Worker xử lý |
| `jobs_running` | ObservableGauge | Count | — | Số lượng tác vụ hiện đang được Worker thuê thực thi |

---

## 🚨 3. Phân Loại Sự Cố Cần Hành Động (Actionable Incidents)

| Loại Sự Cố (`IncidentType`) | Mức Độ | Ngưỡng Kích Hoạt | Kênh Cảnh Báo | Hành Động Xử Lý |
|---|---|---|---|---|
| `CIRCUIT_BREAKER_OPEN` | **CRITICAL** | 5 lỗi AI liên tiếp trong 60s | SNS -> Email/Ops | Kiểm tra AWS Bedrock service health, quota và IAM credentials |
| `STALE_LEASE_DETECTED` | **WARNING** | >= 3 stale leases trong 1 lần scan | SNS -> Slack/DevOps | Kiểm tra worker container/EC2 CPU, OOM killer, hoặc phân vùng mạng |
| `RETRY_BUDGET_EXHAUSTED` | **WARNING** | Job chạm mốc 3 attempts thất bại | SNS -> Support | Kiểm tra dữ liệu đầu vào và format file import |
| `HIGH_FAILURE_RATE` | **WARNING** | Tỷ lệ lỗi > 10% trong 5 phút | CloudWatch Alarm -> SNS | Rà soát lỗi ứng dụng và dịch vụ phụ thuộc |
| `DRILL_TEST_ALERT` | **INFO** | Kích hoạt thủ công từ UI / API (Rate limited 60s) | SNS Mock / Live Dispatch | Xác nhận đường truyền và định dạng payload cảnh báo |

---

## 🔧 4. Cơ Chế Quan Sát & Bảo Mật Nâng Cao

### 4.1 Xuất Bản Metric Chuẩn AWS CloudWatch Embedded Metric Format (EMF)
- `JobMetrics` tích hợp bộ phát log chuẩn **AWS CloudWatch Embedded Metric Format (EMF)** với namespace `AiExamBank/Jobs`.
- Khi các sự kiện trọng yếu phát sinh (Circuit Breaker chuyển trạng thái sang Open/HalfOpen, Job thất bại, hoặc thu hồi Stale Lease), ứng dụng phát một bản ghi JSON có cấu trúc `_aws` chuẩn hóa.
- CloudWatch Logs / CloudWatch Agent tự động phân tích và tạo Metrics trực tiếp trong CloudWatch Console, kích hoạt ngay lập tức 3 CloudWatch Alarms trong Terraform mà không cần cài đặt thêm daemon bên thứ ba.

### 4.2 Tính Nhất Quán Giữa API Host và Worker Host (Store-Backed Metrics)
- `IJobStore` (`FileJobStore` và `InMemoryJobStore`) cung cấp phương thức `GetStoreMetricsSummaryAsync` tính toán trực tiếp từ các thực thể `BackgroundJob` và `JobAttempt` được chia sẻ qua Named Mutex liên tiến trình.
- Khi Worker Host chạy độc lập và xử lý hàng trăm tác vụ, Dashboard trên API Host vẫn phản ánh chính xác 100% số lượng tác vụ đã hoàn tất, thất bại, đang chạy và độ trễ phân vị P50/P90/P95.

### 4.3 Phân Quyền Zero-Trust & Giới Hạn Tốc Độ (Rate Limiting)
- Hai endpoint `/metrics/summary` và `/metrics/test-alert` yêu cầu thẩm quyền **Quản trị viên hệ thống (System Administrator / MinistryExam)**.
- Token của giáo viên trường học bình thường (`JobScopeKind.School`) bị từ chối với mã **HTTP 403 Forbidden**.
- Cảnh báo diễn tập `DRILL_TEST_ALERT` được áp dụng cửa sổ giãn cách riêng `DrillThrottleWindowSeconds` (60 giây), trả về **HTTP 429 Too Many Requests** nếu bị gọi liên tiếp dồn dập.

### 4.4 Chế Độ Mô Phỏng vs Live AWS SNS
- `UseMockSimulation = true`: Phục vụ an toàn cho Local / Testing / Staging khi chưa có quyền AWS thực tế.
- `UseMockSimulation = false`: Bắt buộc cấu hình `TopicArn` và `IAwsSnsClient`. Nếu thiếu hoặc gọi thất bại, publisher trả về lỗi tường minh, nút diễn tập hiển thị cảnh báo lỗi màu đỏ thay vì báo thành công giả tạo.
