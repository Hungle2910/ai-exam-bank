# M4-01: Question Bank & Editor Wireframe

**Status:** Proposed. Đây là wireframe đề xuất, chưa có UI nối API thật; [QuestionsPage.tsx](../../Frontend/src/components/pages/QuestionsPage.tsx) hiện chỉ dùng dữ liệu mẫu. Taxonomy, trạng thái và luật nội dung lấy từ [M4-01_QUESTION_DOMAIN.md](M4-01_QUESTION_DOMAIN.md). API là hợp đồng đề xuất cho M4-02.

## 1. Phạm vi màn hình

| Màn hình | Người dùng | Việc chính |
|---|---|---|
| Danh sách (bank) | Teacher, DepartmentHead | Lọc, tìm, phân trang; mở editor hoặc lịch sử |
| Editor (tạo/sửa) | Teacher | Soạn MCQ; lưu luôn tạo revision `DRAFT` |
| Lịch sử revision | Teacher, DepartmentHead | Xem các revision bất biến và trạng thái từng revision |

Gửi duyệt và duyệt/trả lại thuộc luồng Review của M3, không thuộc màn hình này. Người dùng chỉ thấy câu hỏi trong phạm vi được cấp; server kiểm quyền lại ở mọi thao tác.

## 2. Danh sách câu hỏi

```text
=====================================================================================================
|  Ngân hàng câu hỏi                                                       [Làm mới] [+ Thêm câu]   |
|  Câu hỏi trắc nghiệm khối 12 theo môn, chủ đề và độ khó                                           |
=====================================================================================================
|  [ Nháp 6 ] [ Chờ duyệt 3 ] [ Đã duyệt 41 ] [ Bị trả lại 2 ]       (bấm để lọc theo trạng thái)   |
|                                                                                                   |
|  Tìm: [ nội dung câu hỏi...        ]  Môn: [Toán v]  Chủ đề: [Tất cả v]  Độ khó: [Mọi mức v]      |
|                                                                                       52 câu hỏi  |
|  -------------------------------------------------------------------------------------------------
|  | Câu hỏi                          | Môn / Chủ đề            | Độ khó | Trạng thái | Cập nhật |  |
|  |----------------------------------|-------------------------|--------|------------|----------|  |
|  | Cho hàm số y = x^3 - 3x + 2 ...  | Toán / Ứng dụng đạo hàm | Dễ     | [Nháp]     | 17/10    | [Sửa][Lịch sử]
|  |   q-1001 · rev 2 · gv.nguyen.van.a                                                             |
|  | Thể tích khí lí tưởng ở ...      | Vật lý / Khí lí tưởng   | T.bình | [Đã duyệt] | 16/10    | [Sửa][Lịch sử]
|  |   q-1002 · rev 1 · gv.le.van.c                                                                 |
|  -------------------------------------------------------------------------------------------------
|                                          [< Trước]  Trang 1 / 3  [Sau >]       Mỗi trang: [20 v]  |
=====================================================================================================
```

- Bộ lọc gửi lên server: môn, chủ đề, độ khó, trạng thái, từ khóa. Chủ đề chỉ liệt kê các chủ đề của môn đã chọn.
- Danh sách môn và chủ đề lấy từ catalog của trường, không phải ô nhập tự do.
- Cột **Điểm** bỏ khỏi danh sách: điểm thuộc `BlueprintSlot`, không thuộc câu hỏi.
- Mỗi dòng hiển thị revision hiện tại và trạng thái của **revision đó**.
- "Sửa" trên câu `APPROVED` mở editor với ghi chú sẽ tạo revision mới; nội dung đã duyệt không đổi.

## 3. Editor

```text
+----------------------------------------------------------------------------------+
|  Sửa q-1001                                                                  [X]  |
|  Lưu sẽ tạo revision 3 ở trạng thái Nháp; revision cũ được giữ nguyên.            |
|----------------------------------------------------------------------------------|
|  ! Cần nhập đủ 4 phương án                                (lỗi tổng hợp, nếu có) |
|                                                                                  |
|  Nội dung *                                                                      |
|  [ Cho hàm số y = x^3 - 3x + 2. Đạo hàm của hàm số là?                        ]  |
|                                                                   312 / 4000     |
|  Phương án (chọn đáp án đúng) *                                                  |
|  (A)* [ 3x^2 - 3                                    ]  <- đáp án đúng            |
|  (B)  [ 3x^2 + 3                                    ]                            |
|  (C)  [ x^2 - 3                                     ]                            |
|  (D)  [ 3x - 3                                      ]  ! trùng phương án A       |
|                                                                                  |
|  Môn *         Chủ đề *                     Độ khó *        Nguồn (tùy chọn)     |
|  [ Toán   v]   [ Ứng dụng đạo hàm       v]  ( ) Dễ (o) TB   [ SGK Toán 12, tr.12 ]|
|                                             ( ) Khó                              |
|----------------------------------------------------------------------------------|
|                                       [ Hủy ]  [ Lưu nháp ]                      |
+----------------------------------------------------------------------------------+
```

- Chỉ có **Lưu nháp**. Nút "Gửi duyệt" nằm ở luồng Review (M3) và gọi riêng sau khi lưu; client không tự đặt trạng thái. Mock hiện đặt `REVIEW_REQUIRED` từ client và cần bỏ.
- Đổi môn thì xóa chủ đề đã chọn. Môn không đổi được sau khi câu hỏi đã tồn tại; editor khóa ô Môn khi sửa.
- Lỗi theo từng ô lấy từ `errors[].field` của server (`stem`, `options[2]`, `correctOption`, `difficulty`, `topicId`, `source`); validate phía client chỉ để phản hồi nhanh, server vẫn quyết định.
- Đếm ký tự theo giới hạn trong domain (nội dung 4.000, phương án 1.000, nguồn 2.000).
- Nội dung hiển thị dạng text, không render HTML.

