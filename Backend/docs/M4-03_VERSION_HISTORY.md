# M4-03: Lịch sử phiên bản và chất lượng truy vấn

**Status:** Backend, migration và bằng chứng truy vấn đã làm và kiểm chứng trên SQL Server cục bộ (`.\SQLEXPRESS`); chưa deploy. UI xem/so sánh revision đã có; chưa có nút gửi duyệt (xem mục 7). Liên quan: [API M4-02](M4-02_QUESTION_API.md), [domain M4-01](M4-01_QUESTION_DOMAIN.md), thư mục bằng chứng [M4-03/](M4-03/).

## 1. Quy ước revision

- Một câu hỏi (`Questions`) chỉ là danh tính và phạm vi. Nội dung nằm trong các revision bất biến `(QuestionId, Revision)`; sửa nội dung là thêm revision mới ở trạng thái `DRAFT`.
- `Questions.LatestRevision` là con trỏ tới revision **mới nhất**, thường là bản nháp. Nó **không** phải revision dùng để thi.
- Revision **đang dùng** là revision `APPROVED` mới nhất của câu (`ApprovedQuestionReader`). Khi sửa một câu đã duyệt, bản cũ vẫn được dùng cho tới khi bản mới được duyệt. Đề đã pin `QuestionRevisionRef` cũ không đổi, và bản mới phải qua review lại.
- Không có cột con trỏ riêng cho revision đang dùng: M3 sẽ phải nhớ cập nhật nó và dễ lệch. Giá trị được suy ra từ trạng thái của các revision, và API trả `approvedRevision` / `isApprovedInUse`.
- Trạng thái dùng `PENDING_REVIEW` (thống nhất với nhánh M3-03).

## 2. Bất biến, ở hai tầng

| Quy tắc | Ứng dụng (`QuestionImmutabilityGuard`, ném `QuestionImmutableException`) | Cơ sở dữ liệu (trigger, `THROW`) |
|---|---|---|
| Nội dung revision không đổi (kể cả bản `DRAFT`) | có | `TR_QuestionRevisions_Immutable`, lỗi 51002 |
| Revision không bị xóa hay đánh số lại | có | 51001 (và khóa ngoại từ `QuestionOptions`, lỗi 547) |
| `APPROVED` / `REJECTED` là trạng thái cuối | có | 51003 |
| Option không đổi, không xóa | có | `TR_QuestionOptions_Immutable`, 51004 |
| Câu hỏi không bị xóa | có | `TR_Questions_Immutable`, 51005 (và khóa ngoại, 547) |
| `LatestRevision` chỉ tăng | có | 51006 |

Chỉ cột `Status` của revision được đổi, theo luồng Review của M3 (`DRAFT → PENDING_REVIEW → APPROVED/REJECTED`); các bước chưa cuối không bị hạn chế ở đây. Sửa một câu đang `PENDING_REVIEW` vẫn bị writer từ chối (409). Từ khi M3-03 vào `dev`, luồng Review ghi thêm `ReviewVersion` (token chống ghi đè) và `SubmittedAtUtc` cùng lúc với `Status`; `QuestionImmutabilityGuard` cho phép đúng ba cột này và đóng băng cả ba khi revision đã `APPROVED`/`REJECTED`. Trigger SQL chỉ so các cột nội dung và `Status`, nên không chặn `ReviewVersion`; việc đóng băng nó nằm ở tầng ứng dụng.

Bảng có trigger phải được khai báo trong mô hình EF (`HasTrigger`), nếu không SQL Server từ chối `UPDATE ... OUTPUT` mà EF dùng để đọc lại `RowVersion`.

## 3. API lịch sử

Cùng quyền đọc với `GET /questions/{id}`: câu không tồn tại và câu người gọi không được đọc đều trả 404 `QUESTION_NOT_FOUND`. Đáp án của revision cũ nhạy cảm như bản mới nhất nên không nới quyền.

| Route | Mô tả |
|---|---|
| `GET /questions/{id}/revisions` | Mọi revision, **mới nhất trước**, có đủ nội dung và đáp án. Kèm `latestRevision`, `approvedRevision` |
| `GET /questions/{id}/revisions/{revision}` | Một revision. 404 nếu câu hoặc revision không có |
| `GET /questions/{id}` | Có thêm `approvedRevision` (null nếu chưa có bản duyệt) |

