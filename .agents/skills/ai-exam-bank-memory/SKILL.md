---
name: ai-exam-bank-memory
description: >-
  Lưu trữ, tra cứu và cập nhật toàn bộ bộ nhớ kỹ thuật, tiến độ các task (M5-01, M5-02, M5-03...), kiến trúc Monorepo Backend/Frontend, và cheat sheet lệnh của dự án AI Exam Bank. Dùng skill này khi người dùng yêu cầu xem lại tiến độ dự án, kiểm tra kiến trúc, tra cứu lệnh chạy, hoặc tiếp tục các task liên quan đến Role M5 (Reliability & ML Evaluation).
---

# AI Exam Bank - Project Memory & Context Guide

Skill này dùng để quản lý toàn bộ bộ nhớ kỹ thuật, kiến trúc hệ thống, quy chuẩn chất lượng và tiến độ thực hiện của dự án **AI Exam Bank** theo chuẩn **Antigravity Progressive Disclosure**.

---

## 🎯 Khi nào sử dụng
- **Khôi phục trạng thái (Restore Context):** Khi mở lại phiên làm việc mới, kích hoạt skill để nắm bắt ngay cấu trúc repo, tiến độ các task M5, và nhánh git hiện tại.
- **Tra cứu lệnh thực thi (CLI Cheat Sheet):** Khi cần build, test, chạy API server, Worker, kiểm tra smoke test hoặc lint repo.
- **Xem xét kiến trúc & module (Architecture):** Khi cần tra cứu ranh giới các module (Jobs, Identity, Exams, Questions, Persistence...).
- **Xử lý sự cố (Troubleshooting):** Khi gặp lỗi liên quan đến phiên bản .NET SDK (`global.json`), lock mutex đa tiến trình, hoặc quy trình review CodeRabbit.
- **Cập nhật tiến độ (Update Memory):** Sau khi hoàn thành một task, PR được merge, hoặc có quyết định kiến trúc mới (ADR).

---

## 📂 Danh mục tài liệu tham chiếu (Progressive References)

Để tiết kiệm context window và tra cứu chính xác, các tài liệu chi tiết được tách biệt theo từng chuyên đề:

| Tài liệu | Mô tả nội dung | Liên kết |
|---|---|---|
| **Lịch sử & Tiến độ (Memory)** | Chi tiết các task M5-01, M5-02 (PR #68), M5-03 (PR #74 merged), các quyết định kỹ thuật | [memory.md](file:///d:/projectCode/AWS_project/ai-exam-bank/.agents/skills/ai-exam-bank-memory/references/memory.md) |
| **Bản đồ Kiến trúc (Architecture)** | Cấu trúc Monorepo Backend/Frontend, Modular Monolith, ranh giới domain và data flow | [architecture.md](file:///d:/projectCode/AWS_project/ai-exam-bank/.agents/skills/ai-exam-bank-memory/references/architecture.md) |
| **Bảng tra cứu lệnh (Cheat Sheet)** | Toàn bộ lệnh CLI thường dùng: build, test, run host, run worker, smoke test, repo check | [cheat_sheet.md](file:///d:/projectCode/AWS_project/ai-exam-bank/.agents/skills/ai-exam-bank-memory/references/cheat_sheet.md) |
| **Kinh nghiệm xử lý sự cố (Troubleshooting)** | Bài học kinh nghiệm: lệch version SDK, Named Mutex, quy tắc link markdown, CodeRabbit | [troubleshooting.md](file:///d:/projectCode/AWS_project/ai-exam-bank/.agents/skills/ai-exam-bank-memory/references/troubleshooting.md) |

---

## 📌 Tóm tắt nhanh thông tin dự án
- **Đường dẫn local:** `d:\projectCode\AWS_project\ai-exam-bank`
- **Owner hiện tại:** Gia Bảo (M5 / @TranGiaBao2005) — Reliability & ML Evaluation Engineer
- **Nhánh Git hiện tại:** `dev` (Sau khi hoàn tất và merge PR #74 cho M5-03)
- **Công nghệ cốt lõi:**
  - **Backend:** .NET 10 (C# 14), ASP.NET Core, EF Core SQL Server, Modular Monolith, Standalone Job Worker Daemon.
  - **Frontend:** Next.js 15+, React 19, Tailwind CSS.
  - **Durable Engine:** In-memory & File-based Durable Job Store (Windows Named Mutex, atomic writes, exponential backoff + jitter).