## 4. Lịch sử revision

```text
+--------------------------------------------------------------+
|  Lịch sử revision                                        [X]  |
|  q-1001 · mỗi revision là bất biến                            |
|--------------------------------------------------------------|
|  (3)  Revision 3  [Hiện tại] [Nháp]                           |
|       gv.nguyen.van.a · 17/10/2026 08:12 · Nhập tay           |
|  (2)  Revision 2  [Chờ duyệt]                                 |
|       gv.nguyen.van.a · 16/10/2026 14:05 · Nhập tay           |
|  (1)  Revision 1  [Bị trả lại]                                |
|       gv.nguyen.van.a · 15/10/2026 09:30 · Import (dòng 7)    |
+--------------------------------------------------------------+
```

- Mỗi revision có trạng thái, người tạo, thời điểm và nguồn gốc (`Nhập tay`, `Import`, `AI nháp`).
- Mở một revision để xem nội dung chỉ đọc. Lý do trả lại hiển thị khi M3 cung cấp dữ liệu.

## 5. Trạng thái giao diện

| Tình huống | Hiển thị |
|---|---|
| Đang tải | Khung xương 4 dòng; giữ nguyên bộ lọc |
| Danh sách trống (chưa có câu) | "Chưa có câu hỏi nào" + nút **Thêm câu hỏi** |
| Không khớp bộ lọc | "Không có câu hỏi phù hợp" + nút **Xóa bộ lọc** |
| Lỗi mạng hoặc 5xx | Thông báo lỗi chung + **Thử lại**; không hiện dữ liệu cũ như thể mới |
| 401 | Chuyển về đăng nhập với thông báo phiên hết hạn |
| 403 | "Bạn không có quyền với nội dung này"; không tiết lộ nội dung |
| 404 | "Không tìm thấy câu hỏi" (kể cả khi nằm ngoài phạm vi) |
| 409 khi lưu | "Câu hỏi đã được sửa ở revision mới hơn" + **Tải lại**; giữ lại bản đang soạn để so sánh |
| 400 khi lưu | Lỗi tại từng ô theo `field`; không đóng editor |
| Đang lưu | Khóa nút Lưu, tránh gửi hai lần |

Dữ liệu mẫu chỉ được dùng trong development và phải có nhãn "Dữ liệu mẫu" như hiện nay; không dùng làm bằng chứng cho tích hợp thật.

## 6. API mà UI dùng (đề xuất, thuộc M4-02)

| Hành động | Request | Ghi chú |
|---|---|---|
| Danh sách | `GET /api/v1/questions?subjectId&topicId&difficulty&status&q&page&pageSize` | `pageSize` mặc định 20, tối đa 100; sắp xếp ổn định |
| Chi tiết | `GET /api/v1/questions/{id}` | Trả revision mới nhất và danh sách revision |
| Tạo | `POST /api/v1/questions` | Luôn `DRAFT`; không nhận `status`, `schoolId`, `departmentId` từ client |
| Sửa | `PUT /api/v1/questions/{id}` + `baseRevision` | Tạo revision mới; 409 nếu `baseRevision` không còn là mới nhất |
| Catalog | `GET /api/v1/catalog/subjects`, `.../subjects/{id}/topics` | Chỉ môn và chủ đề của trường mà người dùng được phép |

Mutation cần antiforgery token theo [ADR 0006](adr/0006-identity-session-and-authorization.md).

## 7. Thay đổi cần làm trên mock hiện tại

| Mock hiện tại | Cần đổi |
|---|---|
| Trường `points` trên câu hỏi, cột Điểm, ô nhập Điểm | Bỏ; điểm thuộc blueprint |
| Môn và Chủ đề là ô nhập tự do | Dropdown theo catalog; chủ đề phụ thuộc môn |
| Chủ đề mẫu Logarit, Dao động cơ, Bảng tuần hoàn | Thay theo taxonomy đã chốt ([mục 3](M4-01_QUESTION_DOMAIN.md)) |
| Nút "Gửi duyệt" đặt `REVIEW_REQUIRED` từ client | Bỏ khỏi editor; gửi duyệt qua Review (M3) |
| Nút xóa với câu `DRAFT` | Chờ quyết định (mục 8) |
| Lọc phía client trên toàn bộ mảng | Lọc và phân trang phía server |
| Lịch sử chỉ liệt kê số revision | Thêm trạng thái, nguồn gốc, thời điểm từng revision |
| Môn sửa được khi sửa câu | Khóa ô Môn sau khi tạo |

## 8. Câu hỏi còn mở cho wireframe

| # | Câu hỏi | Đề xuất |
|---|---|---|
| 1 | Có cho **xóa nháp** không? Revision là bất biến và Review có audit | Bỏ khỏi MVP; thêm "bỏ nháp" sau nếu thực sự cần |
| 2 | DepartmentHead có sửa được câu của Teacher không? | Không; chỉ xem và duyệt, theo [phân quyền](IDENTITY_AND_PERMISSIONS.md) |
| 3 | Từ khóa tìm kiếm quét những trường nào? | Chỉ nội dung câu hỏi và mã câu hỏi ở MVP |
