# M5-04: Failure Drill Runbook & Recovery Evidence

- **Phiên bản phát hành:** `v1.2.0-rc1`
- **Phiên bản Job Engine:** `job-engine/v1.2`
- **Tác giả:** Trần Gia Bảo (@TranGiaBao2005) — Role M5: Reliability & ML Evaluation Engineer
- **Mã Task / Issue:** [#20](https://github.com/Hungle2910/ai-exam-bank/issues/20) · **Sprint:** Tuần W04
- **Tài liệu liên quan:** [M5_04_FAILURE_HANDLING_DESIGN.md](M5_04_FAILURE_HANDLING_DESIGN.md), [ADR 0007](../adr/0007-failure-handling-stale-lease-recovery-resilience.md), [ADR 0005](../adr/0005-durable-worker-retry-idempotency.md)
- **Công cụ thực thi tự động:** [run_failure_drills.py](../../tools/run_failure_drills.py)
- **Dữ liệu minh chứng đã khử trùng:** [drill_evidence.json](drill_evidence.json)

---

## 📋 1. Checklist Diễn Tập Sự Cố (Failure Drill Checklist)

### 1.1 Pre-Flight Checklist (Chuẩn bị trước diễn tập)
- [x] **SDK & Runtime:** Kiểm tra runtime .NET 10 và Worker daemon sẵn sàng (`AiExamBank.Worker` hoặc In-Process Worker).
- [x] **IAM Roles & Permissions:** Worker sở hữu quyền gọi `bedrock:InvokeModel` và đọc/ghi bucket S3 `ai-exam-bank-corpus-*`.
- [x] **Durable Store:** Thư mục lưu trữ tác vụ bền vững khả dụng (`%LOCALAPPDATA%/AiExamBank/Jobs` hoặc Windows Named Mutex).
- [x] **Zero Information Leak:** Cơ chế khử trùng lỗi `AwsErrorMapping.SanitizeForClient` đang hoạt động (chặn 100% stack trace và secret tokens).

### 1.2 Execution Checklist (Các bước diễn tập)
- [x] **Drill 1 - Bedrock Throttling:** Gửi request sinh đề vượt ngưỡng hạn ngạch AI. Xác nhận hệ thống phân loại `Transient`, dời lịch thử lại có jitter, không làm rơi job.
- [x] **Drill 2 - Process Kill (Sập Worker đột ngột):** Kill cưỡng bức tiến trình Worker khi đang xử lý job `Running`. Xác nhận sau 2 phút cơ chế `RecoverExpiredLeasesAsync` phát hiện lease quá hạn và thu hồi về `Pending`.
- [x] **Drill 3 - AI Provider Circuit Breaker:** Mô phỏng sự cố mạng kéo dài khiến 5 request AI liên tiếp thất bại trong 60s. Xác nhận Circuit Breaker ngắt `Open`, từ chối ngay với `CIRCUIT_BREAKER_OPEN`. Sau 60s tự động chuyển `HalfOpen` thăm dò.
- [x] **Drill 4 - S3 Permanent Error & Retry Eligibility:** Mô phỏng request import file S3 không tồn tại (`NoSuchKey`). Xác nhận hệ thống chuyển ngay sang `Failed`, khóa nút `[🚫 Không thể thử lại]` trên giao diện.

### 1.3 Post-Drill Verification & Rollback
- [x] **Khôi phục tiến trình Worker:** Khởi động lại Worker daemon bình thường.
- [x] **Xác nhận không tồn đọng Stale Lease:** Tất cả các job đều có trạng thái rõ ràng (`Completed`, `Failed`, hoặc `Cancelled`), không có job nào kẹt spinner vô hạn.
- [x] **Vệ sinh dữ liệu tạm:** Thu hồi các file `.tmp` nếu có trong thư mục file store.

---

## 🔬 2. Ví Dụ Khôi Phục Thực Tế (Redacted Recovery Examples)

Dữ liệu minh chứng dưới đây đã được lọc khử trùng (Redacted), bảo vệ toàn bộ thông tin nhạy cảm, ARN tài khoản, và token xác thực theo tiêu chuẩn an ninh CWE-209.

### 2.1 Ví Dụ 1: Khôi Phục Job Bị Treo Khi Worker Sập (Stale Lease Recovery)

#### Trạng thái trước khi khôi phục (Worker bị kill khi đang chạy):
```json
{
  "jobId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "jobType": "ExamGeneration",
  "status": "Running",
  "attemptCount": 1,
  "currentAttempt": {
    "attemptNumber": 1,
    "status": "Started",
    "workerId": "worker-aws-apse1-crash-sim",
    "startedAt": "2026-10-27T10:50:00.000Z",
    "leaseExpiresAt": "2026-10-27T10:52:00.000Z"
  }
}
```

#### Trạng thái sau khi được cấp cứu bởi `RecoverExpiredLeasesAsync`:
```json
{
  "jobId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "jobType": "ExamGeneration",
  "status": "Pending",
  "attemptCount": 1,
  "maxAttempts": 3,
  "errorCode": "STALE_LEASE_RECOVERED",
  "safeMessage": "Tiến trình xử lý bị mất kết nối đột ngột. Tác vụ đã được tự động cấp cứu và khôi phục.",
  "isRetryEligible": true,
  "actionableReason": "Tiến trình xử lý bị mất kết nối đột ngột. Tác vụ đã được tự động cấp cứu và khôi phục.",
  "availableAt": "2026-10-27T10:53:15.420Z",
  "attemptsHistory": [
    {
      "attemptNumber": 1,
      "status": "Abandoned",
      "workerId": "worker-aws-apse1-crash-sim",
      "executionDurationMs": 120450,
      "safeErrorMessage": "Lượt thực thi bị bỏ rơi do tiến trình Worker sập hoặc mất liên lạc quá hạn lease (Heartbeat timeout)."
    }
  ]
}
```

---

### 2.2 Ví Dụ 2: Xử Lý Throttling Bedrock & Tự Động Thử Lại Có Jitter

```json
{
  "jobId": "8b3f6e10-912a-4c22-b891-729f8a31e012",
  "jobType": "ExamGeneration",
  "status": "Pending",
  "attemptCount": 1,
  "maxAttempts": 3,
  "errorCode": "AWS_BEDROCK_THROTTLED",
  "safeMessage": "Lỗi tạm thời khi kết nối AI Bedrock (Throttling / Quota). Hệ thống sẽ tự động thử lại.",
  "actionableReason": "Dịch vụ AI đang quá tải lượt yêu cầu. Hệ thống đang tự động hoãn và thử lại.",
  "isRetryEligible": true,
  "availableAt": "2026-10-27T10:55:32.180Z",
  "attemptsHistory": [
    {
      "attemptNumber": 1,
      "status": "Failed",
      "workerId": "worker-aws-apse1-02",
      "errorCode": "AWS_BEDROCK_THROTTLED",
      "safeErrorMessage": "Lỗi tạm thời khi kết nối AI Bedrock (Throttling / Quota). Hệ thống sẽ tự động thử lại.",
      "executionDurationMs": 4250
    }
  ]
}
```

---

### 2.3 Ví Dụ 3: Ngắt Mạch Circuit Breaker Khi Dịch Vụ AI Gặp Sự Cố Diện Rộng

Khi có 5 lỗi Bedrock liên tiếp trong vòng 60 giây, Circuit Breaker lập tức chuyển sang trạng thái `Open`. Các yêu cầu mới sẽ bị ngắt sớm (Fail-Fast):

```json
{
  "jobId": "c19a4e32-55fb-41aa-8812-332e1289dfaa",
  "jobType": "ExamGeneration",
  "status": "Pending",
  "errorCode": "CIRCUIT_BREAKER_OPEN",
  "safeMessage": "Dịch vụ AI tạm thời bị ngắt mạch bảo vệ do sự cố kéo dài từ nhà cung cấp. Yêu cầu sẽ tự động hoãn lại.",
  "actionableReason": "Dịch vụ AI đang tạm ngưng do sự cố diện rộng. Hệ thống sẽ tự động thử lại khi ổn định.",
  "isRetryEligible": true,
  "availableAt": "2026-10-27T11:01:00.000Z"
}
```

---

### 2.4 Ví Dụ 4: Lỗi Vĩnh Viễn (Permanent Error) & Khóa Nút Thử Lại

Khi file import không tồn tại trên Amazon S3 (`NoSuchKey`), hệ thống đánh dấu `Failed` ngay lập tức và vô hiệu hóa nút retry:

```json
{
  "jobId": "e5512bc9-0a32-4112-9c44-77a8b11c9921",
  "jobType": "CsvImport",
  "status": "Failed",
  "progressPercentage": null,
  "resultId": null,
  "errorCode": "AWS_S3_FILE_NOT_FOUND",
  "safeMessage": "Không tìm thấy file tài liệu hoặc dữ liệu trên kho lưu trữ đám mây Amazon S3.",
  "actionableReason": "Không tìm thấy file tài liệu hoặc file import trên kho lưu trữ đám mây S3. Vui lòng kiểm tra lại đường dẫn file.",
  "isRetryEligible": false
}
```

---

## 🌐 3. Đặc Tả Hợp Đồng OpenAPI 3.0 (OpenAPI Contract)

```yaml
openapi: 3.0.3
info:
  title: AI Exam Bank Background Jobs API
  version: 1.2.0-rc1
  description: API quản lý tác vụ ngầm bền vững, tự phục hồi và bảo vệ hạ tầng AI/Cloud.
paths:
  /api/v1/jobs/{id}:
    get:
      summary: Tra cứu trạng thái chi tiết của tác vụ nền
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
            format: uuid
        - name: X-Scope-Id
          in: header
          required: true
          schema:
            type: string
            example: school-hn-01
      responses:
        '200':
          description: Chi tiết trạng thái tác vụ
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/JobApiResponse'
        '401':
          description: Chưa xác thực danh tính
        '403':
          description: Từ chối truy cập do vi phạm Scope phân quyền
        '404':
          description: Không tìm thấy tác vụ tương ứng

components:
  schemas:
    JobApiResponse:
      type: object
      required:
        - jobId
        - jobType
        - status
      properties:
        jobId:
          type: string
          format: uuid
          example: f47ac10b-58cc-4372-a567-0e02b2c3d479
        jobType:
          type: string
          example: ExamGeneration
        status:
          type: string
          enum: [Pending, Running, Completed, Failed, Cancelled]
          example: Failed
        progressPercentage:
          type: integer
          nullable: true
          minimum: 0
          maximum: 100
          example: null
        resultId:
          type: string
          nullable: true
          example: null
        errorCode:
          type: string
          nullable: true
          example: AWS_BEDROCK_QUOTA_EXCEEDED
        safeMessage:
          type: string
          nullable: true
          example: Hạn ngạch gọi AI trong tháng đã hết.
        isRetryEligible:
          type: boolean
          nullable: true
          description: Cho biết người dùng có được phép bấm nút Thử lại hay không
          example: false
        actionableReason:
          type: string
          nullable: true
          description: Hướng dẫn hành động cụ thể, thân thiện cho người dùng cuối
          example: Hạn ngạch gọi AI trong tháng đã hết. Vui lòng liên hệ Quản trị viên để nâng gói.
```

---

## ⚙️ 4. Thông Số Cấu Hình Hệ Thống (Configuration Reference)

| Tham Số Cấu Hình | Giá Trị Mặc Định | Ý Nghĩa Kỹ Thuật |
|---|---|---|
| `LeaseDuration` | `120 giây` | Thời hạn tối đa cho một lượt cấp phép thực thi trước khi bị coi là stale lease nếu không gia hạn heartbeat. |
| `HeartbeatInterval` | `30 giây` | Tần suất worker gửi tín hiệu gia hạn quyền thực thi (`LeaseExpiresAt = UtcNow + LeaseDuration`). |
| `RecoveryInterval` | `30 giây` | Tần suất vòng lặp `RecoverExpiredLeasesAsync` quét và thu hồi các job bị sập worker. |
| `CircuitBreaker.FailureThreshold` | `5 lần` | Số lỗi liên tiếp kích hoạt ngắt mạch sang trạng thái `Open`. |
| `CircuitBreaker.CooldownPeriod` | `60 giây` | Thời gian mạch duy trì trạng thái `Open` trước khi chuyển sang `HalfOpen` để probe thử nghiệm. |
| `Retry.MaxAttempts` | `3 lần` | Ngân sách thử lại tự động tối đa trước khi đánh dấu thất bại vĩnh viễn (`RETRY_BUDGET_EXHAUSTED`). |
| `Retry.BaseDelay` | `30 giây` | Thời gian chờ cơ sở ban đầu cho thuật toán Exponential Backoff. |
| `Retry.MaxDelay` | `15 phút` | Thời gian chờ tối đa cho một lần hoãn chạy lại tác vụ. |
| `Retry.JitterFactor` | `±20%` | Biên độ ngẫu nhiên dãn cách lượt request, triệt tiêu giông bão yêu cầu (Thundering Herd). |
