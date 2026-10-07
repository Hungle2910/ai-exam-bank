# Thiết kế vai trò và phân quyền người dùng

**Trạng thái:** Proposed
**Người thực hiện:** Minh Phúc — Lancelot-sys25
**Issue:** M3-01 — Identity, permission matrix và approval threat model

## 1. Mục đích

Tài liệu xác định vai trò, quyền hạn và quy trình tạo, kiểm duyệt đề trong
hệ thống AI Exam Bank.

Hệ thống có ba role chính:

- `Teacher` — Giáo viên.
- `DepartmentHead` — Trưởng bộ môn.
- `Admin` — Quản trị viên.

Admin phân công role và phạm vi môn học. Giáo viên yêu cầu AI tạo đề;
Trưởng bộ môn kiểm tra và quyết định đề có hợp lý để sử dụng hay không.

## 2. Teacher — Giáo viên

Teacher là người tạo yêu cầu đề thi.

Teacher có thể:

- Đăng nhập và đăng xuất.
- Tạo ma trận đề.
- Chọn môn học, chủ đề, độ khó, số câu và số điểm.
- Gửi yêu cầu để AI và hệ thống tạo đề bản nháp.
- Xem trạng thái tạo đề.
- Xem đề do mình yêu cầu tạo.
- Kiểm tra sơ bộ và gửi đề bản nháp đi kiểm duyệt.
- Xem kết quả được duyệt hoặc bị từ chối.
- Chỉnh sửa ma trận hoặc yêu cầu AI tạo lại đề bị từ chối.

Teacher không thể:

- Xem toàn bộ danh sách đề đang chờ duyệt.
- Duyệt hoặc từ chối đề.
- Quản lý tài khoản hoặc phân role.
- Tự chuyển đề sang trạng thái `APPROVED`.

## 3. DepartmentHead — Trưởng bộ môn

DepartmentHead là người chịu trách nhiệm kiểm duyệt đề trong môn hoặc
bộ môn được Admin phân công.

DepartmentHead có thể:

- Xem danh sách đề đang chờ duyệt trong phạm vi phụ trách.
- Xem ma trận, nội dung đề, đáp án và thông tin AI của đề bản nháp.
- Kiểm tra số câu, số điểm, chủ đề và tỉ lệ độ khó có đúng ma trận không.
- Kiểm tra câu hỏi, đáp án, câu trùng lặp và nguồn tham khảo nếu có.
- Đồng ý hoặc từ chối đề.
- Ghi lý do khi từ chối để Teacher chỉnh sửa hoặc yêu cầu AI tạo lại.
- Xem lịch sử kiểm duyệt trong phạm vi phụ trách.

DepartmentHead không thể:

- Duyệt đề ngoài bộ môn hoặc phạm vi được phân công.
- Duyệt một phiên bản khác với phiên bản đã kiểm tra.
- Quản lý tài khoản hoặc tự thay đổi phạm vi phụ trách.
- Tự duyệt đề do chính mình tạo nếu một tài khoản đồng thời có role Teacher.

## 4. Admin — Quản trị viên

Admin chịu trách nhiệm quản lý tài khoản, role và phạm vi bộ môn.

Admin có thể:

- Tạo, khóa hoặc mở khóa tài khoản.
- Gán hoặc thu hồi role `Teacher` và `DepartmentHead`.
- Phân công bộ môn hoặc phạm vi kiểm duyệt cho DepartmentHead.
- Xem nhật ký kiểm toán của hệ thống.
- Kiểm tra lịch sử thay đổi role và phạm vi.

Admin không mặc định có quyền tạo hoặc duyệt đề. Nếu một người cần thực
hiện nghiệp vụ đó, tài khoản phải được cấp role tương ứng và vẫn phải tuân
theo quy tắc không tự duyệt đề của mình.

## 5. Bảng phân quyền

| Chức năng | Teacher | DepartmentHead | Admin |
|---|---:|---:|---:|
| Tạo ma trận đề | Có | Không | Không |
| Yêu cầu AI tạo đề | Có | Không | Không |
| Xem đề mình yêu cầu tạo | Có | Không | Không |
| Gửi đề đi duyệt | Có | Không | Không |
| Xem danh sách đề chờ duyệt | Không | Có, theo bộ môn | Có, để quản trị |
| Xem nội dung và đáp án để kiểm duyệt | Không | Có, theo bộ môn | Không |
| Duyệt hoặc từ chối đề | Không | Có, theo bộ môn | Không |
| Tự duyệt đề của mình | Không | Không | Không |
| Quản lý tài khoản và role | Không | Không | Có |
| Phân công phạm vi bộ môn | Không | Không | Có |
| Xem toàn bộ audit | Không | Không | Có |

## 6. Quyền hệ thống

Các quyền dự kiến:

- `exams.create`: tạo ma trận và yêu cầu AI tạo đề.
- `exams.read.own`: xem đề do mình yêu cầu tạo.
- `exams.submit`: gửi đề bản nháp đi kiểm duyệt.
- `exams.review.list`: xem danh sách đề chờ duyệt trong phạm vi.
- `exams.review.read`: xem nội dung và đáp án phục vụ kiểm duyệt.
- `exams.review.decide`: đồng ý hoặc từ chối đề.
- `users.manage`: quản lý tài khoản.
- `roles.assign`: gán hoặc thu hồi role.
- `review-scopes.assign`: phân công phạm vi bộ môn.
- `audit.view`: xem nhật ký kiểm toán.

