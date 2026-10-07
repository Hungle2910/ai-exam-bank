# Thiết kế vai trò và phân quyền người dùng

**Trạng thái:** Proposed
**Người thực hiện:** Minh Phúc — Lancelot-sys25
**Issue:** M3-01 — Identity, permission matrix và approval threat model

## 1. Mục đích

Tài liệu xác định vai trò và quyền hạn của người dùng trong hệ thống
AI Exam Bank.

Hệ thống có hai role chính:

- Teacher
- Admin

Hệ thống không tạo role Reviewer riêng. Admin sẽ cấp thêm quyền review
cho một số Teacher phù hợp.

## 2. Teacher

Teacher là người sử dụng chức năng tạo đề.

Teacher có thể:

- Đăng nhập và đăng xuất.
- Tạo ma trận đề.
- Chọn môn học, chủ đề, độ khó, số câu và số điểm.
- Gửi yêu cầu để AI và hệ thống tạo đề.
- Xem trạng thái đề đang được tạo.
- Xem đề do mình tạo.
- Gửi đề bản nháp đi kiểm duyệt.
- Xem kết quả đề được duyệt hoặc bị từ chối.

Teacher bình thường không thể:

- Xem toàn bộ danh sách đề đang chờ duyệt.
- Duyệt hoặc từ chối đề.
- Quản lý tài khoản người dùng.
- Tự cấp quyền review.

## 3. Teacher có quyền review

Teacher có thể được Admin cấp thêm quyền `exams.review`.

Teacher có quyền review vẫn có toàn bộ chức năng của Teacher và có thêm
các quyền:

- Xem danh sách đề đang chờ duyệt.
- Xem nội dung và thông tin của đề bản nháp.
- Kiểm tra câu hỏi và đáp án do AI tạo.
- Đồng ý hoặc từ chối đề.
- Ghi lý do khi từ chối đề.
- Xem lịch sử kiểm duyệt trong phạm vi được giao.

Teacher có quyền review không được:

- Duyệt đề do chính mình tạo.
- Duyệt đề ngoài môn học hoặc phạm vi được phân công.
- Duyệt một phiên bản đề đã bị thay đổi.
- Cấp quyền review cho người dùng khác.

## 4. Admin

Admin chịu trách nhiệm quản lý tài khoản và phân quyền.

Admin có thể:

- Tạo, khóa hoặc mở khóa tài khoản.
- Cấp quyền `exams.review` cho Teacher.
- Thu hồi quyền `exams.review`.
- Phân công phạm vi hoặc môn học được review.
- Xem nhật ký kiểm toán của hệ thống.
- Kiểm tra lịch sử cấp và thu hồi quyền.

Admin không trực tiếp duyệt đề nếu chưa được thiết kế thêm quyền nghiệp vụ
phù hợp.

## 5. Bảng phân quyền

| Chức năng | Teacher thường | Teacher có quyền review | Admin |
|---|---:|---:|---:|
| Tạo ma trận đề | Có | Có | Không |
| Yêu cầu AI tạo đề | Có | Có | Không |
| Xem đề mình tạo | Có | Có | Không |
| Gửi đề đi duyệt | Có | Có | Không |
| Xem danh sách đề chờ duyệt | Không | Có | Có |
| Duyệt hoặc từ chối đề | Không | Có | Không |
| Tự duyệt đề của mình | Không | Không | Không |
| Quản lý tài khoản | Không | Không | Có |
| Cấp hoặc thu hồi quyền review | Không | Không | Có |
| Xem toàn bộ audit | Không | Không | Có |

## 6. Quyền hệ thống

Các quyền dự kiến:

- `exams.create`: tạo ma trận và yêu cầu tạo đề.
- `exams.read.own`: xem đề do mình tạo.
- `exams.submit`: gửi đề đi kiểm duyệt.
- `exams.review`: duyệt hoặc từ chối đề.
- `users.manage`: quản lý tài khoản.
- `permissions.assign`: cấp và thu hồi quyền.
- `audit.view`: xem nhật ký kiểm toán.

Teacher mặc định có:

- `exams.create`
- `exams.read.own`
- `exams.submit`

Teacher được Admin phân công review có thêm:

- `exams.review`

Admin có:

- `users.manage`
- `permissions.assign`
- `audit.view`

## 7. Quy trình tạo và duyệt đề

1. Teacher tạo ma trận đề.
2. AI và hệ thống tạo đề bản nháp.
3. Teacher kiểm tra sơ bộ và gửi đề đi duyệt.
4. Đề chuyển sang trạng thái `PENDING_REVIEW`.
5. Teacher có quyền review kiểm tra đề.
6. Reviewer đồng ý hoặc từ chối đề.
7. Nếu được đồng ý, đề chuyển sang `APPROVED`.
8. Nếu bị từ chối, đề chuyển sang `REJECTED` và lưu lý do.
9. Teacher chỉnh sửa hoặc yêu cầu AI tạo lại trước khi gửi duyệt lần nữa.

Luồng trạng thái:

DRAFT → PENDING_REVIEW → APPROVED

DRAFT → PENDING_REVIEW → REJECTED

REJECTED → DRAFT

## 8. Quy tắc bảo mật

- Backend phải kiểm tra quyền cho mỗi API.
- Việc ẩn nút trên giao diện không thay thế kiểm tra quyền tại backend.
- Teacher bình thường gọi API review phải bị từ chối.
- Người tạo không được duyệt đề của chính mình.
- Quyền review phải được giới hạn theo phạm vi được Admin phân công.
- Quyết định review phải gắn với đúng phiên bản đề.
- Đề đã thay đổi sau khi reviewer mở phải được tải lại trước khi duyệt.
- Mọi lần cấp quyền, thu hồi quyền, duyệt và từ chối phải được ghi audit.
- AI không được tự chuyển đề sang trạng thái `APPROVED`.

## 9. Nhật ký kiểm toán

Mỗi lần duyệt hoặc từ chối cần lưu:

- ID người thực hiện.
- ID đề.
- Phiên bản đề.
- Hành động APPROVE hoặc REJECT.
- Lý do từ chối.
- Thời gian thực hiện.
- Mã request hoặc correlation ID.

Mỗi lần Admin thay đổi quyền cần lưu:

- Admin thực hiện thay đổi.
- Teacher được thay đổi quyền.
- Quyền được cấp hoặc thu hồi.
- Phạm vi review.
- Thời gian thực hiện.

## 10. Các trường hợp kiểm thử

- Teacher bình thường gọi API duyệt đề phải nhận lỗi 403.
- Teacher có quyền review được duyệt đề đúng phạm vi.
- Reviewer duyệt ngoài phạm vi phải nhận lỗi 403.
- Reviewer tự duyệt đề của mình phải nhận lỗi 403.
- Reviewer duyệt phiên bản đề cũ phải nhận lỗi 409.
- Từ chối đề mà không ghi lý do phải nhận lỗi 400.
- Duyệt thành công phải tạo bản ghi quyết định và audit.
- Sau khi Admin thu hồi quyền, Teacher không thể tiếp tục review.