Mỗi revision có `status`, `origin`, `topic`, `difficulty`, `source`, `importBatchId`, `importRowNumber`, `createdByUserId`, `createdAt`, `isLatest`, `isApprovedInUse`. `importBatchId` / `importRowNumber` là định danh nguồn gốc của dòng CSV đã sinh ra revision đó (null với bản nhập tay). OpenAPI cập nhật trong [openapi/questions.json](openapi/questions.json) khi sinh lại (xem [openapi/README.md](openapi/README.md)).

Truy vấn lịch sử tốn số query cố định (không N+1): câu hỏi, trạng thái mới nhất, phân quyền, các revision, các option, tên chủ đề, tên môn. Test so sánh lịch sử 1 revision với 9 revision và đòi cùng số query.

## 4. Migration

Hai migration, theo thứ tự:

1. `20261110075651_QuestionHistoryIndexes`: thay `IX_Questions_OwnerUserId` bằng `IX_Questions_OwnerUserId_UpdatedAtUtc_Id` và `IX_Questions_SchoolId_SubjectId_DepartmentId` bằng `IX_Questions_SchoolId_SubjectId_DepartmentId_UpdatedAtUtc_Id` (có `UpdatedAtUtc DESC` và cột INCLUDE).
2. `20261110075848_QuestionImmutabilityTriggers`: ba trigger ở mục 2.

Áp dụng theo cơ chế chung (không thêm service): `dotnet ef database update --project Persistence/AiExamBank.Persistence --startup-project Api` với `ConnectionStrings__DefaultConnection` trỏ tới database đích.

Hoàn tác về M4-02: `dotnet ef database update 20261029062927_QuestionBank ...`. Đã chạy Up → Down → Up trên database sạch: sau Down không còn trigger và index cũ trở lại. Cả hai migration không đổi dữ liệu, chỉ đổi index và thêm trigger, nên áp trên database đã có dữ liệu là an toàn; việc tạo index khóa bảng trong thời gian ngắn, nên áp ngoài giờ cao điểm nếu bảng lớn.

Khi một script cần sửa dữ liệu lịch sử (ví dụ dọn dữ liệu thử), phải `DISABLE TRIGGER` rõ ràng và có chủ đích; đừng tắt vĩnh viễn.

## 5. Bằng chứng truy vấn trên seed

Seed: 60.000 câu, 150.000 revision, 600.000 option (1 trường, 20 giáo viên, 1 trưởng bộ môn), [query_evidence_seed.sql](M4-03/query_evidence_seed.sql). Chỉ số là **logical reads** (trang 8 KB), ổn định giữa các lần chạy; thời gian chạy trên SQL Express rất nhiễu nên không dùng làm bằng chứng. Cách tái lập: xem [M4-03/README.md](M4-03/README.md).

Đo bằng đúng câu SQL của ứng dụng, qua `sp_executesql` ([index_experiment.sql](M4-03/index_experiment.sql)); số reads trên bảng `Questions`:

| Trường hợp | Trước | Sau |
|---|---:|---:|
| Trưởng bộ môn: hàng đợi, trang 1 | 1.025 | 4 |
| Trưởng bộ môn: hàng đợi + chủ đề + độ khó, trang 1 | 1.025 | 12 |
| Trưởng bộ môn: đếm hàng đợi | 1.025 | 760 |
| Giáo viên: danh sách của mình, trang 1 | 1.025 | 3 |
| Giáo viên: trang sâu (bỏ qua 1.000 dòng) | 1.025 | 16 |
| Giáo viên: chủ đề + độ khó, trang 1 | 1.025 | 4 |
| Giáo viên: đếm | 1.025 | 41 |

Reads trên `QuestionRevisions` không đổi ở mọi trường hợp (không tệ đi). Trước đó mỗi truy vấn danh sách quét toàn bộ `Questions`; sau đó chỉ đọc vài trang rồi dừng.

**Một ứng viên bị loại.** Index hẹp `QuestionRevisions (QuestionId, Revision) INCLUDE (Status, TopicId, Difficulty)` không giảm reads trên seed (stem trong seed ngắn nên bảng gốc không rộng) và làm các truy vấn đếm tốn thêm, nên không đưa vào. Nên đo lại nếu stem thật dài hơn nhiều.

## 6. Giới hạn đã biết

