# M4-02: Question Bank API và hợp đồng writer cho Import

**Status:** Đã kiểm chứng migration và API trên SQL Server cục bộ trong database review riêng; đã quay video UI nối API thật trên SQL Server cục bộ; đã kiểm ghi đồng thời và import writer (mức writer) trên SQL Server cục bộ; chưa kiểm import qua API (chờ M2-03) và chưa deploy/staging. Xem mục 6 cho phần đã và chưa kiểm chứng. Liên quan: [domain M4-01](M4-01_QUESTION_DOMAIN.md), [wireframe](M4-01_QUESTION_BANK_WIREFRAME.md), [CSV contract M2-01](M2-01_deploy_network.md).

OpenAPI 3.1 của các route dưới đây nằm ở [openapi/questions.json](openapi/questions.json), sinh từ endpoint thật (xem [openapi/README.md](openapi/README.md) để tạo lại). Tất cả route nằm dưới `/api/v1`, yêu cầu đăng nhập (cookie) và token antiforgery `X-CSRF-TOKEN` cho `POST`/`PUT`. Trường học luôn lấy từ tài khoản đang đăng nhập, không nhận từ request. Trạng thái luôn do server quyết định; request không có trường `status`.

## 1. Endpoint

| Method và route | Mô tả | Mã trả về |
|---|---|---|
| `GET /catalog` | Môn học và chủ đề của các bộ môn mà người dùng phụ trách (Teacher hoặc DepartmentHead). Mỗi môn có `canWrite`: chỉ Teacher của bộ môn mới ghi được | 200, 401, 403 |
| `GET /questions` | Danh sách, lọc và phân trang | 200, 400, 401, 403 |
| `GET /questions/{id}` | Revision mới nhất kèm danh sách mọi revision | 200, 401, 404 |
| `POST /questions` | Tạo câu hỏi và revision 1 ở trạng thái `DRAFT` | 201, 400, 401, 403 |
| `PUT /questions/{id}` | Tạo revision mới (`DRAFT`); không sửa tại chỗ | 200, 400, 401, 403, 404, 409 |
| `GET /admin/schools/{schoolId}/subjects` | Môn trường đang dạy và bộ môn phụ trách | 200, 401, 403 |
| `PUT /admin/schools/{schoolId}/subjects/{subjectId}` | Gán môn cho một bộ môn của trường | 200, 201, 400, 403, 404, 409 |

Hai route quản trị dành cho `SchoolAdmin` của trường đó hoặc `MinistryAdmin`. Một trường chưa gán môn thì không có môn nào để tạo câu hỏi. Một môn chỉ đổi được bộ môn khi chưa có câu hỏi nào (409 `SCHOOL_SUBJECT_HAS_QUESTIONS` nếu đã có), vì phạm vi của câu hỏi gắn với bộ môn lúc ghi. Gán lại đúng bộ môn hiện tại trả 200.

### `GET /questions`

Tham số: `subjectId`, `topicId`, `difficulty` (`EASY`, `MEDIUM`, `HARD`), `status` (`DRAFT`, `PENDING_REVIEW`, `APPROVED`, `REJECTED`), `q`, `page` (từ 1), `pageSize` (mặc định 20, tối đa 100). Bộ lọc `status` áp dụng cho revision mới nhất của mỗi câu. `q` tìm trong nội dung hoặc khớp mã câu hỏi. Kết quả sắp theo thời điểm cập nhật mới nhất, rồi theo mã câu hỏi.

```json
{
  "items": [{
    "id": "7b1c…", "revision": 2, "status": "DRAFT",
    "stem": "Hàm số y = x³ − 3x + 2 đồng biến trên khoảng nào?",
    "subject": { "id": "8ab2…", "name": "Toán" },
    "topic": { "id": "223f…", "name": "Ứng dụng đạo hàm để khảo sát hàm số" },
    "difficulty": "EASY", "ownerUserId": "c0f3…", "updatedAt": "2026-10-29T06:30:00+00:00"
  }],
  "page": 1, "pageSize": 20, "total": 1
}
```

Danh sách không trả đáp án; đáp án nằm trong `GET /questions/{id}`.

### `POST /questions` và `PUT /questions/{id}`

