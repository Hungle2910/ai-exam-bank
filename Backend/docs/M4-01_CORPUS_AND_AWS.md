# M4-01: Corpus manifest và yêu cầu AWS cho RAG

**Status:** Proposed; chưa có bằng chứng AWS. Bedrock chưa được sử dụng và model access chưa được xác nhận. Tài liệu nguồn đã có nhưng chưa nằm trên máy của M4 nên **manifest ở mục 2 chưa điền**. Liên quan: [domain M4-01](M4-01_QUESTION_DOMAIN.md), [AWS strategy](AWS_STRATEGY.md), [M2-01](M2-01_deploy_network.md), M4-04 (S3 corpus, Knowledge Base, retrieval).

Phần này không chặn M4-02. M4-04 và các task sinh câu bằng AI cần bằng chứng thật trước khi bắt đầu; mock hoặc fake retriever không thay thế bằng chứng triển khai.

## 1. Blocker hiện tại

| # | Điều kiện | Người sở hữu | Bước tiếp theo |
|---|---|---|---|
| 1 | Chưa xác nhận Bedrock model access, embedding model và Knowledge Base ở Region dự kiến `ap-southeast-1` | Hữu Phước (tài khoản, hạ tầng), Minh Phúc (IAM) | Kiểm tra trên tài khoản đích: model nào được cấp quyền, quota, và Knowledge Base có khả dụng ở Region này không. Nếu không, đề xuất Region khác trước khi dựng hạ tầng |
| 2 | Tài liệu nguồn chưa có trên máy M4 | Thiên Phúc | Lấy bản tài liệu về máy riêng (không vào repo), điền manifest, tính hash, xác nhận giấy phép |
| 3 | Chưa chọn vector store và chưa có ước tính chi phí | Thiên Phúc, Hữu Phước | So sánh các lựa chọn ở mục 4 theo Region và cấu hình đã chọn; chỉ ghi số tiền sau khi có cấu hình |

Mỗi dòng đóng lại khi có bằng chứng đã che thông tin nhạy cảm (mục 5), hoặc khi chuyển thành quyết định ghi trong ADR.

## 2. Corpus manifest

Manifest chỉ chứa **metadata**. File nguồn nằm ở storage riêng (S3 private theo mục 3) và không commit vào repo. Mỗi dòng ứng với một bản của một tài liệu; cập nhật nội dung tạo `SourceId` mới hoặc tăng phiên bản.

| Cột | Ý nghĩa | Ghi chú |
|---|---|---|
| `SourceId` | ID ổn định của tài liệu (GUID) | Khớp `KnowledgeSources.Id` |
| `Title` | Tên tài liệu | |
| `Subject` | `MATH`, `PHYS` hoặc `CHEM` | Theo [taxonomy](M4-01_QUESTION_DOMAIN.md) |
| `Kind` | Giáo trình, sách giáo khoa, đề tham khảo, tài liệu giáo viên | |
| `Grade` | Khối, MVP là 12 | |
| `Publisher` / `Edition` | Nhà xuất bản và năm | Dùng để đối chiếu chương trình |
| `SourceVersion` | Phiên bản nội bộ, bắt đầu từ 1 | |
| `ContentHash` | SHA-256 của file | Phát hiện thay đổi nội dung |
| `Pages` | Số trang | |
| `LicenseStatus` | `PERMITTED`, `DENIED` hoặc `UNKNOWN` | Mặc định `UNKNOWN`; chỉ `PERMITTED` mới được ingest |
| `LicenseEvidence` | Tham chiếu tới nơi lưu giấy phép hoặc xác nhận, không đính kèm file | Ví dụ mã văn bản hoặc email đã lưu |
| `ApprovedBy` / `ApprovedAt` | Người xác nhận được dùng để ingest | Phải là người có thẩm quyền, không phải AI |
| `StorageKey` | Đường dẫn S3 theo mục 3 | Để trống đến khi upload |
| `IngestionStatus` | `NOT_STARTED`, `SYNCING`, `READY`, `FAILED` | Không ghi `READY` nếu chưa sync thành công |

Bảng điền (chưa có dữ liệu):

| SourceId | Title | Subject | Kind | Grade | Edition | LicenseStatus | ApprovedBy |
|---|---|---|---|---|---|---|---|
| _chưa điền_ | | | | 12 | | UNKNOWN | |

Cách tính hash trên máy có tài liệu (PowerShell): `Get-FileHash -Algorithm SHA256 <đường dẫn file>`.

Quy tắc:

- Không ingest tài liệu có `LicenseStatus` khác `PERMITTED`.
- Tài liệu chứa dữ liệu cá nhân (họ tên, email, số định danh) bị loại hoặc che trước khi upload.
- Mỗi tài liệu gắn đúng một môn; metadata môn dùng để lọc khi truy xuất.
- Thu hồi quyền dùng nguồn thì chuyển `LicenseStatus` sang `DENIED` và xóa khỏi KB ở lần sync kế tiếp; câu hỏi AI đã sinh vẫn cần đi qua review như mọi bản nháp.
- Citation chỉ chứng minh nguồn của đoạn trích, không cấp quyền đọc nguồn.