Role `Teacher` có:

- `exams.create`
- `exams.read.own`
- `exams.submit`

Role `DepartmentHead` có:

- `exams.review.list`
- `exams.review.read`
- `exams.review.decide`

Role `Admin` có:

- `users.manage`
- `roles.assign`
- `review-scopes.assign`
- `audit.view`

## 7. Quy trình tạo và kiểm duyệt đề

1. Teacher tạo ma trận đề gồm môn học, chủ đề, độ khó, số câu và số điểm.
2. Teacher gửi yêu cầu để AI và hệ thống tạo đề bản nháp.
3. AI chỉ tạo nội dung ở trạng thái `DRAFT`.
4. Teacher kiểm tra sơ bộ và gửi đề đi duyệt.
5. Đề chuyển sang trạng thái `PENDING_REVIEW`.
6. DepartmentHead thuộc đúng bộ môn kiểm tra đề và ma trận.
7. Nếu hợp lý, DepartmentHead đồng ý và đề chuyển sang `APPROVED`.
8. Nếu chưa hợp lý, DepartmentHead từ chối, ghi lý do và đề chuyển sang
   `REJECTED`.
9. Teacher chỉnh sửa ma trận hoặc yêu cầu AI tạo lại; phiên bản mới trở về
   `DRAFT` và phải được gửi duyệt lại.

Luồng trạng thái:

```text
DRAFT → PENDING_REVIEW → APPROVED
                       → REJECTED → DRAFT (phiên bản mới)
```

Chỉ đề ở trạng thái `APPROVED` mới được phép sử dụng hoặc phát hành.

## 8. Tiêu chí Trưởng bộ môn kiểm duyệt

DepartmentHead cần kiểm tra tối thiểu:

- Đúng môn học và chủ đề.
- Đủ số câu và tổng điểm theo ma trận.
- Tỉ lệ độ khó phù hợp với yêu cầu.
- Nội dung câu hỏi rõ ràng và chính xác.
- Đáp án và đáp án đúng hợp lý.
- Không có câu trùng lặp trong cùng đề.
- Câu do AI tạo không chứa thông tin sai hoặc nội dung ngoài phạm vi.
- Nguồn tham khảo hoặc trích dẫn phù hợp nếu đề có sử dụng.

Nếu từ chối, DepartmentHead phải ghi lý do cụ thể để Teacher biết phần cần
sửa hoặc tạo lại.

## 9. Quy tắc bảo mật

- Backend phải kiểm tra role và phạm vi cho mỗi API.
- Việc ẩn nút trên giao diện không thay thế kiểm tra quyền tại backend.
- Teacher gọi trực tiếp API duyệt đề phải bị từ chối.
- DepartmentHead chỉ được duyệt đề thuộc bộ môn được phân công.
- Người tạo không được tự duyệt đề của mình dù có nhiều role.
- Quyết định kiểm duyệt phải gắn với đúng phiên bản đề.
- Đề đã thay đổi sau khi DepartmentHead mở phải được tải lại trước khi duyệt.
- AI không được tự chuyển đề sang trạng thái `APPROVED`.
- Mọi lần đổi role, đổi phạm vi, duyệt và từ chối phải được ghi audit.

## 10. Nhật ký kiểm toán

Mỗi lần duyệt hoặc từ chối cần lưu:

- ID người kiểm duyệt.
- Role và phạm vi kiểm duyệt tại thời điểm quyết định.
- ID đề và ID người yêu cầu tạo đề.
- Phiên bản đề.
- Hành động `APPROVE` hoặc `REJECT`.
- Lý do từ chối.
- Thời gian thực hiện.
- Mã request hoặc correlation ID.

Mỗi lần Admin thay đổi role hoặc phạm vi cần lưu:

- ID Admin thực hiện thay đổi.
- ID tài khoản bị thay đổi.
- Role được gán hoặc thu hồi.
- Phạm vi bộ môn trước và sau thay đổi.
- Thời gian thực hiện.

## 11. Các trường hợp kiểm thử

- Teacher gọi API duyệt đề phải nhận lỗi 403.
- DepartmentHead duyệt đề đúng bộ môn và đúng phiên bản phải thành công.
- DepartmentHead duyệt đề ngoài bộ môn phải nhận lỗi 403.
- Tài khoản có nhiều role tự duyệt đề của mình phải nhận lỗi 403.
- DepartmentHead duyệt phiên bản đề cũ phải nhận lỗi 409.
- Từ chối đề mà không ghi lý do phải nhận lỗi 400.
- Duyệt thành công phải tạo ReviewDecision và AuditEvent.
- Sau khi Admin thu hồi role DepartmentHead, tài khoản không thể tiếp tục duyệt.
- Sau khi Admin đổi phạm vi bộ môn, quyền kiểm duyệt phải áp dụng theo phạm
  vi mới ngay tại backend.

## 12. Các điểm cần nhóm xác nhận

- Một tài khoản có được giữ đồng thời role `Teacher` và `DepartmentHead` không.
- DepartmentHead kiểm duyệt toàn bộ đề hay hệ thống còn yêu cầu duyệt riêng
  từng câu hỏi AI trước khi ghép đề.
- Cách biểu diễn phạm vi: bộ môn, môn học, khối lớp hoặc kết hợp các trường này.
- Phương thức đăng nhập sẽ dùng cookie/session hay token.
