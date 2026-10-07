# Tổng quan dự án AI Exam Bank - Lịch sử Onboarding

Tài liệu này lưu lại quá trình tìm hiểu dự án **AI Exam Bank**, giúp các thành viên mới nắm bắt ngữ cảnh, cấu trúc và các quy định của dự án mà không cần quét lại toàn bộ mã nguồn.

## 1. Cấu trúc thư mục dự án

```text
ai-exam-bank/
├── .github/                         # Cấu hình GitHub (Issue, PR template, CI/CD Actions, Dependabot)
├── docs/                            # Tài liệu dự án (Kiến trúc, tiến độ, phân công)
│   ├── adr/                         # Architecture Decision Records
│   ├── ARCHITECTURE.md              # Kiến trúc tổng quan
│   ├── AWS_STRATEGY.md              # Chiến lược AWS
│   ├── BACKLOG.md, backlog.csv      # Danh sách task
│   ├── IMPLEMENTATION_PLAN.md       # Kế hoạch triển khai (10 tuần)
│   ├── PROJECT_TRACKING.md          # Quy trình theo dõi dự án
│   ├── REPOSITORY_REVIEW.md         # Đánh giá tiêu chuẩn repo
│   └── TEAM.md                      # Phân chia nhiệm vụ
├── infrastructure/                  # Mã nguồn IaC triển khai AWS (chờ code)
├── src/                             # Mã nguồn ứng dụng (Backend, Frontend) (chờ code)
├── tests/                           # Bài kiểm thử tự động
├── tools/                           # Script tiện ích (như kiểm tra repo)
├── CONTRIBUTING.md                  # Hướng dẫn đóng góp (Branch, PR, Code review)
├── README.md                        # Giới thiệu tổng quan và MVP
└── SECURITY.md                      # Chính sách bảo mật
```

## 2. Trình tự đọc tài liệu dành cho người mới
1. **`README.md`**: Hiểu dự án làm gì, phạm vi MVP.
2. **`docs/ARCHITECTURE.md`**: Hiểu kiến trúc, ranh giới các module.
3. **`docs/TEAM.md`**: Hiểu vai trò của từng thành viên.
4. **`CONTRIBUTING.md`**: Hiểu quy tắc đặt tên branch, commit, tạo PR.
5. **`docs/AWS_STRATEGY.md`**: Hiểu chiến lược triển khai lên cloud.

## 3. Tóm tắt `README.md` (Tổng quan & MVP)
*   **Sản phẩm:** Nền tảng quản lý ngân hàng câu hỏi và tạo đề thi dựa trên AI.
*   **MVP:** 
    *   Hỗ trợ tạo đề thi (AI sinh bản nháp cho câu thiếu).
    *   Trạng thái câu hỏi bắt buộc phải do **người duyệt**.
    *   Hạ tầng backend .NET, triển khai private trên AWS.
*   **Ngoài MVP:** Chưa làm phân hệ cho học sinh thi, chưa mở tính năng Lex (Chatbot) hay SageMaker (Đánh giá).

## 4. Tóm tắt `ARCHITECTURE.md` (Kiến trúc kỹ thuật)
*   **Mô hình:** Modular Monolith (1 source code chia nhiều module: Auth, Questions, Exams...).
*   **Vòng đời câu hỏi:** DRAFT ➔ REVIEW_REQUIRED ➔ APPROVED.
*   **Bất biến (Immutable):** Đề thi đã chốt (Snapshot) hoặc Câu hỏi đã Approved thì không được phép sửa đè. Việc "sửa" sẽ sinh ra một phiên bản DRAFT mới.
*   **Luỹ đẳng (Idempotency):** Worker chạy ngầm phải xử lý chống trùng lặp dữ liệu.

## 5. Tóm tắt `CONTRIBUTING.md` (Kỷ luật Code)
*   **Tên nhánh:** Theo chuẩn `loại/ID-tên` (ví dụ `feat/M5-03-idempotency`).
*   **Pull Request:** Phải có ảnh/log chứng minh chạy thật trên AWS. Không tự merge code của mình, phải có người review.
*   **Check code:** Luôn chạy `python tools/check_repository.py` ở máy cá nhân trước khi đẩy code lên GitHub.

## 6. Tóm tắt `AWS_STRATEGY.md` (Quy định Cloud)
*   **Private Network:** Không public DB và Backend ra mạng ngoài Internet.
*   **IAM:** Phân quyền tối thiểu (Least Privilege). Cấm dùng quyền AdminAccess để "chữa cháy".
*   **Tự động hóa:** Triển khai hạ tầng bằng Code (IaC) và GitHub Actions (OIDC).
*   **Chi phí:** Chú ý các dịch vụ trừ tiền theo giờ (NAT, SageMaker). Phải có cơ chế dọn rác tự động.

## 7. Chi tiết vai trò Gia Bảo (M5) - Reliability & ML
*   **Giai đoạn 1-6 (Core MVP):** Đảm bảo hệ thống không sập.
    *   Quản lý Background Jobs (các tác vụ chạy ngầm như import file, gọi Bedrock AI).
    *   Xử lý Idempotency (chống trùng lặp dữ liệu khi rớt mạng), Bounded Retry (thử lại khi API Amazon sập nhưng có giới hạn lần thử), và Lease (khóa tiến trình, tránh 2 worker làm chung 1 việc).
    *   Xây dựng UI để quản trị viên theo dõi trạng thái Job.
    *   Cấu hình hệ thống báo động CloudWatch và gửi tin nhắn cảnh báo qua màn hình/email (SNS) khi hệ thống có vấn đề (Vd: Job lỗi vượt ngưỡng 5%).
*   **Giai đoạn mở rộng (Tùy chọn):**
    *   Tích hợp AWS SageMaker AI để huấn luyện mô hình tự động đánh giá độ khó của câu hỏi, thay cho sức người.
