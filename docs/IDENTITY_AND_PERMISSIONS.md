# Vai trò và phân quyền nhiều trường/cấp Bộ

**Trạng thái:** Phạm vi MVP được project owner chốt ngày 09/10/2026; đây là hợp đồng thiết kế, chưa có auth/API thực thi.
**Người thực hiện:** Minh Phúc — Lancelot-sys25
**Liên quan:** M3-01 (thiết kế đã merge); M3-02 (triển khai đăng nhập và phân quyền)

## 1. Phạm vi MVP

Hệ thống có bốn role: `MinistryAdmin` (quản trị cấp Bộ), `SchoolAdmin`
(quản trị cấp trường), `DepartmentHead` (trưởng bộ môn) và `Teacher`
(giáo viên). `MinistryAdmin` quản lý toàn hệ thống; ba role còn lại chỉ
thao tác trong trường và bộ môn được phân công. Một tài khoản có thể giữ
nhiều role nếu được gán rõ ràng, nhưng mỗi thao tác vẫn phải kiểm tra
đúng phạm vi tài nguyên.

Kỳ thi thường do từng trường tự tổ chức: `SchoolAdmin` quản lý người dùng
và phân công, `Teacher` soạn đề, `DepartmentHead` xem và chọn đề. Cấp Bộ
không tham gia quy trình chọn đề thường của trường. `MinistryAdmin` chỉ
tham gia nội dung đề khi có kỳ thi cấp Bộ và tài khoản đó được giao vào
kỳ thi. Nếu dùng AI, Teacher tự kiểm tra và chấp nhận, sửa hoặc bỏ từng
gợi ý trước khi gửi đề; AI không tự duyệt nội dung.

MVP bao gồm nhiều trường và kỳ thi cấp Bộ. Phạm vi này thay thế giả định
một trường trong kế hoạch ban đầu. Các endpoint, migration và quyền vận
hành vẫn phải được triển khai và kiểm thử; quyết định phạm vi không đồng
nghĩa tính năng đã hoàn thành. Xem [ADR 0004](adr/0004-multi-school-ministry-mvp.md).

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

Quyền quản trị cấp Bộ không tự động cấp quyền đọc, sửa hoặc chọn đề thi
thường của từng trường. Khi trường tổ chức kỳ kiểm tra nội bộ,
`SchoolAdmin` quản lý tài khoản và phạm vi, còn `DepartmentHead` chọn đề
trong bộ môn.

Chỉ `MinistryAdmin` được tạo hoặc thu hồi tài khoản `SchoolAdmin`.
`SchoolAdmin` gán hoặc thu hồi `Teacher` và `DepartmentHead` trong trường
của mình. Không role nào được tự nâng quyền hoặc tự đổi phạm vi của mình.

## 3. Bảng quyền

| Hành động | Teacher | DepartmentHead | SchoolAdmin | MinistryAdmin |
|---|---|---|---|---|
| Tự soạn hoặc dùng AI hỗ trợ tạo đề cấp trường | Có, trong phạm vi được giao | Chỉ khi có thêm role Teacher | Không mặc định | Không |
| Kiểm tra và chấp nhận/sửa/bỏ gợi ý AI trong đề của mình | Có | Khi có role Teacher | Không | Không |
| Xem đề mình yêu cầu tạo | Có | Khi có role Teacher | Không mặc định | Không mặc định |
| Gửi đề cấp trường đi chọn | Có, với đề của mình | Khi có role Teacher | Không | Không |
| Xem nội dung đề chờ duyệt cấp trường | Không | Có, theo trường–bộ môn | Không mặc định | Không mặc định |
| Chọn hoặc trả lại đề cấp trường | Không | Có, theo trường–bộ môn | Không | Không |
| Quản lý Teacher/DepartmentHead | Không | Không | Trong trường mình | Chỉ khi xử lý quản trị cấp hệ thống |
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

1. `SchoolAdmin` phân công giáo viên và trưởng bộ môn trong trường. Cấp
   Bộ không tham gia bước này hoặc xem nội dung đề thường.
2. Teacher chọn môn, chủ đề, độ khó, số câu và số điểm rồi tự soạn đề.
   Teacher có thể dùng AI để gợi ý câu hỏi. Với mỗi gợi ý, Teacher tự
   kiểm tra nội dung, đáp án, độ khó và nguồn nếu có, rồi chấp nhận,
   sửa hoặc bỏ vào bản nháp. Teacher chịu trách nhiệm về nội dung đề mình gửi.
3. Khi hài lòng, Teacher gửi **một phiên bản cố định** của đề. Sau khi
   gửi, Teacher không sửa trực tiếp phiên bản đó. Nếu muốn thay đổi,
   Teacher tạo phiên bản mới và gửi lại; bản cũ được lưu lịch sử và
   không còn nằm trong danh sách chờ chọn khi bản mới được gửi. Nếu bản
   cũ đã được chọn, nó vẫn là đề được chọn cho đến khi DepartmentHead
   quyết định thay bằng phiên bản khác.
