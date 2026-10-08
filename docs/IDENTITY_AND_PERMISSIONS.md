# Đề xuất vai trò và phân quyền

**Trạng thái:** Proposed — thay đổi phạm vi cần nhóm duyệt trước khi triển khai
**Người thực hiện:** Minh Phúc — Lancelot-sys25
**Liên quan:** M3-01 (thiết kế đã merge); M3-02 (triển khai đăng nhập và phân quyền)

## 1. Phạm vi đề xuất

Hệ thống có bốn role: `MinistryAdmin` (quản trị cấp Bộ), `SchoolAdmin`
(quản trị cấp trường), `DepartmentHead` (trưởng bộ môn) và `Teacher`
(giáo viên). `MinistryAdmin` quản lý toàn hệ thống; ba role còn lại chỉ
thao tác trong trường và bộ môn được phân công. Một tài khoản có thể giữ
nhiều role nếu được gán rõ ràng, nhưng mỗi thao tác vẫn phải kiểm tra
đúng phạm vi tài nguyên.

Đây là đề xuất **mở rộng** so với MVP một trường trong `README.md` và
`docs/IMPLEMENTATION_PLAN.md`. Tài liệu này chưa tự thay đổi phạm vi MVP,
kiến trúc dữ liệu hoặc backlog. Nhóm cần chốt có triển khai nhiều trường
ngay hay đưa luồng cấp Bộ vào giai đoạn sau.

## 2. Phạm vi dữ liệu và gán role

- `School` xác định trường sở hữu người dùng và đề cấp trường.
- `Department` thuộc đúng một `School` và xác định bộ môn phụ trách.
- `Teacher` thuộc một trường; quyền tạo đề chỉ áp dụng trong môn/phạm vi
  được trường phân công.
- `DepartmentHead` được gán một hoặc nhiều cặp trường–bộ môn; chỉ xem và
  quyết định đề cấp trường thuộc các cặp đó.
- `SchoolAdmin` chỉ quản lý tài khoản, gán role và xem audit trong trường
  của mình. Họ không tự mở rộng phạm vi sang trường khác.
- `MinistryAdmin` quản lý danh sách trường, tài khoản quản trị cấp trường,
  các kỳ thi cấp Bộ và audit cấp hệ thống. Việc truy cập nội dung đề cấp
  Bộ chỉ dành cho tài khoản được giao vào kỳ thi đó.

Chỉ `MinistryAdmin` được tạo hoặc thu hồi tài khoản `SchoolAdmin`.
`SchoolAdmin` gán hoặc thu hồi `Teacher` và `DepartmentHead` trong trường
của mình. Không role nào được tự nâng quyền hoặc tự đổi phạm vi của mình.

## 3. Bảng quyền

| Hành động | Teacher | DepartmentHead | SchoolAdmin | MinistryAdmin |
|---|---|---|---|---|
| Yêu cầu AI tạo đề cấp trường | Có, trong phạm vi được giao | Chỉ khi có thêm role Teacher | Không mặc định | Không mặc định |
| Xem đề mình yêu cầu tạo | Có | Khi có role Teacher | Không mặc định | Không mặc định |
| Gửi đề cấp trường đi duyệt | Có, với đề của mình | Khi có role Teacher | Không | Không |
| Xem nội dung đề chờ duyệt cấp trường | Không | Có, theo trường–bộ môn | Không mặc định | Không mặc định |
| Duyệt hoặc từ chối đề cấp trường | Không | Có, theo trường–bộ môn | Không | Không |
| Quản lý Teacher/DepartmentHead | Không | Không | Trong trường mình | Toàn hệ thống khi cần quản trị |
| Quản lý SchoolAdmin và trường | Không | Không | Không | Có |
| Xem audit | Việc liên quan đến mình | Trong phạm vi phụ trách | Trong trường mình | Toàn hệ thống theo quyền quản trị |
| Tự soạn đề kỳ thi cấp Bộ | Không | Không | Không | Có, khi được giao kỳ thi |
| Xác nhận hoặc từ chối đề cấp Bộ | Không | Không | Không | Có, khi được giao kỳ thi |

Quyền tạo đề và quyền xác nhận đề cấp Bộ có thể cùng thuộc role
`MinistryAdmin`, nhưng **người xác nhận phải là tài khoản khác người soạn
cùng phiên bản đề**. Đề thi chuyển cấp hoặc tốt nghiệp không được một
người tự soạn rồi tự xác nhận. Nếu nhóm muốn cho phép ngoại lệ này, cần
một quyết định riêng nêu rõ lý do và cách kiểm soát.

## 4. Luồng đề cấp trường

1. Teacher chọn môn, chủ đề, độ khó, số câu và số điểm để tạo ma trận.
2. Teacher yêu cầu AI và hệ thống tạo bản nháp đề cấp trường.
3. AI chỉ tạo nội dung nháp; Teacher kiểm tra sơ bộ rồi gửi đề đi duyệt.
4. DepartmentHead đúng trường–bộ môn kiểm tra ma trận, câu hỏi, đáp án,
   độ khó, trùng lặp và nguồn tham khảo.
5. DepartmentHead đồng ý hoặc từ chối. Từ chối phải có lý do để Teacher
   chỉnh sửa hoặc yêu cầu AI tạo lại.
6. Chỉ phiên bản đã được duyệt mới đủ điều kiện để chốt và sử dụng.

## 5. Luồng kỳ thi cấp Bộ

1. `MinistryAdmin` được giao kỳ thi (ví dụ thi chuyển cấp hoặc tốt nghiệp)
   tạo bản nháp đề và ma trận bằng cách soạn trực tiếp. Việc dùng AI hỗ
   trợ, nếu có, vẫn chỉ tạo nội dung nháp.
