# Thiết kế solution .NET cho AI Exam Bank

**Trạng thái:** Bản thiết kế mục tiêu để M1–M5 review; chưa phải tuyên bố đã triển khai. **Owner:** M1 / Le Doan Gia Hung. **Cập nhật:** 09/10/2026. [ADR 0002](adr/0002-dotnet-module-boundaries.md) giải thích lựa chọn modular monolith; [ADR 0003](adr/0003-data-identity-runtime-baseline.md) ghi các mặc định kỹ thuật cần team phê duyệt.

## 1. Phạm vi và trạng thái thật

MVP là **một trường/tổ chức**, câu hỏi MCQ một đáp án, nhập CSV, duyệt nội dung bởi người có quyền, ma trận đề, chọn câu đã duyệt, AI chỉ tạo bản nháp có nguồn, đề cuối bất biến và job bền vững. Lex, SageMaker, VPN và quy trình nhiều trường/cấp Bộ là phần mở rộng có cổng quyết định riêng; [PR #58](https://github.com/Hungle2910/ai-exam-bank/pull/58) chưa thay đổi phạm vi MVP cho đến khi team chốt.

Trên `dev` hiện có .NET 10 API host, `GET /health/live`, HTTP Problem Details, build và smoke CI. Chưa có module nghiệp vụ, database, Worker, frontend, IaC hay triển khai AWS. Mọi đường dẫn, entity và interface bên dưới là **hợp đồng thiết kế**; project được tạo cùng hành vi đầu tiên và test của nó, không tạo skeleton rỗng.

| Quyết định thiết kế | Mặc định đề xuất | Lý do và điểm cần chốt |
|---|---|---|
| Kiến trúc | Một modular monolith; API và Worker là hai process dùng cùng module contracts và một relational DB | Giữ approval + audit trong một transaction, phù hợp 5 người/10 tuần; M1–M5 duyệt ADR 0002 |
| Dữ liệu | PostgreSQL + EF Core, một DbContext/migration stream | Phù hợp dữ liệu quan hệ và optimistic concurrency; M1/M2 chốt engine, hosting EC2 hay RDS và chi phí trước migration đầu tiên |
| Identity | ASP.NET Core Identity với session cookie HttpOnly/Secure/SameSite cho Web/API cùng origin | Không tự viết password/crypto; M3 chốt user provisioning, session expiry, CSRF và resource scope trước endpoint có quyền |
| Web | Một Web TypeScript cùng origin với API; React là mặc định đề xuất nếu team không có frontend hiện hữu | M1/M3 chốt framework và cách build/deploy trong ADR trước project Web đầu tiên |
| API | `/api/v1`, JSON, RFC Problem Details + `traceId` + `code` ổn định | DTO và HTTP status được chốt theo use case; OpenAPI sinh từ endpoint thật |
| Async | Job/Attempt lưu DB, lease có hạn, retry hữu hạn và idempotency | M5 chốt lease/recovery trước Worker đầu tiên; API không giữ HTTP request cho Bedrock/import dài |
| AWS | HTTPS entry công khai; API/Worker/DB private, S3/Bedrock qua role, quản trị EC2 bằng SSM | M2 chốt Region, entry, egress, DB hosting, budget và IaC; không tự tạo tài nguyên tính phí từ tài liệu này |

## 2. Cây solution và chiều phụ thuộc

```text
AiExamBank.slnx
global.json                       SDK feature band đã pin
Directory.Build.props             net10.0, nullable, warnings-as-errors
src/
  Api/                             ✓ HTTP host, DI, auth middleware, route groups
  Modules/
    Identity/                      + M3: users, roles, sessions, scope
    Questions/                     + M4: question identity, revision, taxonomy
    Review/                        + M3: decision, audit, state transitions
    Exams/                         + M1: blueprint, selection, gap report, snapshot
    Import/                        + M2: upload, validation, preview, commit
    Knowledge/                     + M4: source lifecycle, citations, RAG draft
    Jobs/                          + M5: durable job, attempt, lease, retry
  Persistence/                     + one EF DbContext, mapping by module, migrations
  Worker/                          + process host when first durable handler exists
  Web/                             + frontend after framework/auth decision
tests/
  repository/                      ✓ repository checks
  smoke/                           ✓ API liveness and 404 Problem Details
  Modules/<Module>.Tests/          + domain/use-case behavior
  Integration/                     + DB/API/auth/transaction tests
  EndToEnd/                        + browser and AWS staging journeys
infrastructure/                   + IaC when M2 network/hosting ADR is accepted
```

`✓` đã có; `+` là target, chưa có code. Mỗi module bắt đầu bằng **một .NET project** khi có use case chạy được. Bên trong chỉ thêm `Domain/`, `Application/`, `Contracts/`, `Infrastructure/`, `Endpoints/` khi có code thật. EF mappings/repositories nằm trong `Persistence/<Module>/`; S3/Bedrock adapters nằm sau interface do module sử dụng sở hữu. Tách adapter thành project riêng chỉ khi một dependency/provider bắt đầu làm bẩn domain hoặc gây khó test.

```text
Web --HTTPS--> Api ---> Module application contracts ---> Module domain
                  \------> Persistence ---> one relational DB
Worker ----------> Jobs + business handlers ----> Persistence
Module consumer --> provider-owned public contract (one-way only)
Module adapter ----------------------------------> AWS SDK / external service
```

- `Api` và `Worker` là composition roots: đăng ký DI, middleware, route groups, hosted service; không chứa quy tắc chọn câu, duyệt hoặc retry. Worker không được tham chiếu từ API.
- `Persistence` tham chiếu các module để map entity và triển khai repository; module không tham chiếu `Persistence`. Host đăng ký cả module và adapter. Domain không phụ thuộc ASP.NET, EF Core, AWS SDK hay HTTP DTO.
- Consumer chỉ dùng public contract của provider; không đọc bảng, `DbContext` hoặc entity nội bộ của module khác. Nếu hai chiều tham chiếu xuất hiện, M1 và hai owner rút contract nhỏ ra hoặc đảo chiều phụ thuộc trước merge.
- Một DbContext/migration stream có owner điều phối là M1; từng owner chịu trách nhiệm mapping/query của mình. Hai PR sửa cùng schema phải thống nhất thứ tự migration và chạy upgrade từ DB sạch lẫn DB phiên bản trước.

## 3. Quyền sở hữu module và hợp đồng tối thiểu

| Module / owner | Sở hữu dữ liệu và hành vi | Public contract cần cho module khác | Không được làm |
|---|---|---|---|
| Identity / M3 | User, role, session, resource assignment | Actor/scope lookup, permission decision | Dựa vào role trong UI hoặc IAM để thay kiểm quyền server |
| Questions / M4 | Question, immutable QuestionRevision, taxonomy, approved read model | Revision ref/version, approved-only reader, revision writer | Cho module khác ghi thẳng bảng Questions |
| Review / M3 | ReviewDecision, audit, transition | Submit/approve/reject có expected version và reason | Tự approve revision của mình hoặc duyệt qua event không atomic |
| Exams / M1 | Blueprint, Slot, selection, ExamSnapshot | Generate/preview/finalize và immutable snapshot read | Chọn draft/unapproved revision hoặc sửa snapshot đã finalize |
| Import / M2 | ImportBatch/Row, CSV validation/result | Upload/preview/commit idempotent; gọi Questions writer | Bỏ qua review và publish trực tiếp |
| Knowledge / M4 | Source/version/access scope/citation, AI draft adapter | Retrieve/generate draft kèm nguồn và metadata | Coi citation của model là quyền truy cập hoặc auto-approve |
| Jobs / M5 | Job/Attempt, lease, retry, failure status | Schedule/status/cancel/handler contract | Chạy tác vụ dài trong HTTP request hoặc retry vô hạn |

Các contract đầu tiên cần chốt bằng ví dụ request/response và test: `ActorContext` (user ID, role, school/resource scope), `QuestionRevisionRef` (question ID, revision ID, version, status), `ApprovedQuestionRead`, `ReviewDecisionCommand` (actor, revision, expected version, reason), `BlueprintSlot` (taxonomy/count/points), `JobRequest` (kind, actor/scope, idempotency/correlation). Interface ứng dụng phải trả lỗi có nghĩa nghiệp vụ, không trả EF entity hoặc AWS SDK DTO. M3/M4/M5 cùng M1 ký duyệt các contract dùng chung trước khi module consumer triển khai.

## 4. Dữ liệu, trạng thái và transaction

| Aggregate | Khóa/quy tắc tối thiểu | Invariant phải test |
|---|---|---|
| Question + Revision | Question ID ổn định; Revision ID/version riêng; content/options/answer/provenance của revision đã approved bất biến | Edit approved tạo revision DRAFT mới; một MCQ có đúng một đáp án hợp lệ |
| ReviewDecision + Audit | Decision gắn đúng revision/version, actor, reason, timestamp | Reviewer có scope và không tự duyệt; stale version trả 409; transition + audit cùng transaction |
| Blueprint + Slot | Slot có subject/topic/difficulty/count/points; version khi sửa | Count/score hợp lệ; selection không lặp Question ID và chỉ lấy revision APPROVED đúng scope |
| ExamSnapshot | Pin revision ID, version và nội dung đã chọn tại finalize | Finalize recheck toàn bộ slot, quyền, uniqueness; snapshot không đổi khi nguồn sửa |
| ImportBatch + Row | Unique (batch ID, row number); key commit và kết quả được lưu | Cùng key trả kết quả cũ; batch đã commit với key khác trả 409; không tạo QuestionRevision trùng |
| Job + Attempt | Job ID, status, lease owner/expiry, attempt count, idempotency key | Hai Worker không cùng commit effect; crash/restart thu hồi lease; retry bounded |
| KnowledgeSource + Citation | Source version/hash/location/scope; citation gắn draft/revision | Thu hồi nguồn hoặc scope không được lộ nội dung; draft thiếu nguồn không tự publish |

**Luồng duyệt:** Questions tạo revision DRAFT → Review submit thành REVIEW_REQUIRED → M3 kiểm quyền, scope, self-review và expected version → trong một DB transaction đổi trạng thái đúng revision, ghi decision và audit → commit; lỗi nào cũng rollback cả ba. REJECTED chỉ sửa bằng revision DRAFT mới. Không dùng HTTP/event giữa các ghi cần atomic.

**Luồng tạo đề:** M1 lưu blueprint → đọc approved revisions qua contract M4 → chọn đủ số lượng/điểm, không trùng Question ID → trả gap report nếu thiếu → M4/M5 có thể tạo AI DRAFT theo job → người có quyền review → M1 revalidate và finalize thành snapshot bất biến. Không gọi trạng thái `COMPLETE` nếu còn slot thiếu hoặc item chưa duyệt.

**Luồng import:** M2 upload vào batch, validate từng dòng và preview; commit chỉ sau hành động xác nhận, kiểm lại actor/scope, header `Idempotency-Key`, trạng thái batch và expected version. Batch transition, row mapping và QuestionRevision mới phải commit atomic. Retry cùng key trả kết quả lưu; key khác sau commit trả 409. Các lỗi từng dòng được báo rõ, không làm nhập âm thầm nội dung sai.

## 5. HTTP API, identity và frontend

Đặt route nghiệp vụ dưới `/api/v1`; `/health/live` chỉ chứng minh process sống. `/health/ready` được thêm khi có DB/Worker thực và có timeout ngắn; không dùng Bedrock call tính phí làm health probe. Endpoint admin health cần auth. Mỗi route group khai báo chính sách quyền hoặc lý do public; không dựa vào việc Web ẩn nút.

| Nhóm route đề xuất | Owner | Quyền và failure chính |
|---|---|---|
| `/auth/*`, `/admin/users/*` | M3 | login/logout/session, user-role/scope; 401/403, rate limit và audit thay đổi quyền |
| `/questions/*`, `/reviews/*` | M4/M3 | resource scope, DRAFT/revision/version; 403 và 409 cho self-review/stale state |
| `/imports/*` | M2 | upload/preview/commit/result; CSV size/type/row limits, key bắt buộc, 409 duplicate state |
| `/blueprints/*`, `/exams/*` | M1 | approved-only selection, gap report, preview/finalize; 409 khi version/slot đổi |
| `/jobs/*` | M5 | chỉ owner/authorized admin xem; bounded retry, terminal failure/cancel |
| `/admin/infrastructure/health` | M2 | admin only; không lộ secret/network detail nhạy cảm |

HTTP error dùng Problem Details với `status`, `traceId`, `code` ổn định và validation details an toàn: 400 input, 401 thiếu/expired session, 403 không đủ quyền/scope, 404 không tồn tại hoặc không được tiết lộ, 409 version/state conflict, 429 throttling, 500 lỗi không dự kiến. Có pagination/filter bounded cho list/search. OpenAPI mô tả endpoint thật và negative examples; khi contract đổi, consumer test và tài liệu cùng PR.

MVP role nền là Teacher, Reviewer và Admin **trong một trường**. Quyền thực thi phụ thuộc role **và** resource scope/ownership/state; reviewer không tự duyệt nội dung mình tạo. ASP.NET Core Identity là mặc định đề xuất, với password hashing thư viện, cookie Secure/HttpOnly/SameSite, CSRF protection cho mutation, session expiry/logout và audit user-role changes. Nếu team chọn OIDC hoặc đa trường/cấp Bộ, cần ADR và permission matrix mới trước khi đổi code; không tự mở rộng quyền Admin bằng suy đoán từ [PR #58](https://github.com/Hungle2910/ai-exam-bank/pull/58).

Web cùng origin gọi API, hiển thị rõ pending/failed/partial, không giữ secret/AWS credential hoặc answer key trong client storage. Teacher upload/soạn, Reviewer duyệt có lý do, Admin quản quyền/health; UI chỉ là lớp trình bày, server kiểm lại toàn bộ mutation.

## 6. Worker, AI và AWS boundary

API ghi `Job` vào DB trong cùng transaction với yêu cầu nghiệp vụ, trả `202 Accepted` + job URL khi việc chạy nền thật sự cần thiết. Worker claim bằng thao tác atomic với lease expiry, ghi Attempt/correlation, tạo DI scope riêng, truyền cancellation, giới hạn timeout/retry/backoff. Side effect phải có idempotency key ở DB và provider boundary; sau crash Worker có thể chạy lại nhưng không tạo câu hỏi/đề/job trùng. Terminal failure có lý do an toàn và thao tác retry có quyền, không mất job im lặng.

Bedrock adapter của Knowledge chỉ trả bản nháp đã qua schema validation, source/citation version và kiểm tra scope; không cho AI chuyển sang APPROVED. M1 orchestration dùng gap report để yêu cầu thêm câu, chờ job xong rồi revalidate trước finalize. Lex và SageMaker là feature flag tắt mặc định, không chặn luồng core hoặc bypass .NET authorization.

M2 thiết kế HTTPS entry công khai → API/Worker private → DB private. S3/Bedrock/CloudWatch/SSM dùng runtime role với quyền hẹp; secret qua SSM/Secrets Manager, không commit credential. Nếu chọn RDS, DB subnet group có ít nhất hai subnet ở hai AZ. NAT default route và VPC endpoint routes/ENIs là hai cơ chế riêng; chọn egress bằng connectivity test và cost sheet. App/Worker có image/artifact version, config validation, structured logs, trace/job IDs, retention và alert delivery proof; log không chứa token, đáp án, toàn bộ prompt hoặc PII mặc định.

Môi trường AWS development/staging/production là các deployment có config và quyền riêng, **không đồng nghĩa** với Git branch `dev`/`main`. M2 chỉ provision sau khi Region, budget, IAM, DB hosting, backup/restore, rollback và teardown được review; release lưu exact tested `dev` SHA, migration và evidence. `main` chỉ nhận PR promotion đã kiểm thử, không deploy chỉ vì tài liệu mô tả.

## 7. Kiểm thử và cổng CI

| Cấp kiểm thử | Bằng chứng tối thiểu | Owner |
|---|---|---|
| Foundation hiện có | Release build, liveness HTTP và 404 Problem Details | M1 |
| Module behavior | Domain constraints, permission denial, wrong state/version, idempotency; assert output observable | Owner module |
| DB/API integration | Migration DB sạch + upgrade, CRUD, 401/403/409, approval/audit atomic, concurrent review/finalize, import commit race | M1/M3/M4/M2 |
| Worker recovery | Hai Worker claim cùng job, crash sau DB write, lease expiry, provider timeout, retry ceiling | M5 + handler owner |
| Web/E2E | Teacher → draft/import → reviewer → blueprint → AI draft → final snapshot; negative auth/source-scope paths | M1–M5 |
| AWS staging | Private reachability, SSM, IAM least privilege, backup/restore, alarm delivery, cost/teardown, rollback trên exact SHA | M2 + all owners |

Mỗi PR code thêm test có ý nghĩa cho hành vi mới và mở rộng CI cùng project thật: restore locked → build warnings-as-errors → module/integration tests → smoke → migration verification → artifact/deploy checks. `dev` hiện bắt buộc `Repository quality`, `Application build and smoke`, `CodeRabbit` và resolved conversations; tác giả tự merge sau khi bot thật sự review. Promotion `dev → main` cần human approval độc lập và kiểm SHA/CI. Check xanh của foundation **không chứng minh** product logic, AWS deployment hoặc readiness.

## 8. Thứ tự triển khai và điều kiện hoàn thành thiết kế

| Bước | Owner chính | Đầu ra để bước sau bắt đầu |
|---|---|---|
| 1. Chốt hợp đồng nền | M1 cùng M2–M5 | ADR 0002/0003; DB, identity, API/error, role/scope và module contracts có reviewer/issue; quyết định scope PR #58 |
| 2. Dữ liệu + duyệt | M4 Questions, M3 Identity/Review; M1 migration coordinator | Module code, DB migration, atomic approval/audit, API, 403/409/concurrency tests |
| 3. Ma trận + import | M1 Exams, M2 Import | Approved-only selection, gap report, snapshot bất biến, upload/preview/commit idempotent, tests |
| 4. Job + AI draft | M5 Jobs/Worker, M4 Knowledge, M1 orchestration | Durable recovery/idempotency, source-scoped draft, human review, failure UI |
| 5. Web + AWS release | M1–M5, M2 vận hành | E2E staging, IaC, IAM/SSM, migration/restore, alert/cost/rollback evidence trên exact commit |

Thiết kế được **team chấp nhận** khi M1–M5 ký duyệt ownership, public contracts, DB/identity/frontend/hosting decisions và permission matrix; ADR chuyển từ Proposed sang Accepted, các issue tương ứng được liên kết. Solution được **triển khai hoàn chỉnh** chỉ khi các hành vi ở bước 2–5 có code, test, CI và AWS evidence; không đóng issue hoặc tự nhận production-ready vì cây thư mục hay tài liệu đã đủ.