4. Một đợt kiểm tra có thể nhận nhiều đề từ các Teacher. Các đề được
   so sánh với nhau khi cùng **trường, môn học, khối lớp và đợt kiểm
   tra**. DepartmentHead đúng trường–bộ môn xem các đề, kiểm tra ma
   trận, câu hỏi, đáp án, độ khó, trùng lặp và nguồn tham khảo rồi chọn
   **một đề** phù hợp.
5. Các đề còn lại có trạng thái `NOT_SELECTED`; trạng thái này chỉ có
   nghĩa là không được chọn, không khẳng định đề đúng hay sai. Chúng vẫn
   được lưu để tra cứu, không bị xóa. Đề được xác định là chưa đạt phải
   được trả lại kèm lý do (`REJECTED`). Nếu không đề nào đạt,
   DepartmentHead trả lại từng đề, không có đề được chọn. Teacher sửa
   thành phiên bản mới rồi gửi lại.
6. Chỉ phiên bản được DepartmentHead chọn mới đủ điều kiện để chốt và sử
   dụng tại trường. `SchoolAdmin` không mặc định có quyền chọn đề.

Teacher chấp nhận một gợi ý AI nghĩa là đưa nó vào **bản nháp đề của
mình**, không tự cấp quyền dùng câu đó trong đề cuối hay ngân hàng chung.
Trước khi một đề có thể được chọn và chốt, **mọi QuestionRevision trong
đề phải ở trạng thái APPROVED** theo luồng Review/Audit hiện có. Quyết
định chọn toàn bộ đề của DepartmentHead không thay thế quyết định duyệt
từng QuestionRevision; người soạn không được tự duyệt câu mình tạo.

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
                       → NOT_SELECTED (chỉ đề cấp trường)
                       → SUPERSEDED (chỉ bản đang chờ, khi nộp bản mới)