```json
{
  "stem": "Hàm số y = x³ − 3x + 2 đồng biến trên khoảng nào?",
  "options": ["(−∞; −1) và (1; +∞)", "(−1; 1)", "(−∞; 1)", "(−1; +∞)"],
  "correctOption": "A",
  "difficulty": "EASY",
  "subjectId": "8ab2a612-3558-435f-9e60-ef3f8e24a8a8",
  "topicId": "223f2355-059b-4195-80e0-6056ea6115f6",
  "source": "SGK Toán 12, tr. 12",
  "baseRevision": 1
}
```

`baseRevision` chỉ dùng cho `PUT` và bắt buộc: là revision mà người soạn đã mở. Nếu câu hỏi đã có revision mới hơn, server trả 409. Môn của câu hỏi không đổi được sau khi tạo.

## 2. Lỗi

Lỗi là Problem Details có `code` ổn định và `traceId`.

| HTTP | `code` | Khi nào |
|---|---|---|
| 400 | `QUESTION_INVALID` | Nội dung sai luật. Kèm `errors[]` gồm `code`, `field`, `message` cho từng lỗi |
| 400 | `QUESTION_FILTER_INVALID` | Bộ lọc hoặc phân trang không hợp lệ |
| 400 | `QUESTION_BASE_REVISION_REQUIRED` | `PUT` thiếu `baseRevision` |
| 403 | `QUESTION_FORBIDDEN` | Không có quyền ghi vào môn hoặc sửa câu hỏi này |
| 404 | `QUESTION_NOT_FOUND` | Không tồn tại, hoặc người dùng không được đọc (không tiết lộ sự tồn tại) |
| 409 | `QUESTION_CONFLICT` | `baseRevision` không còn là mới nhất, ghi đồng thời, hoặc câu hỏi đang chờ duyệt (`PENDING_REVIEW`) nên chưa sửa được |

Mã lỗi theo trường nằm trong `errors[].code`: `QUESTION_STEM_REQUIRED`, `QUESTION_STEM_TOO_LONG`, `QUESTION_OPTIONS_COUNT_INVALID`, `QUESTION_OPTION_REQUIRED`, `QUESTION_OPTION_TOO_LONG`, `QUESTION_OPTIONS_DUPLICATE`, `QUESTION_CORRECT_OPTION_REQUIRED`, `QUESTION_CORRECT_OPTION_INVALID`, `QUESTION_DIFFICULTY_INVALID`, `QUESTION_SUBJECT_REQUIRED`, `QUESTION_SUBJECT_NOT_FOUND`, `QUESTION_SUBJECT_IMMUTABLE`, `QUESTION_TOPIC_REQUIRED`, `QUESTION_TOPIC_NOT_FOUND`, `QUESTION_TOPIC_SUBJECT_MISMATCH`, `QUESTION_SOURCE_TOO_LONG`. `field` có dạng `stem`, `options[2]`, `correctOption`, `difficulty`, `subjectId`, `topicId`, `source`.

## 3. Quyền

Quy tắc dùng `ResourceAccessPolicy` hiện có của M3, không nới thêm:

- **Teacher** tạo câu hỏi cho môn thuộc bộ môn mình được phân công; đọc và sửa câu do chính mình sở hữu. Không sửa được khi revision mới nhất đang chờ duyệt, để câu không rút khỏi hàng đợi của người duyệt; sau khi có quyết định (duyệt hoặc trả lại) thì sửa tiếp bằng revision mới. Quy tắc này cần M3 xác nhận.
- **DepartmentHead** đọc câu đã gửi duyệt (`PENDING_REVIEW`) trong bộ môn mình; không sửa.
- **SchoolAdmin**, **MinistryAdmin** không đọc hay sửa nội dung câu hỏi.
- Quyền được đọc lại từ bảng phân quyền ở mỗi request, nên thu hồi vai trò có hiệu lực ngay.
- Bộ lọc danh sách là cùng quy tắc của policy viết dạng điều kiện truy vấn; một test so khớp hai bên trên mọi tổ hợp vai trò, chủ sở hữu và trạng thái.

Chưa có chuyển trạng thái nào trong module này. `PENDING_REVIEW`, `APPROVED`, `REJECTED` do luồng Review của M3 ghi.

## 4. Hợp đồng writer cho Import (M2)

Import ghi câu hỏi qua `IQuestionCatalog` và `IQuestionWriter`, không ghi thẳng bảng.

