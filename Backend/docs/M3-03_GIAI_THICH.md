# M3-03: Luồng duyệt câu hỏi hoạt động thế nào?

## 1. Giáo viên gửi duyệt

Trong ngân hàng câu hỏi, nút **Gửi duyệt** gọi `POST /api/v1/questions/{id}/submit-review`.
Server lấy người dùng từ phiên đăng nhập, tải câu hỏi và vai trò hiện tại từ DB. Chỉ giáo
viên sở hữu câu hỏi trong đúng bộ môn mới được gửi. Revision phải là bản nháp mới nhất.
Sau khi lưu, trạng thái là `PendingReview`, `ReviewVersion` tăng từ 1 lên 2.

## 2. Trưởng bộ môn xem và quyết định

`GET /api/v1/reviews/pending` chỉ trả câu hỏi thuộc bộ môn người duyệt phụ trách.
Server loại câu do chính họ sở hữu hoặc tạo revision; việc lọc diễn ra trước phân trang.
Người duyệt bấm **Duyệt** hoặc **Trả lại**. Trả lại phải có lý do. API kiểm tra lại quyền,
revision, trạng thái và version từ DB trước khi lưu.

## 3. Vì sao có ReviewVersion và ReviewDecision?

`ReviewVersion` là số chống quyết định dựa trên dữ liệu cũ. Nếu hai người cùng mở một câu
ở version 2, người lưu trước chuyển nó sang version 3. Người lưu sau nhận HTTP 409 và
giao diện tải lại hàng chờ. `ReviewDecision` lưu ai đã quyết định, khi nào, duyệt hay
trả lại và lý do. Trạng thái câu và bản ghi quyết định được lưu cùng một giao dịch DB.
Unique index trên `(QuestionId, Revision)` ngăn ghi hai quyết định cho cùng revision.

## 4. Đề thi chỉ dùng câu nào?

`ApprovedQuestionReader` của M4 chỉ trả revision có trạng thái `Approved`. Bản nháp,
câu đang chờ và câu bị trả lại không thể đi vào tập ứng viên tạo đề.

## 5. Những nơi nên đọc trong code

- `Backend/Modules/Review/Domain/ReviewWorkflow.cs`: quy tắc ai được gửi/duyệt.
- `Backend/Api/Endpoints/ReviewEndpoints.cs`: API, tải quyền thật và lưu quyết định.
- `Backend/Persistence/AiExamBank.Persistence/Questions/QuestionRevisionEntity.cs`: trạng thái, version.
- `Backend/Persistence/AiExamBank.Persistence/Questions/ReviewDecisionEntity.cs`: lịch sử quyết định.
- `Frontend/src/components/pages/QuestionsPage.tsx`: nút Gửi duyệt.
- `Frontend/src/components/pages/ReviewPage.tsx`: danh sách chờ và nút quyết định.

## 6. Còn phải xác nhận trước khi đánh dấu Done

Migration và API đã được thử trên LocalDB riêng ngày 16/11/2026. Test tạo câu nháp, gửi
duyệt, thấy hàng chờ, chặn giáo viên duyệt (403), trưởng bộ môn duyệt (200), chặn quyết
định cũ (409). Hai tài khoản trưởng bộ môn quyết định cùng một câu: một lần 200, một lần
409. DB thử đã được xóa sau test. Còn cần ghép với M1-03 để demo đề thi chỉ lấy câu đã
duyệt và đi qua giao diện bằng dữ liệu thật trước khi cả nhóm đánh dấu gate tuần 3 Done.

## 7. Bàn giao cho M4-03

Nhánh `feat/m4-03-version-history-and-quality` hiện có `QuestionImmutabilityGuard` chỉ
cho phép cập nhật `Status` của revision. Khi ghép M3-03, M4 cần cho phép thêm đúng hai
metadata của luồng duyệt: `ReviewVersion` và `SubmittedAtUtc`. Nội dung câu, đáp án,
nguồn gốc và các options vẫn bất biến. Trigger SQL của M4 hiện đã cho phép hai metadata
đó vì chỉ đối chiếu các cột nội dung; guard ở ứng dụng cần theo cùng quy tắc. Migration
M4 và M3 cùng sửa model snapshot, nên phải tạo/kiểm lại migration sau khi ghép nhánh.
