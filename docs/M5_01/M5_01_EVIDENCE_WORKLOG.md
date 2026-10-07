# M5-01: Evidence Worklog & Project Tree

## 1. Quá Trình Làm Việc (Process Worklog)
Tài liệu này ghi nhận lại toàn bộ quá trình thực hiện nhiệm vụ **[M5-01] Reliability contracts và telemetry baseline** từ đầu đến cuối để làm bằng chứng (Evidence) cho quá trình nghiệm thu.

### Giai đoạn 1: Thiết kế Database Schema & Job Contracts
- Đã tạo các Entity trong thư mục `src/Entities/BackgroundJobs/` để định nghĩa lược đồ cơ sở dữ liệu:
  - `BackgroundJob.cs`: Chứa `IdempotencyKey` để chống trùng lặp.
  - `JobAttempt.cs`: Chứa `WorkerId` (cơ chế Lease) và `HeartbeatAt` (cơ chế nhịp tim).
  - `JobStatus.cs` & `JobStatusDto.cs`: Quản lý vòng đời tác vụ.
- Đã tạo các Interface (Contracts) trong `src/Contracts/BackgroundJobs/` để làm chuẩn giao tiếp cho team M1 và M2:
  - `IJobPublisher.cs`: Dùng để ném job vào DB.
  - `IJobHandler.cs`: Chứa chính sách `MaxRetries` (Retry Policy).
  - `IJobTracker.cs`: Phục vụ UI truy vấn tiến độ.

### Giai đoạn 2: Thiết kế Telemetry Baseline & Error Matrix
- Đã tạo tài liệu `docs/M5_01_ERROR_MATRIX.md` định nghĩa:
  - Bảng Error Taxonomy: Phân loại chi tiết các lỗi `InfrastructureError`, `TransientError`, `BusinessLogicError` và hướng giải quyết.
  - Telemetry Baseline: Bắt buộc truyền `Correlation ID` qua mọi HTTP Request và không ghi log thông tin nhạy cảm.
- Đã tạo tài liệu `docs/M5_01_METRICS_SLO.md` định nghĩa:
  - Metric Catalog: Các bộ đếm (Counter/Timer) để theo dõi hệ thống.
  - Initial SLO: Cam kết thời gian chờ dưới 10 giây, tỷ lệ lỗi không quá 5%.

### Giai đoạn 3: Thiết kế UI Wireframe & Dataset Spec
- Đã vẽ phác thảo giao diện quản lý ở `docs/M5_01_RELIABILITY_UI_WIREFRAME.md` (bao gồm Nút Retry khi thất bại và tiến độ % công việc).
- Đã lên khung chuẩn Rubric chấm điểm câu hỏi AI ở dạng JSON Schema tại `docs/M5_01_DATASET_SPEC.md` để phục vụ đánh giá ở Tuần 7.

## 2. Cây Thư Mục Cập Nhật (Updated Project Tree)
Danh sách các file được sinh ra (mới) trong nhánh `feature/M5-01-reliability-contracts` để đáp ứng toàn bộ các dấu tick của Issue:

```text
ai-exam-bank/
├── docs/
│   ├── M5_01_DATASET_SPEC.md            # Khung JSON cho AI chấm điểm
│   ├── M5_01_ERROR_MATRIX.md            # Phân loại lỗi và Log Schema
│   ├── M5_01_METRICS_SLO.md             # Danh sách Metric & SLO cam kết
│   ├── M5_01_RELIABILITY_UI_WIREFRAME.md# Giao diện theo dõi & Retry Job
│   └── (các file docs cũ...)
└── src/
    ├── Contracts/
    │   └── BackgroundJobs/
    │       ├── IJobHandler.cs           # Giao diện cho Worker (chứa Retry)
    │       ├── IJobPublisher.cs         # Giao diện ném việc (chứa Idempotency)
    │       └── IJobTracker.cs           # Giao diện theo dõi tiến độ
    └── Entities/
        └── BackgroundJobs/
            ├── BackgroundJob.cs         # Bảng Database lưu Job gốc
            ├── JobAttempt.cs            # Bảng lưu nhật ký chạy (Lease/Heartbeat)
            ├── JobStatus.cs             # Enum trạng thái
            └── JobStatusDto.cs          # Model mang dữ liệu trả ra UI
```

Tất cả các file này đã được commit vào nhánh và sẵn sàng để Peer Review. Mọi tiêu chí thuộc phần `Implementation checklist` và `Documentation / evidence` trên Github Issue #5 đã được đáp ứng 100%.