| Cột CSV | Trường | Ghi chú |
|---|---|---|
| `subject`, `topic` | `IQuestionCatalog.ResolveAsync(actor, school, subject, topic)` | Tra theo tên (trim, Unicode NFC, không phân biệt hoa thường) trong các môn mà actor được ghi. Không khớp thì trả `QUESTION_SUBJECT_NOT_FOUND` hoặc `QUESTION_TOPIC_NOT_FOUND`; import không tự tạo taxonomy |
| `difficulty` | `McqInput.Difficulty` | Nhận `easy`, `medium`, `hard` |
| `question_text` | `McqInput.Stem` | Tối đa 4.000 ký tự |
| `option_a`…`option_d` | `McqInput.Options` | Đúng 4 phần tử theo thứ tự A-D, mỗi phần tối đa 1.000 ký tự, không trùng |
| `correct_option` | `McqInput.CorrectOption` | A-D |
| `source` | `McqInput.SourceText` | Tối đa 2.000 ký tự |

Quy trình cho mỗi dòng:

1. Gọi `ResolveAsync`. Có lỗi thì ghi vào dòng preview, không gọi writer.
2. Dựng `McqInput` với `QuestionTopic.Create(topicId, subjectId)` từ kết quả.
3. Khi tạo preview, gọi `McqContent.Validate(input)` để lấy đủ lỗi nội dung mà chưa ghi.
4. Khi commit, gọi `IQuestionWriter.CreateDraftAsync(CreateQuestionCommand.Create(actor, school, input, QuestionProvenance.Import(batchId, rowNumber)))`. `rowNumber` là số dòng logic của CSV, header là dòng 1.

Cam kết của writer:

- Luôn tạo `DRAFT`; không có đường approve.
- Dùng cùng `DbContext` của request, nên nếu Import mở transaction thì việc ghi batch và ghi câu hỏi cùng commit hoặc cùng rollback.
- Ghi lại dòng đã ghi: cặp `(batchId, rowNumber)` là duy nhất trong DB. Gọi lại cho cùng dòng, kể cả hai lần retry đồng thời, ném `QuestionConflictException` thay vì ghi trùng, nên commit retry của M2 an toàn.
- Khi một lần ghi thất bại, các entity của lần gọi đó được bỏ khỏi `DbContext` dùng chung, nên Import tiếp tục sang dòng kế tiếp mà không bị lỗi cũ lặp lại. Import vẫn cần tự quyết định xử lý khi gặp xung đột (bỏ qua dòng đã ghi hoặc dừng batch).
- Lỗi nội dung ném `QuestionValidationException` chứa toàn bộ lỗi; không đủ quyền ném `QuestionAccessDeniedException`.

## 5. Dữ liệu

Migration `QuestionBank` thêm các bảng `Subjects`, `Topics`, `SchoolSubjects`, `Questions`, `QuestionRevisions`, `QuestionOptions`, cùng chuỗi migration với Identity. Migration seed 3 môn và 9 chủ đề với ID cố định; chủ đề vẫn chờ đối chiếu với tài liệu nguồn ([domain M4-01, mục 11](M4-01_QUESTION_DOMAIN.md)), nên cần chốt trước khi merge vì đổi sau phải thêm migration.

Ràng buộc chính: khóa `(QuestionId, Revision)`, câu hỏi phải thuộc đúng bộ môn đã gán cho môn đó, chủ đề phải thuộc đúng môn, `RowVersion` chống ghi đè đồng thời, không cascade xóa, và các check constraint cho trạng thái, độ khó, đáp án và nguồn gốc import. Index cho lọc theo chủ đề, độ khó, trạng thái và theo trường, môn, thời điểm cập nhật.

## 6. Kiểm chứng

