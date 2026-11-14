# M5-05: Hướng Dẫn Vận Hành & Ứng Phó Sự Cố Cảnh Báo SNS (Runbook)

- **Người chịu trách nhiệm chính:** Gia Bảo (@TranGiaBao2005) — Role M5: Reliability & ML Evaluation Engineer
- **Mã Task:** M5-05 · **Chủ đề:** Amazon SNS Alerts & CloudWatch Alarms Remediation Runbook
- **Tài liệu liên quan:** [M5_05_METRICS_DESIGN.md](M5_05_METRICS_DESIGN.md), [M5_04_FAILURE_DRILL_RUNBOOK.md](../M5_04/M5_04_FAILURE_DRILL_RUNBOOK.md)

---

## 🎯 1. Nguyên Tắc Trực Ca & Xử Lý Sự Cố (Triage Protocol)

1. **Nhận cảnh báo qua Amazon SNS:**
   - Mỗi cảnh báo gửi qua email/chat bot chứa tiêu đề chuẩn, mức độ (CRITICAL/WARNING/INFO), loại sự cố, và URL runbook này.
   - Kiểm tra trường `Environment` (Development, Staging, Production) để xác định mức độ ưu tiên.
2. **Quy tắc ngăn chặn bão cảnh báo (Alert Storm Mitigation):**
   - Hệ thống tự động kích hoạt cửa sổ khóa cooldown 300 giây (5 phút) cho mỗi loại sự cố.
   - Nếu nhận được cảnh báo, hãy tiến hành xử lý ngay thay vì chờ cảnh báo tiếp theo.
3. **Bảo mật thông tin:**
   - Payload đã được làm sạch tự động theo CWE-209. Nếu cần thông tin chi tiết (Job ID, Worker ID), truy cập trực tiếp Status UI tại `/jobs/` hoặc log tập trung AWS CloudWatch Insights.

---

## 🛠️ 2. Quy Trình Ứng Phó Theo Từng Loại Sự Cố

### 2.1 `CIRCUIT_BREAKER_OPEN` (Mức độ: CRITICAL)
- **Dấu hiệu:** Nhận được thông báo ngắt mạch từ SNS; KPI Bar trên Status UI hiển thị badge màu đỏ `[AWS_BEDROCK: OPEN]`; các yêu cầu sinh đề thi hoặc import AI bị tạm hoãn với mã lỗi `CIRCUIT_BREAKER_OPEN`.
- **Nguyên nhân tiềm ẩn:**
  1. Hạn mức chi phí hoặc token AWS Bedrock hàng tháng đã cạn (`AWS_BEDROCK_QUOTA_EXCEEDED`).
  2. Bị AWS áp dụng giới hạn lượt gọi API (Throttling / HTTP 429).
  3. Quyền hạn IAM role của ứng dụng bị thu hồi hoặc hết hạn.
  4. Khu vực AWS Bedrock (ví dụ `ap-southeast-1` hoặc `us-east-1`) đang gặp sự cố gián đoạn dịch vụ diện rộng.
- **Các bước khắc phục (Action Steps):**
  1. Mở AWS Console -> Amazon Bedrock -> Service Health Dashboard kiểm tra trạng thái khu vực.
  2. Kiểm tra hạn mức sử dụng (Service Quotas -> Bedrock). Nếu chạm ngưỡng, gửi yêu cầu nâng quota hoặc chuyển model dự phòng.
  3. Kiểm tra IAM Role gán cho Backend/Worker EC2/ECS task.
  4. Sau khi sự cố được khắc phục, Circuit Breaker sẽ tự động chuyển sang `HalfOpen` sau 60 giây và tự phục hồi khi có lượt gọi thử nghiệm thành công đầu tiên.

---

### 2.2 `STALE_LEASE_DETECTED` (Mức độ: WARNING)
- **Dấu hiệu:** Thông báo có từ 3 worker leases trở lên bị thu hồi trong một chu kỳ quét; chỉ số Stale Lease Recoveries trên UI tăng đột biến.
- **Nguyên nhân tiềm ẩn:**
  1. Máy chủ Worker bị sập đột ngột (EC2 Spot Instance termination, Out-Of-Memory OOM kill process, hoặc lỗi phần cứng).
  2. Phân vùng mạng (Network Partition) giữa Worker và kho dữ liệu làm mất tín hiệu nhịp tim (Heartbeat 30s).
- **Các bước khắc phục (Action Steps):**
  1. Kiểm tra CloudWatch Metrics của Worker instance: CPU Utilization, Memory Utilization, và Network In/Out.
  2. Kiểm tra container logs qua `journalctl -u aiexambank-worker` hoặc CloudWatch Logs xem có bị SIGKILL/OOM hay không.
  3. Đảm bảo tiến trình Worker đã được systemd/supervisor tự động khởi động lại.
  4. Các tác vụ bị ảnh hưởng đã được hệ thống tự động đưa về `Pending` và áp dụng Exponential Backoff có Jitter để chạy lại an toàn.

---

### 2.3 `RETRY_BUDGET_EXHAUSTED` (Mức độ: WARNING)
- **Dấu hiệu:** Một tác vụ nền đã thử lại đủ 3 lần nhưng vẫn thất bại; trạng thái job chuyển sang `Failed` vĩnh viễn với mã `RETRY_BUDGET_EXHAUSTED`.
- **Nguyên nhân tiềm ẩn:**
  1. Lỗi dữ liệu đầu vào không hợp lệ vượt qua khâu tiền kiểm tra.
  2. File dữ liệu trên Amazon S3 bị hỏng hoặc payload quá lớn gây tràn bộ nhớ liên tục.
- **Các bước khắc phục (Action Steps):**
  1. Truy cập Status UI `/jobs/`, click vào Job ID tương ứng để mở Drawer chi tiết lịch sử attempts.
  2. Kiểm tra `SafeErrorMessage` và lịch sử lỗi của từng attempt (lỗi mạng, timeout hay schema mismatch).
  3. Liên hệ với người tạo job (giáo viên hoặc quản trị viên) để chỉnh sửa file hoặc cấu hình đầu vào, sau đó sử dụng nút `[🔄 Thử lại]` thủ công trên Status UI.

---

### 2.4 `DRILL_TEST_ALERT` (Mức độ: INFO)
- **Dấu hiệu:** Cảnh báo diễn tập xuất phát từ việc bấm nút "Diễn Tập SNS" trên giao diện hoặc gọi API `POST /api/v1/jobs/metrics/test-alert`.
- **Mục đích:** Kiểm tra tính toàn vẹn của chuỗi cảnh báo từ ứng dụng -> SNS Topic -> Hộp thư người nhận.
- **Hành động:** Không cần can thiệp kỹ thuật. Xác nhận email hoặc thông báo đã đến nơi kịp thời.