## 3. Yêu cầu S3

| Hạng mục | Yêu cầu |
|---|---|
| Bucket | Private, chặn public access, mã hóa phía server, không dùng chung với bucket import nếu M2 chọn tách |
| Cấu trúc key | `corpus/{subjectCode}/{sourceId}/v{sourceVersion}/{tên file}` |
| Metadata cho KB | Thuộc tính `subject`, `grade`, `sourceId`, `sourceVersion` đi kèm từng tài liệu để lọc khi truy xuất (cần xác minh cách khai báo metadata của Bedrock KB trên Region đã chọn) |
| Quyền | Role runtime chỉ đọc prefix cần dùng; role ingestion mới được ghi; không có quyền xóa rộng |
| Vòng đời | Giữ phiên bản cũ đến khi KB sync xong; có quy trình xóa khi thu hồi quyền |
| Truy cập từ app | Qua IAM role của instance, không lưu khóa dài hạn trong repo hay cấu hình |

## 4. Yêu cầu Bedrock và Knowledge Base

Danh sách cần xác nhận **trên tài khoản và Region thật**, không đoán trước khả dụng:

| Hạng mục | Cần xác nhận |
|---|---|
| Region | Có dùng được Bedrock Knowledge Base ở `ap-southeast-1` (M2-01 đang chọn Region này) |
| Embedding model | Tên model, được cấp quyền, hỗ trợ tiếng Việt, kích thước vector |
| Generation model | Tên model, quota, độ dài ngữ cảnh; chỉ dùng để tạo **bản nháp**, không auto-approve |
| Vector store | Chọn một trong các phương án KB hỗ trợ ở Region đã chọn (ví dụ OpenSearch Serverless, Aurora PostgreSQL với pgvector, S3 Vectors). Mỗi phương án có chi phí nền và quản trị khác nhau |
| Truy xuất | Hỗ trợ lọc theo metadata (môn, khối) và trả về citation kèm đoạn trích |
| Ingestion | Có trạng thái sync và log lỗi từng tài liệu để đưa vào `IngestionStatus` |
| IAM | Role cho KB đọc S3 và ghi vector store; role runtime có quyền gọi truy xuất và gọi model tối thiểu; tách role deploy, CI, runtime và KB |
| Mạng | Bedrock từ instance private qua NAT hoặc VPC endpoint theo quyết định egress của M2 |
| Guardrails | Phạm vi theo M3-04 (SHOULD), không phải điều kiện của proof |

**Chi phí:** chưa ước tính bằng tiền vì chưa có Region, model và vector store. Các khoản cần tính: chi phí nền của vector store, embedding khi ingest, token của model khi truy xuất và sinh, lưu trữ S3, và log CloudWatch. Ngưỡng USD 250 trong [M2-01](M2-01_deploy_network.md) chỉ cho hạ tầng và **không gồm** Bedrock, KB hay vector store; M4 phải lập bảng chi phí riêng theo mẫu của M2 và báo trước khi tạo tài nguyên có phí nền.

## 5. Kế hoạch proof retrieval nhỏ

Chạy khi blocker 1 và 2 được gỡ. Mục tiêu là xác nhận đường đi, không phải chất lượng.

1. Xác nhận tài khoản và Region bằng `aws sts get-caller-identity`; chỉ làm việc trên tài khoản đích đã được cho phép.
2. Kiểm tra model access cho embedding model và generation model; ghi tên model, Region và quota.
3. Upload 1-2 tài liệu nhỏ có `LicenseStatus = PERMITTED` theo cấu trúc key ở mục 3.
4. Tạo Knowledge Base với vector store đã chọn, chạy sync, ghi trạng thái và log lỗi nếu có.
5. Truy xuất với bộ lọc môn: ít nhất một truy vấn đúng môn trả đoạn kèm citation, và một truy vấn môn khác không trả nội dung của môn kia.
6. Ghi bằng chứng đã che account ID, ARN nội bộ và nội dung tài liệu: tên model, Region, request/response mẫu, thời gian sync, chi phí nền quan sát được.
7. Dọn tài nguyên có phí nền sau proof nếu chưa dùng ngay.

Kết quả đạt: Region, model, embedding và vector store khả dụng và có bằng chứng, hoặc blocker cụ thể ghi ở mục 1 với người sở hữu và bước tiếp theo.

## 6. Ảnh hưởng đến các task khác

| Task | Ảnh hưởng |
|---|---|
| M4-02 | Không bị chặn; không gọi AI |
| M4-04 | Cần manifest đã điền, KB và vector store |
| M4-05 / M1-04 | Cần retrieval live và model access để sinh bản nháp |
| M2-02 / M3-04 | Cung cấp IAM, egress và quyền model cho proof |

Trong lúc chờ, phát triển qua `IKnowledgeRetriever` với bản fake chỉ dùng cho test. Không ghi kết quả của fake vào bằng chứng AWS.