```

Với đề cấp trường, `APPROVED` nghĩa là đề đã được Trưởng bộ môn chọn.
Một kỳ kiểm tra chỉ có một phiên bản đề đang được chọn; việc đổi lựa chọn
cần ghi audit và không làm mất lịch sử lựa chọn cũ. Với đề cấp Bộ,
`APPROVED` nghĩa là đề đã được người xác nhận cấp Bộ chấp thuận. Việc
chốt snapshot phát hành vẫn phải kiểm tra lại đủ ma trận và quyền theo
kiến trúc đề thi của nhóm.

`NOT_SELECTED` khác `REJECTED`: không được chọn chưa đủ để kết luận đề
sai. `SUPERSEDED` là phiên bản **đang chờ chọn** được thay bằng phiên bản
mới của cùng giáo viên cho cùng đợt kiểm tra; bản cũ vẫn bất biến và
được giữ để truy vết. Bản đã `APPROVED` không tự chuyển sang
`SUPERSEDED` khi giáo viên nộp bản mới. Bản đã `NOT_SELECTED` hoặc
`REJECTED` cũng giữ nguyên trạng thái lịch sử khi có bản mới; chỉ bản
`PENDING_REVIEW` chưa được quyết định mới chuyển sang `SUPERSEDED`.
Lịch sử có thể giữ một phiên bản từng được `APPROVED`; phiên bản hiện
được dùng cho đợt kiểm tra luôn được xác định bằng con trỏ lựa chọn của
`ExamEvent`, không chỉ dựa vào trạng thái của các phiên bản trong lịch sử.

## 6. Quy tắc bắt buộc ở backend

- Không tin `schoolId`, `departmentId`, `examId` hoặc role do client tự
  gửi. Backend lấy danh tính từ phiên đăng nhập và kiểm tra quan hệ gán
  quyền với tài nguyên thật trong database.
- Mọi API đọc danh sách, xem nội dung/đáp án, sửa, duyệt, tải và xuất đề
  đều kiểm tra phạm vi trường, bộ môn hoặc kỳ thi cấp Bộ tương ứng.
- AI và worker không có quyền tự chuyển đề sang `APPROVED`.
- Teacher có thể chấp nhận gợi ý AI vào bản nháp mình sở hữu, nhưng
  không thể chuyển đề hoặc `QuestionRevision` trong ngân hàng chung
  sang `APPROVED` bằng thao tác đó.
- Mọi câu trong đề được chọn/finalize phải tham chiếu `QuestionRevision`
  đã được người có quyền duyệt, đúng scope và đúng phiên bản.
- Người tạo không được duyệt chính phiên bản đề của mình, kể cả khi có
  nhiều role hoặc là `MinistryAdmin`.
- Quyết định dựa trên phiên bản cũ bị từ chối; chỉ phiên bản người duyệt
  đã kiểm tra mới được xác nhận.
- Chọn đề cấp trường phải bảo đảm không có hai đề cùng được chọn cho một
  kỳ kiểm tra tại cùng thời điểm, kể cả khi hai người thao tác đồng thời.
- Quyết định, con trỏ đề được chọn của `ExamEvent`, trạng thái các đề
  liên quan và `AuditEvent` phải cùng một giao dịch. Nếu ghi bất kỳ phần
  nào thất bại, không thay đổi lựa chọn và không lưu quyết định dở dang.
- Việc cấp/thu hồi role và phạm vi phải được audit và có hiệu lực ở lần
  kiểm tra quyền tiếp theo, kể cả khi người dùng còn phiên đăng nhập cũ.
- Giao diện chỉ hiển thị thao tác được phép; backend vẫn là nơi quyết định.

## 7. Dữ liệu và hợp đồng cần thống nhất

Các thực thể hoặc trường tối thiểu dự kiến: `User`, `RoleAssignment`,
`School`, `Department`, `DepartmentAssignment`, `ExamEvent` (cấp trường
hoặc cấp Bộ), `ExamRevision`, `ReviewDecision`, `AuditEvent`. Một
`ExamEvent` cấp trường cần xác định trường, môn, khối lớp và đợt kiểm
tra để gom các đề cùng cạnh tranh. Mỗi đề cần biết cấp tổ chức, chủ sở
hữu, người soạn, phiên bản, trạng thái và người quyết định; `ExamEvent`
chỉ trỏ tới một phiên bản đang được chọn tại một thời điểm.

API quản lý role, tạo đề, danh sách chờ duyệt và quyết định duyệt phải
định nghĩa rõ quyền và phạm vi. Trường dữ liệu và endpoint cụ thể sẽ
được chốt cùng M1/M4 trước khi viết migration hoặc controller.

## 8. Các trường hợp cần kiểm thử

- Teacher gọi trực tiếp API duyệt đề cấp trường bị từ chối (`403`).
- DepartmentHead duyệt đúng trường–bộ môn và phiên bản thì thành công.
- DepartmentHead xem hoặc duyệt đề trường khác hay bộ môn khác bị từ
  chối (`403`), kể cả khi đổi ID trong URL.
- SchoolAdmin và MinistryAdmin không được gọi API chọn đề cấp trường.
- Nếu nhiều Teacher nộp đề cho cùng kỳ kiểm tra, chỉ một phiên bản được
  chọn; đề còn lại là `NOT_SELECTED` hoặc `REJECTED` theo quyết định và
  vẫn giữ lịch sử.
- Hai DepartmentHead chọn đồng thời hai phiên bản khác nhau cho cùng
  `ExamEvent`: chỉ một giao dịch thành công; giao dịch còn lại nhận `409`.
- Khi Teacher nộp phiên bản sửa, bản đã nộp trước đó không bị thay đổi và
  không còn là ứng viên chờ chọn. Nếu bản cũ đã được chọn, lựa chọn đó
  chỉ đổi khi DepartmentHead chọn phiên bản khác.
- Bản cũ `NOT_SELECTED` hoặc `REJECTED` không đổi trạng thái khi nộp bản mới.
- Nếu không đề nào đạt, DepartmentHead trả lại kèm lý do; không có đề
  nào chuyển sang `APPROVED`.
- SchoolAdmin tạo tài khoản ở trường khác hoặc cấp role `MinistryAdmin`
  bị từ chối (`403`).
- MinistryAdmin chưa được giao kỳ thi không xem được nội dung đề cấp Bộ.
- MinistryAdmin soạn đề cấp Bộ không tự xác nhận được; một tài khoản
  khác được giao kỳ thi có thể xác nhận.
- AI cố ghi trạng thái `APPROVED` bị từ chối.
- Từ chối không có lý do bị từ chối (`400`); duyệt phiên bản cũ bị từ
  chối (`409`).
- Khi ghi audit hoặc cập nhật con trỏ lựa chọn thất bại, quyết định,
  chuyển trạng thái và con trỏ đều rollback.
- Sau khi thu hồi role hoặc phạm vi, phiên đăng nhập cũ không còn quyền.

## 9. Các quyết định triển khai còn mở

1. M3/M1 phải chốt API gán hai `MinistryAdmin` độc lập vào từng kỳ thi
   và quy trình cấp/thu hồi quyền trước khi mở endpoint soạn/xác nhận.
2. M1/M3/M4 phải chốt hợp đồng bản nháp câu AI → `QuestionRevision` →
   review độc lập → câu đủ điều kiện chọn, rồi kiểm thử toàn luồng.
3. Cách đăng nhập, quản lý phiên và bảo vệ nội dung đề thi vẫn theo
   ADR 0003 đang đề xuất; cần chốt trước khi triển khai M3-02.
