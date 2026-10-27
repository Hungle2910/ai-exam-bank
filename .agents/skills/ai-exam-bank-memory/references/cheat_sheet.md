# AI Exam Bank - CLI Command Cheat Sheet

Bảng tra cứu nhanh toàn bộ các câu lệnh thực thi phổ biến trong dự án AI Exam Bank từ thư mục gốc `d:\projectCode\AWS_project\ai-exam-bank`.

---

## ⚡ 1. Build & Test Commands

| Mục đích | Lệnh thực thi (Chạy từ root `ai-exam-bank`) | Ghi chú |
|---|---|---|
| **Build toàn bộ Backend** | `dotnet build Backend/AiExamBank.slnx -c Release` | Đảm bảo 0 Warning, 0 Error |
| **Chạy toàn bộ Test Suite** | `dotnet test Backend/AiExamBank.slnx` | Chạy 84+ unit & architecture tests |
| **Chạy riêng Job Engine Tests** | `dotnet test Backend/tests/Modules/Jobs.Tests/AiExamBank.Modules.Jobs.Tests.csproj` | Kiểm tra toàn bộ 84 tests của M5 |
| **Chạy Architecture Boundary Tests** | `dotnet test Backend/tests/Architecture.Tests/AiExamBank.Architecture.Tests.csproj` | Kiểm tra ranh giới phụ thuộc Clean Architecture |
| **Chạy Tests với Filter** | `dotnet test Backend/tests/Modules/Jobs.Tests/ --filter "FullyQualifiedName~RetryAndIdempotencyTests"` | Chỉ chạy bộ test Retry/Idempotency |

---

## 🚀 2. Run Applications

| Mục đích | Lệnh thực thi | Chi tiết |
|---|---|---|
| **Khởi chạy API Server** | `dotnet run --project Backend/Api` | Web API tại `http://localhost:5000`<br>Job Status UI tại `http://localhost:5000/jobs/` |
| **Khởi chạy Worker Daemon** | `dotnet run --project Backend/Worker` | Tiến trình xử lý job nền độc lập ngoài tiến trình |
| **Dừng tiến trình dotnet chạy ngầm** | `Stop-Process -Name dotnet` (PowerShell) | Dọn dẹp cổng và file lock mutex |
| **Khởi chạy Frontend Web** | `cd Frontend && npm run dev` | Giao diện Next.js client tại `http://localhost:3000` |

---

## 🧪 3. Quality & Hygiene Checks

| Mục đích | Lệnh thực thi | Chi tiết |
|---|---|---|
| **Smoke Test API** | `python Backend/tests/smoke/test_api_health.py` | Kiểm tra 19 assertions về Auth & Job lifecycle |
| **Kiểm tra UI Jobs** | `node Backend/tests/smoke/jobs_ui.test.cjs` | Kiểm tra các phần tử giao diện Job Status UI |
| **Kiểm tra Vệ sinh Repo** | `python Backend/tools/check_repository.py` | Quét markdown link, path hoa/thường, file tracking |
| **Kiểm tra Git Status** | `git status -s` | Kiểm tra trạng thái cây làm việc |