2. Một `MinistryAdmin` **khác**, cũng được giao kỳ thi, kiểm tra và xác
   nhận hoặc từ chối đúng phiên bản đề. Người này xem đủ nội dung, đáp
   án, tổng điểm và nguồn để đánh giá.
3. Quyết định được lưu cùng audit. Khi bị từ chối, người soạn tạo phiên
   bản mới và gửi lại. Khi được xác nhận, đề được chốt thành snapshot
   không bị thay đổi bởi việc sửa bản nháp sau đó.
4. Đề cấp Bộ chỉ được xem hoặc xuất bởi tài khoản có quyền cho đúng kỳ
   thi. Quản trị trường không mặc định được xem trước nội dung đề cấp Bộ.

Luồng cấp trường và cấp Bộ đều sử dụng các trạng thái dự kiến:

```text
DRAFT → PENDING_REVIEW → APPROVED
                       → REJECTED → DRAFT (phiên bản mới)
```

`APPROVED` là kết quả duyệt của con người; việc chốt snapshot phát hành
vẫn phải kiểm tra lại đủ ma trận và quyền theo kiến trúc đề thi của nhóm.

## 6. Quy tắc bắt buộc ở backend

- Không tin `schoolId`, `departmentId`, `examId` hoặc role do client tự
  gửi. Backend lấy danh tính từ phiên đăng nhập và kiểm tra quan hệ gán
  quyền với tài nguyên thật trong database.
- Mọi API đọc danh sách, xem nội dung/đáp án, sửa, duyệt, tải và xuất đề
  đều kiểm tra phạm vi trường, bộ môn hoặc kỳ thi cấp Bộ tương ứng.
- AI và worker không có quyền tự chuyển đề sang `APPROVED`.
- Người tạo không được duyệt chính phiên bản đề của mình, kể cả khi có
  nhiều role hoặc là `MinistryAdmin`.
- Quyết định dựa trên phiên bản cũ bị từ chối; chỉ phiên bản người duyệt
  đã kiểm tra mới được xác nhận.
- Quyết định, chuyển trạng thái đề và `AuditEvent` phải cùng một giao dịch.
  Nếu ghi audit thất bại, đề vẫn `PENDING_REVIEW` và không lưu quyết định.
- Việc cấp/thu hồi role và phạm vi phải được audit và có hiệu lực ở lần
  kiểm tra quyền tiếp theo, kể cả khi người dùng còn phiên đăng nhập cũ.
- Giao diện chỉ hiển thị thao tác được phép; backend vẫn là nơi quyết định.

## 7. Dữ liệu và hợp đồng cần thống nhất

Các thực thể hoặc trường tối thiểu dự kiến: `User`, `RoleAssignment`,
`School`, `Department`, `DepartmentAssignment`, `ExamEvent` (cấp trường
hoặc cấp Bộ), `ExamRevision`, `ReviewDecision`, `AuditEvent`. Mỗi đề cần
biết cấp tổ chức, chủ sở hữu, trường/bộ môn hoặc kỳ thi cấp Bộ, người
soạn, phiên bản, trạng thái và người quyết định.

API quản lý role, tạo đề, danh sách chờ duyệt và quyết định duyệt phải
định nghĩa rõ quyền và phạm vi. Trường dữ liệu và endpoint cụ thể sẽ
được chốt cùng M1/M4 trước khi viết migration hoặc controller.

## 8. Các trường hợp cần kiểm thử

- Teacher gọi trực tiếp API duyệt đề cấp trường bị từ chối (`403`).
- DepartmentHead duyệt đúng trường–bộ môn và phiên bản thì thành công.
- DepartmentHead xem hoặc duyệt đề trường khác hay bộ môn khác bị từ
  chối (`403`), kể cả khi đổi ID trong URL.
- SchoolAdmin tạo tài khoản ở trường khác hoặc cấp role `MinistryAdmin`
  bị từ chối (`403`).
- MinistryAdmin chưa được giao kỳ thi không xem được nội dung đề cấp Bộ.
- MinistryAdmin soạn đề cấp Bộ không tự xác nhận được; một tài khoản
  khác được giao kỳ thi có thể xác nhận.
- AI cố ghi trạng thái `APPROVED` bị từ chối.
- Từ chối không có lý do bị từ chối (`400`); duyệt phiên bản cũ bị từ
  chối (`409`).
- Khi ghi audit thất bại, quyết định và chuyển trạng thái đều rollback.
- Sau khi thu hồi role hoặc phạm vi, phiên đăng nhập cũ không còn quyền.

## 9. Các quyết định cần nhóm chốt

1. Có mở phạm vi nhiều trường và kỳ thi cấp Bộ trong MVP hay chỉ thiết
   kế trước rồi triển khai sau? README và kế hoạch hiện ghi MVP một trường.
2. Ai được tạo kỳ thi cấp Bộ, ai được giao soạn và ai được giao xác nhận?
   Bản đề xuất yêu cầu hai tài khoản `MinistryAdmin` độc lập.
3. Đề cấp trường được duyệt toàn bộ như trên, hay câu hỏi AI còn phải
   được duyệt riêng trước khi đưa vào đề? Kiến trúc hiện tại yêu cầu
   chỉ dùng câu hỏi đã được duyệt.
4. Cách đăng nhập, quản lý phiên, phạm vi trường–bộ môn và bảo vệ nội
   dung đề thi sẽ được ghi trong ADR trước khi triển khai M3-02.