- Tổng reads của cả bộ truy vấn chỉ giảm khoảng 4% (236.267 → 225.740, [evidence_before.txt](M4-03/evidence_before.txt), [evidence_after.txt](M4-03/evidence_after.txt)) vì bị một truy vấn chi phối: trưởng bộ môn lọc theo chủ đề và độ khó **mà không có kết quả khớp** tốn khoảng 192.000 reads (quét hết 60.000 câu, mỗi câu một lần tra revision). Index hiện tại không giải quyết được vì các bộ lọc (trạng thái, chủ đề, độ khó) nằm trên revision mới nhất, còn phân trang và phân quyền nằm trên `Questions`.
- Cách xử lý triệt để là chép trạng thái, chủ đề, độ khó của revision mới nhất lên `Questions` để lọc và index trên một bảng. Chưa làm vì nó buộc mọi nơi đổi trạng thái revision (luồng Review của M3) cũng phải cập nhật `Questions` trong cùng giao dịch, và một trigger tự đồng bộ có thể làm `RowVersion` của `Questions` vô hiệu giữa chừng một lần lưu của EF. Nên quyết định cùng M3 nếu ngân hàng của một trường dự kiến vượt vài trăm nghìn câu.
- Các truy vấn đếm (tổng số cho phân trang) vẫn đọc toàn bộ revision mới nhất khớp bộ lọc.
- Tìm theo văn bản (`LIKE '%...%'`) quét toàn bộ; chấp nhận được ở quy mô này, cần full-text nếu tìm kiếm trở thành chức năng chính.

## 7. Còn lại của M4-03

- Nút gửi duyệt: endpoint và nút ở danh sách đã vào `dev` cùng M3-03. Chưa có nút gửi duyệt trong modal lịch sử/trình soạn thảo.
- ~~UI xem và so sánh các revision~~ (đã làm): nút Lịch sử trên danh sách mở `QuestionHistoryModal`, gọi `GET /questions/{id}/revisions`, xem nội dung từng revision và so sánh hai revision theo từng trường (`revisionDiff.ts`). Trình soạn thảo có nút "Lịch sử revision" mở lịch sử phía trên form, form giữ nguyên nội dung đang gõ. Còn lại: chụp ảnh/video demo.
- Sinh lại `openapi/questions.json` từ API đang chạy.
- Video/ảnh demo revision, ghi PR, giờ thực tế.

## 8. Smoke test trên API thật (sau khi gộp M3-03)

`tools/m403_smoke.ps1` chạy trên API Development và database dùng một lần, kết quả trong [M4-03/evidence_smoke.txt](M4-03/evidence_smoke.txt): tạo câu, gửi duyệt, duyệt revision 1, sửa câu đã duyệt (revision 2 là DRAFT, revision 1 vẫn đang dùng), cập nhật từ revision cũ trả 409, lịch sử mới nhất trước và revision 1 không đổi, duyệt revision 2 thì revision đang dùng chuyển sang 2, trưởng bộ môn đọc được lịch sử lúc câu chờ duyệt, giáo viên tổ khác nhận 404. Sau đó các lệnh SQL thô sửa nội dung, mở lại bản đã duyệt, xóa revision, sửa option, xóa câu đều bị trigger hoặc khóa ngoại từ chối.

Lần chạy đầu bắt được một lỗi tích hợp mà gộp sạch và test riêng từng nhánh đều không thấy: `submit-review` trả 500 vì guard coi `ReviewVersion` là nội dung bất biến. Đã sửa guard và thêm test `The_review_flow_may_move_status_version_and_submit_time_together_but_not_after_a_decision`.

Giới hạn theo chính sách M3-02: trưởng bộ môn chỉ đọc câu đang `PENDING_REVIEW`, nên sau khi quyết định họ nhận 404 với lịch sử (script kiểm đúng điều này).

Chạy lại: khởi động API với `ASPNETCORE_ENVIRONMENT=Development`, `LocalDemoUser__Enabled=true`, `LocalDemoUser__Password`, `LocalDemoUser__BootstrapMinistryAdmin=true`, `Jobs__DisableEmbeddedWorker=true`, rồi `powershell -File tools/m403_smoke.ps1 -Password <mật khẩu>`. Script chạy được trên Windows PowerShell 5.1 và PowerShell 7. Đăng nhập bị giới hạn tốc độ (429): đợi một phút giữa các lần chạy.