| Hạng mục | Kết quả |
|---|---|
| Build toàn solution, test hiện có | Đạt (Questions 28, Persistence 12, Knowledge 3, Exams 3, Identity 8, Jobs 126, Architecture 3; chạy lại sau khi thêm OpenAPI, 2026-11-10) |
| Quy tắc nội dung MCQ, contract, quy tắc truy cập so với policy, mô hình EF và seed | Đạt (unit test) |
| Sinh migration và kiểm không còn thay đổi mô hình chưa vào migration | Đạt; SQL sinh ra đã xem lại |
| Áp migration lên SQL Server | Đạt trên database cục bộ riêng `AiExamBankM4Review20261103C` (2026-11-03); migration Identity và QuestionBank đều áp thành công. Chưa kiểm chứng trên staging |
| Writer, catalog, truy vấn, API và IDOR trên database thật | Smoke API cục bộ đạt: tạo trường/bộ môn, gán môn, tạo Teacher qua SchoolAdmin, catalog 200, tạo DRAFT 201, lọc kết hợp và phân trang 200, detail 200, sửa tạo revision 2, baseRevision cũ 409, MinistryAdmin và Teacher trường khác không đọc được 404, Teacher trường khác không thấy trong list, ẩn danh 401. Race đồng thời và import writer xem hai dòng kế tiếp |
| Ghi đồng thời trên SQL Server | Đạt trên SQL Server Express cục bộ (database tạm, đã xóa sau khi chạy, 2026-11-13), gọi `IQuestionWriter` qua DI, mỗi lệnh một scope/`DbContext` riêng, 8 luồng xuất phát cùng lúc, lặp 25 vòng. (a) 8 lệnh `CreateRevisionAsync` cùng `baseRevision` 1: mỗi vòng đúng 1 lệnh thành công, 7 lệnh `QuestionConflictException`, không có lỗi khác; `LatestRevision` = 2, đúng 2 revision và 8 option, không có dòng mồ côi. (b) 8 lệnh `CreateDraftAsync` cùng `(batchId, rowNumber)`: mỗi vòng đúng 1 dòng được ghi, 7 lệnh `QuestionConflictException`. Đây là script chạy tay ngoài repo, chưa là test tự động |
| Import writer | Đạt ở mức writer, chưa qua API. Trên SQL Server cục bộ (cùng ngày): retry một dòng đã ghi trả `QuestionConflictException` và dòng kế tiếp trên cùng `DbContext` vẫn ghi được, không trùng; ghi 3 dòng trong một transaction: rollback để lại 0 dòng, commit để lại 3 dòng `DRAFT`. **Chưa kiểm chứng qua API vì API chưa có route import**; luồng `POST /imports`, preview và commit thuộc M2-03 ([#12](https://github.com/Hungle2910/ai-exam-bank/issues/12)). Cần chạy lại bước này khi M2-03 nối writer |
| Staging | **Chưa kiểm chứng.** Chưa có môi trường staging chạy được: M2-02 mới có Terraform và test mô phỏng, chưa `plan`/`apply` thật ([M2-02](M2-02_LOCAL_PREPARATION.md)). Migration `QuestionBank` và API chưa được áp lên staging. Việc này chờ M2-02 ([#7](https://github.com/Hungle2910/ai-exam-bank/issues/7)) và pipeline deploy M2-04, không nằm trong phạm vi mã của PR này |
| UI nối API thật | Đã quay video thao tác trên trình duyệt với API thật và SQL Server cục bộ, dữ liệu giả tắt (`NEXT_PUBLIC_USE_MOCK=false`); người phụ trách báo đã quay xong ngày 2026-11-10, video đính kèm ở issue #9. Frontend unit test 10/10 qua; Next.js production build hoàn thành biên dịch, kiểm tra TypeScript và sinh static pages |
| Hai giáo viên hai bộ môn cùng một trường | Đạt trên SQL Server cục bộ (database `AiExamBankM4Smoke`, 2026-11-10), gọi API trực tiếp. Môn Toán gán cho bộ môn A, Vật lý gán cho bộ môn B. Catalog của mỗi người chỉ có môn của bộ môn mình. Giáo viên B: danh sách và tìm kiếm không thấy câu của A, xem 404, sửa 404, tạo vào môn Toán 403. Kiểm tra đối xứng cho giáo viên A với câu của B cũng đạt. Đây là script chạy tay, chưa là test trong repo |
| OpenAPI | [openapi/questions.json](openapi/questions.json), 7 route, có ví dụ request và lỗi (thông báo lấy từ server thật). Bản sinh từ code đã so JSON thành công giữa API trước và sau khi chuyển response sang record có kiểu: giống từng byte với dữ liệu thật trên 8 GET. Chưa chạy bộ lint OpenAPI, và chưa có kiểm tra tự động bắt spec lệch khỏi code |
| Bằng chứng AWS | Không áp dụng; M4-02 không gọi AI |

Còn lại sau PR này, và không chặn việc merge vào `dev`: (1) chạy lại import writer qua API khi M2-03 có route import; (2) áp migration và smoke API trên staging khi M2-02/M2-04 có môi trường; (3) đưa script ghi đồng thời thành test tích hợp có điều kiện nếu nhóm muốn giữ làm hồi quy. Cần chốt trước khi merge: taxonomy seed ở mục 5 vẫn cần owner xác nhận với tài liệu nguồn, vì đổi sau phải thêm migration. Bằng chứng ở bảng trên là kiểm thử cục bộ, không thay thế staging.
