# Cẩm Nang Ánh Xạ Mã Lỗi Sang Hành Động (M5-06 Error-to-Action Guide)

Tài liệu này quy định bảng tra cứu chuẩn giữa **Mã lỗi (`ErrorCode`)**, **Phân loại (`ErrorCategory`)**, **Khả năng thử lại (`IsRetryEligible`)**, **Hướng dẫn hiển thị cho người dùng (`ActionableReason`)**, và **Quy trình xử lý dành cho Vận hành viên (Operator Runbook)**.

---

## 1. Bảng Ánh Xạ Lỗi Tạm Thời (Transient Errors — Tự Động Retry)

Các lỗi thuộc nhóm này được hệ thống tự động thử lại theo thuật toán **Exponential Backoff + Full Jitter** (tối đa 3 lượt chạy). Người dùng không cần can thiệp trừ khi tác vụ vượt quá 3 lượt thử (`RETRY_BUDGET_EXHAUSTED`).

| Mã Lỗi (`ErrorCode`) | Phân Loại | Tự Động Retry | Thông Báo & Hướng Dẫn Người Dùng (`ActionableReason`) | Hành Động Của Operator |
| :--- | :--- | :--- | :--- | :--- |
| `AWS_BEDROCK_THROTTLED` | `Transient` | Có (1p $\rightarrow$ 5p) | Dịch vụ AI đang quá tải lượt yêu cầu. Hệ thống đang tự động giãn cách và thử lại. | Theo dõi metric `ai_provider_errors_total`; nếu kéo dài > 5 phút, kiểm tra hạn ngạch RPM/TPM trên AWS Service Quotas. |
| `AWS_BEDROCK_MODEL_TIMEOUT` | `Transient` | Có (1p $\rightarrow$ 5p) | Phản hồi từ mô hình AI vượt quá thời gian chờ. Hệ thống sẽ tự động thử lại. | Kiểm tra độ phức tạp của Blueprint hoặc kích thước ngữ cảnh RAG gửi sang Bedrock. |
| `AWS_S3_SLOW_DOWN` | `Transient` | Có (1p $\rightarrow$ 5p) | Kho lưu trữ S3 đang giới hạn tốc độ truy cập tạm thời. Hệ thống sẽ tự động thử lại. | Không cần can thiệp nếu tỷ lệ thành công sau retry đạt > 99%. |
| `AWS_S3_TIMEOUT` | `Transient` | Có (1p $\rightarrow$ 5p) | Kết nối mạng tới kho lưu trữ S3 bị gián đoạn tạm thời. | Kiểm tra VPC Endpoint / NAT Gateway nếu lỗi lặp lại liên tục. |
| `STALE_LEASE_RECOVERED` | `Transient` | Có (Tự động cứu hộ) | Tiến trình xử lý bị mất kết nối đột ngột. Tác vụ đã được tự động cấp cứu và đưa lại vào hàng đợi. | Kiểm tra CloudWatch Alarm `stale-lease-recoveries` và sức khỏe bộ nhớ/CPU của tiến trình Worker. |
| `CIRCUIT_BREAKER_OPEN` | `Transient` | Hoãn (Bảo toàn lượt thử) | Dịch vụ AI đang tạm ngưng để bảo vệ hệ thống sau chuỗi lỗi liên tiếp. Tác vụ sẽ tự động chạy khi mạch đóng lại. | Kiểm tra cảnh báo SNS `CIRCUIT_BREAKER_OPEN` và trạng thái hoạt động của AWS Bedrock tại vùng `ap-southeast-1`. |

---

## 2. Bảng Ánh Xạ Lỗi Vĩnh Viễn (Permanent Errors — Khóa Retry)

Các lỗi thuộc nhóm này phản ánh dữ liệu đầu vào không hợp lệ hoặc cấu hình phân quyền bị từ chối. Hệ thống **chuyển thẳng tác vụ sang `Failed`** và **khóa nút Thử lại (`IsRetryEligible = false`)** để tránh lãng phí tài nguyên.

