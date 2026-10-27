# AI Exam Bank - Architecture & Codebase Map

Tài liệu này mô tả chi tiết kiến trúc Monorepo phân tách vật lý giữa .NET Backend và Next.js Frontend, cùng cấu trúc Modular Monolith bên trong Backend.

---

## 🏛️ 1. Bản đồ cấu trúc Monorepo

Sau khi chuẩn hóa qua PR #70 (`chore/sync-backend-frontend-layout`), toàn bộ source code được quy hoạch như sau:

```text
ai-exam-bank/
├── Backend/                                  <-- Toàn bộ mã nguồn & kiểm thử .NET Backend
│   ├── AiExamBank.slnx                       <-- Solution file chính (.NET 10)
│   ├── global.json                           <-- Ghim SDK "10.0.400" cho CI
│   ├── Directory.Build.props / Packages.props<-- Central Package Management & compiler settings
│   ├── Api/                                  <-- ASP.NET Core Host, Controllers, Minimal APIs, Auth, UI static files
│   ├── Worker/                               <-- Standalone Background Job Engine Worker (Daemon/CLI)
│   ├── Persistence/                          <-- SQL Server EF Core DbContext, Entity Configurations, Migrations
│   ├── Modules/                              <-- Modular Monolith (Các domain nghiệp vụ độc lập):
│   │   ├── Jobs/                             <-- [CORE M5 - Gia Bảo] Durable Job Engine, FileJobStore, Leases, Retry, Watcher
│   │   ├── Identity/                         <-- Xác thực, Phân quyền RBAC, Multi-school Tenancy (M3)
│   │   ├── Exams/                            <-- Quản lý đề thi, Blueprint slot (M2)
│   │   ├── Questions/                        <-- Ngân hàng câu hỏi, QuestionRevisionRef (M1)
│   │   ├── Import/                           <-- Pipeline nhập dữ liệu câu hỏi tự động (M4)
│   │   ├── Knowledge/                        <-- Ma trận kiến thức, Taxonomy cây môn học
│   │   └── Review/                           <-- Quy trình bình duyệt đề và câu hỏi
│   ├── infrastructure/                       <-- Terraform IaC (bootstrap, staging, private-bucket), shell scripts, security
│   ├── tests/                                <-- Toàn bộ test suite Backend:
│   │   ├── Modules/                          <-- Unit Tests theo module (Jobs.Tests, Identity.Tests, Exams.Tests, Questions.Tests)
│   │   ├── Architecture.Tests/               <-- Kiểm tra ranh giới phụ thuộc giữa các module (MSTest)
│   │   ├── smoke/                            <-- Smoke tests API & UI (test_api_health.py, jobs_ui.test.cjs)
│   │   ├── acceptance/                       <-- BDD Features (identity_and_permissions.feature)
│   │   └── repository/                       <-- Kiểm tra policy templates & repo rules (test_repository_checks.py)
│   ├── tools/                                <-- Công cụ kiểm tra repo & hygiene:
│   │   └── check_repository.py               <-- Kiểm tra đường dẫn, markdown links, file tracking (0 lỗi)
│   └── docs/                                 <-- Tài liệu kỹ thuật, ADRs (0001-0005), M5_01, M5_02, M5_03
│
├── Frontend/                                 <-- Ứng dụng Web Client (Next.js 15+, React, Tailwind CSS)
│   ├── package.json                          <-- Quản lý dependencies độc lập của Frontend
│   ├── src/
│   │   ├── app/                              <-- Next.js App Router (Layouts, Pages)
│   │   ├── components/                       <-- UI Component library tái sử dụng
│   │   ├── contexts/                         <-- React state & Authentication Contexts
│   │   └── lib/                              <-- API client fetcher & utilities
│   └── .claude/skills/                       <-- Skills hỗ trợ giao diện (design, ui-ux, playwright)
│
├── .github/                                  <-- CI/CD Workflows, PR & Issue Templates
├── .coderabbit.yaml                          <-- Cấu hình CodeRabbit AI review (allow_author_approval: true)
└── .gitignore                                <-- Bỏ qua bin/, obj/, node_modules/, secrets, state
```

---

## ⚙️ 2. Cấu trúc Mô-đun Jobs (Core M5 - Reliability)

Mô-đun `Backend/Modules/Jobs/` được thiết kế theo nguyên lý Clean Architecture:

1. **Domain:**
   - `BackgroundJob.cs`: Thực thể cốt lõi biểu diễn công việc (Id, Type, Payload, Status, RetryCount, MaxRetries, Heartbeat, ExecutionDurationMs).
   - `JobAttempt.cs`: Bản ghi chi tiết từng lần chạy (AttemptNumber, StartedAt, CompletedAt, DurationMs, Status, ErrorCategory, ErrorMessage).
   - `JobStatus.cs`: Enum các trạng thái (`Pending`, `Running`, `Completed`, `Failed`, `Cancelled`).
   - `ErrorCategory.cs`: Enum phân loại lỗi (`Transient`, `Permanent`, `Terminal`).

2. **Contracts:**
   - `IJobStore.cs`: Hợp đồng lưu trữ công việc (Enqueue, AcquireLease, Complete, Fail, Cancel, Heartbeat, Retry, GetAttempts).
   - `IJobHandler<T>`: Hợp đồng xử lý nghiệp vụ cho từng loại job.

3. **Infrastructure:**
   - `FileJobStore.cs`: Lưu trữ bền vững trên filesystem với `Global\AiExamBank_FileJobStore_Mutex` đa tiến trình, ghi nguyên tử qua file `.tmp` và `File.Move`.
   - `InMemoryJobStore.cs`: Lưu trữ trong bộ nhớ phục vụ unit testing nhanh.
   - `BackoffJitterCalculator.cs`: Bounded Exponential Backoff với Jitter ngăn bão retry.
   - `ErrorClassifier.cs`: Logic phân loại ngoại lệ thành Transient / Permanent.

4. **Engine & Services:**
   - `JobWorkerEngine.cs`: Vòng lặp lấy job, cấp lease, thực thi, heartbeat định kỳ và watcher kiểm tra cờ hủy tức thì.
   - `JobEngineServiceCollectionExtensions.cs`: Đăng ký DI cho API và Worker daemon.