| Mã Lỗi (`ErrorCode`) | Phân Loại | Cho Phép Retry | Hướng Dẫn Khắc Phục Cho Người Dùng (`ActionableReason`) | Hành Động Của Operator / Người Dùng |
| :--- | :--- | :--- | :--- | :--- |
| `ROW_VALIDATION_ERROR` | `Permanent` | Không (`409`) | Tệp CSV chứa dòng dữ liệu sai định dạng hoặc thiếu đáp án đúng. Vui lòng sửa tệp CSV và tải lên đợt nhập mới. | Người dùng kiểm tra lại định dạng cột trong tệp CSV mẫu trước khi tạo job mới. |
| `BLUEPRINT_INSUFFICIENT_QUESTIONS` | `Permanent` | Không (`409`) | Ngân hàng câu hỏi đã duyệt (Approved) không đủ đáp ứng ma trận đề thi. | Duyệt thêm câu hỏi ở phân hệ Review (M3) hoặc giảm số câu yêu cầu trong Blueprint (M1). |
| `AWS_S3_FILE_NOT_FOUND` | `Permanent` | Không (`409`) | Không tìm thấy tệp dữ liệu nguồn trên kho lưu trữ S3. | Kiểm tra lại đường dẫn `PayloadReference` đã được tải lên S3 thành công chưa. |
| `AWS_S3_ACCESS_DENIED` | `Permanent` | Không (`409`) | Hệ thống không có quyền đọc tệp nguồn trên Amazon S3. | Operator kiểm tra IAM Role Policy của Worker đối với bucket S3 mục tiêu. |
| `AWS_BEDROCK_ACCESS_DENIED` | `Permanent` | Không (`409`) | Chưa cấp quyền truy cập mô hình Amazon Bedrock. | Operator kiểm tra IAM Policy `bedrock:InvokeModel` và Model Access trên AWS Console. |
| `AWS_BEDROCK_QUOTA_EXCEEDED` | `Permanent` | Không (`409`) | Đã vượt hạn ngạch sử dụng dịch vụ AI trong kỳ. | Liên hệ quản trị viên hạ tầng (M2/M5) để nâng hạn mức chi tiêu hoặc quota tháng. |
| `AI_MALFORMED_OUTPUT` | `Permanent` | Không (`409`) | Dữ liệu sinh ra từ AI không tuân thủ cấu trúc JSON Schema quy định. | Kỹ sư M4 kiểm tra lại phiên bản Prompt và bộ kiểm tra đầu ra của mô hình. |
| `RAG_RETRIEVAL_EMPTY` | `Permanent` | Không (`409`) | Không tìm thấy tài liệu tham chiếu phù hợp trong kho tri thức (Knowledge Base) cho chủ đề này. | Kỹ sư M4 bổ sung tài liệu ngữ liệu vào Knowledge Base cho chủ đề tương ứng. |

---

## 3. Lỗi Chạm Ngưỡng Giới Hạn & Hủy Thủ Công (Terminal / User Actions)

| Mã Lỗi / Trạng Thái | Phân Loại | Cho Phép Retry Thủ Công | Hướng Dẫn Trên Giao Diện (`/jobs/`) |
| :--- | :--- | :--- | :--- |
| `RETRY_BUDGET_EXHAUSTED` | `Terminal` | **Có** (Sau khi xử lý nguyên nhân gốc) | Đã thử tự động tối đa 3 lần nhưng vẫn thất bại. Sau khi hệ thống hạ tầng ổn định, người dùng có thể bấm nút `🔄 Thử lại` trên giao diện để cấp thêm 1 lượt chạy thủ công. |
| `CANCELLED` | `UserAction` | **Có** (Chỉ chủ sở hữu trong cùng Scope) | Tác vụ đã bị hủy theo yêu cầu của người dùng. Chủ sở hữu có thể bấm `🔄 Thử lại` để đưa tác vụ vào hàng đợi lại từ đầu. |
