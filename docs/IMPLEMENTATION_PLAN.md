# AI Exam Bank — Phân công triển khai cho đội 5 người

Ngày lập: 04/10/2026. Member mapping: M1 Gia Hưng; M2 Hữu Phước; M3 Minh Phúc; M4 Thiên Phúc; M5 Gia Bảo.

**Cập nhật phạm vi 09/10/2026:** Project owner đã mở rộng MVP sang nhiều trường và kỳ thi cấp Bộ ([ADR 0004](adr/0004-multi-school-ministry-mvp.md)). Các gói việc, effort 926 giờ và demo flow bên dưới là **baseline lịch sử trước khi mở rộng**, không phải estimate/DoD đã đủ cho phạm vi mới. M1–M5 phải bổ sung và ước lượng việc về phân tách dữ liệu trường, gán quyền theo bộ môn/kỳ thi, hai người xác nhận đề cấp Bộ, migration, kiểm thử xuyên trường và chi phí AWS trước khi chốt lại milestone. [Permission matrix](IDENTITY_AND_PERMISSIONS.md) là nguồn sự thật cho role mới.

## 1. Executive Repository Assessment

**Đây là kế hoạch triển khai đề xuất, chưa phải audit mã nguồn.** Scope bắt nguồn từ master prompt AI Exam Bank do project owner cung cấp. Repository mới được bootstrap với tài liệu và backlog; chưa có runtime implementation để xác minh completion. Bản Markdown nguồn tham chiếu trong master prompt chưa được cung cấp. Tên/path/API trong tài liệu là proposed design, không phải evidence đã triển khai.

Kế hoạch dùng phạm vi trong master prompt: .NET modular monolith + background workers; ngân hàng câu hỏi; ma trận đề; chọn câu đã duyệt; phát hiện ô thiếu; Bedrock RAG tạo bản nháp; người duyệt phê duyệt; preview đề cuối; RBAC, audit, AWS, IaC, monitoring và xử lý lỗi. Lex, SageMaker và VPN có cổng điều kiện riêng, không chặn MVP.

**Giả định lập kế hoạch:** mỗi người có khoảng 20–24 giờ/tuần dành cho project trong 10 tuần, đã biết làm fullstack cơ bản. Tổng effort dự kiến 926 giờ, đã bao gồm xây dựng, test, review và tài liệu; cần khoảng 10–15% dung lượng trống ngoài estimate để xử lý phát sinh. Nếu chỉ có 10 giờ/người/tuần, phải kéo dài lịch hoặc giảm phần SHOULD/STRETCH và độ rộng demo, không bỏ yêu cầu cốt lõi.

Chốt tuần bắt đầu sau khi đội duyệt kế hoạch; “Tuần 1” là tuần dự án, chưa gán ngày lịch cụ thể. Nếu có mã nguồn đang làm, audit trước rồi bỏ các task đã đạt DoD; không bắt đội viết lại chức năng đã chạy.

**Các quyết định cần chốt tại tuần 1:** framework frontend theo repository hiện có; DB engine tương thích EF Core; một công cụ IaC; AWS Region/model/embedding/vector store; hạn mức chi phí; mô hình hosting DB; tài liệu kiến thức được phép dùng; rubric độ khó câu hỏi. Không coi lựa chọn công nghệ chưa xác nhận là blocker của việc lập kế hoạch.

Kiến trúc đề xuất cho demo: trình duyệt → public HTTPS entry/reverse proxy → .NET API và worker trong private subnet → DB private; API gọi S3/Bedrock bằng IAM role. Public entry có thể là reverse proxy trên EC2 cho demo hoặc ALB nếu có lý do và ngân sách; chọn một phương án tại tuần 1. Không public trực tiếp cổng ứng dụng/DB. Có backup/restore nếu DB chạy EC2; RDS chỉ thêm khi nhu cầu quản trị DB và ngân sách phù hợp. Không thêm Kubernetes hoặc tách microservices.

Các tên bảng, API, namespace và thư mục trong tài liệu này đều là **thiết kế dự kiến**, phải đối chiếu repository trước triển khai. Chúng không phải bằng chứng chức năng đã tồn tại.

## 2. Overall Completion %

**Tiến độ thực tế: N/A — chưa đủ bằng chứng.** Không ghi 0%, DONE, PARTIAL hoặc MISSING cho chức năng chưa kiểm tra.

Khi nhận repository, tính tiến độ theo trọng số effort của backlog MUST:

`Completion_MVP = Σ(effort task MUST × mức đạt DoD) / Σ(effort task MUST) × 100%`.

- Mức đạt 0: chưa có đầu ra được xác minh.
- Mức đạt 0,5: đã có triển khai chạy được và có bằng chứng nhưng còn thiếu test/tích hợp hoặc tiêu chí DoD.
- Mức đạt 1: toàn bộ DoD đạt, người review xác nhận, có bằng chứng test/tích hợp.
- Task bị BLOCKED vẫn nằm trong mẫu số. Không tính số dòng code, số commit hoặc số AWS service thành tiến độ.
- Báo cáo completion MVP và completion extension riêng; không dùng Lex/ML để làm tăng completion của MVP.

## 3. Completion by Member

| Member | Tiến độ xác minh | Effort 10 tuần | Tải trung bình | Nhận xét dự kiến |
|---|---|---:|---:|---|
| Member 1 | N/A | 184 giờ | 18,4 giờ/tuần | Đường găng business logic; cần khả năng thiết kế và tích hợp mạnh |
| Member 2 | N/A | 186 giờ | 18,6 giờ/tuần | Hạ tầng + import fullstack; cần thời gian AWS access từ đầu |
| Member 3 | N/A | 180 giờ | 18 giờ/tuần | Security và approval là điều kiện hoàn thành MVP |
| Member 4 | N/A | 196 giờ | 19,6 giờ/tuần | Tải cao nhất, tuần 4–5 đạt 24 giờ; Lex chỉ làm khi có dung lượng |
| Member 5 | N/A | 180 giờ | 18 giờ/tuần | Reliability bắt đầu sớm; ML thực hiện sau cổng MVP |
| Tổng | N/A | **926 giờ** | **92,6 giờ/đội/tuần** | Không có cơ sở xác nhận mức hoàn thành hay năng lực người cụ thể |

Completion từng member dùng cùng công thức trên với task MUST do người đó sở hữu. Effort extension được theo dõi riêng. Không cộng effort reviewer vào completion của owner lần thứ hai.

## 4. Requirement Status Matrix

Trong bảng này, **BLOCKED-AUDIT** nghĩa là việc xác minh bị thiếu repository/file kế hoạch gốc, không có nghĩa tính năng bị lỗi hoặc chưa làm. Owner hiện tại là vai trò ghi trong master prompt, không phải người được xác minh qua Git. Mọi dòng đều chưa có file/class/endpoint thực tế làm bằng chứng. Khi có repo, thay trạng thái bằng DONE/PARTIAL/MISSING/DUPLICATE/BLOCKED/MISASSIGNED và ghi đường dẫn, class/API/workflow cụ thể.

| Requirement | Trạng thái / bằng chứng hiện có | Owner trong prompt → đề xuất | Phần cần kiểm tra/triển khai | Phụ thuộc và rủi ro | Hành động / task |
|---|---|---|---|---|---|
| Kiến trúc, shared contracts, modular monolith | BLOCKED-AUDIT; chỉ có mô tả yêu cầu | Backend Lead → M1 | Module boundaries, DTO, migrations, error contract | Schema không thống nhất | M1-01, M1-02 |
| Authentication | BLOCKED-AUDIT; chưa có code auth | Security → M3 | Login, session/token, logout, user management | Identity chưa chọn | M3-01, M3-02 |
| Authorization/RBAC | BLOCKED-AUDIT; chưa có policy | Security → M3 | Kiểm quyền API và UI, sở hữu tài nguyên | Auth; IDOR, role stale | M3-02, M3-07 |
| Question Bank + metadata + search/filter | BLOCKED-AUDIT; chưa có models/routes | AI Knowledge → M4 | CRUD, subject/topic/difficulty, paging/index | Schema; dữ liệu thiếu | M4-01, M4-02 |
| Question versioning | BLOCKED-AUDIT; chưa có migrations | AI Knowledge → M4 | Revision bất biến, version history, concurrency | CRUD; sửa nội dung đã duyệt | M4-03 |
| Import | BLOCKED-AUDIT; chưa có parser/job | AI Knowledge → M2 | CSV validation, preview, commit, row errors | Domain API M4, jobs M5 | M2-03, M2-04 |
| Blueprint / ma trận đề | BLOCKED-AUDIT; chưa có engine | Backend Lead → M1 | Slot constraints, CRUD, UI ma trận | Taxonomy M4 | M1-02 |
| Generate từ câu approved + missing slots | BLOCKED-AUDIT; chưa có thuật toán | Backend Lead → M1 | Count/score, không trùng, gap report | Version/review states | M1-03 |
| S3 + Bedrock Knowledge Base + RAG | BLOCKED-AUDIT; chưa có AWS config | AI Knowledge → M4 | Ingestion, retrieval, embeddings/vector store, citations | IAM, region, tài liệu nguồn | M4-04, M4-05 |
| Bedrock application orchestration | BLOCKED-AUDIT; chưa có worker | Backend Lead → M1 | Dispatch missing slots, validate output, rebuild exam | RAG M4; jobs M5 | M1-04, M1-05 |
| AI chỉ DRAFT/REVIEW_REQUIRED | BLOCKED-AUDIT; chưa có transitions | AI + Security → M4/M3 | DB/API cưỡng chế trạng thái, AI không approve | State machine; vượt quyền | M3-03, M3-04, M4-05 |
| Human review / approval + AI Review UI | BLOCKED-AUDIT; chưa có review module | Security/AI → M3 chính, M4 evidence | Pending list, citations, reject/approve, history | Auth, revision, audit | M3-03, M3-05, M3-06 |
| Final exam preview | BLOCKED-AUDIT; chưa có snapshot | Backend Lead → M1 | Version-pinned items, snapshot, đủ ma trận | Approval; thay đổi câu nguồn | M1-06 |
| Audit trail | BLOCKED-AUDIT; chưa có audit store | Security → M3 | Actor/action/resource/revision/time/request ID | Auth; audit mất khi mutation | M3-05 |
| Job/failure handling/reliability UI | BLOCKED-AUDIT; chưa có jobs | ML Reliability → M5 | State, bounded retry, lease, idempotency | DB/worker; duplicate effect | M5-01 đến M5-06 |
| Monitoring + SNS | BLOCKED-AUDIT; chưa có metrics/alarms | DevOps/ML → M2 infra, M5 app | Metrics, log retention, alarm, notifications | AWS; thiếu signal/quá nhiều alert | M2-06, M5-05 |
| AWS networking/EC2/IAM/SSM | BLOCKED-AUDIT; chưa có IaC | DevOps/Security → M2/M3 | Private hosting, access, egress, SG/NACL | Account/region/budget | M2-01, M2-02, M3-04 |
| IaC + CI/CD + deploy | BLOCKED-AUDIT; chưa có workflows | DevOps → M2 | Reproducible provision, build/test/release/rollback | Artifact, migration strategy | M2-02, M2-04, M2-05 |
| Tests + docs + demo | BLOCKED-AUDIT; chưa có test reports | Tất cả → mỗi owner | Unit, integration, E2E, runbook, evidence | Core flow ổn định | Tuần 6, 9, 10 |
| Lex V2 | BLOCKED-AUDIT; chưa có bot/adapter | AI Knowledge → M4 | Intent/slots → .NET validation → blueprint | MVP gate; locale/quota | M4-08, SHOULD |
| SageMaker AI | BLOCKED-AUDIT; chưa có dataset/model | ML Reliability → M5 | Labels, train/eval, endpoint, inference adapter | Dataset/ML budget/MVP gate | M5-07, M5-08, SHOULD |
| Flow Logs / Reachability Analyzer | BLOCKED-AUDIT; chưa có network evidence | DevOps → M2 | Flow diagnostics + phân tích đường đi | VPC/IAM; chi phí log/analysis | M2-06, M2-07 |
| Site-to-Site VPN | BLOCKED-AUDIT; chưa có use case hybrid | DevOps → M2 | Legacy dataset route/SG/mapping nếu thật sự cần | Customer gateway/route/CIDR | STRETCH, không mở mặc định |

## 5. Current Responsibility Matrix

Đây là ownership **theo prompt**, chưa xác minh owner thực tế qua repository.

| Vai trò gốc | Nhóm trách nhiệm trong nguồn | Điểm cần điều chỉnh |
|---|---|---|
| Solution Architect & Backend Lead | Architecture, contracts, blueprint, generation, frontend tương ứng, Bedrock orchestration, integration tests | Tránh nhận toàn bộ việc nối module và sửa CI |
| Cloud Infrastructure & DevOps Engineer | VPC, subnets, EC2, NAT, SG/NACL, IAM phần infra, SSM, logs, IaC, CI/CD, deploy, health UI | Có thể thiếu vertical slice nghiệp vụ đủ lớn |
| Security & Platform Engineer | Auth, RBAC, approval security, audit, IAM review, Guardrails, threat model, security UI/tests | Chưa rõ ai sở hữu toàn bộ review workflow |
| AI Product & Knowledge Engineer | Question Bank, versioning, import, search, S3/KB/RAG, prompts/citations, AI Review UI, Lex | Nhiều module cốt lõi và extension; rủi ro quá tải |
| ML & Reliability Engineer | Dataset, SageMaker, adapter, difficulty, monitoring/SNS, failures, tests, ML UI | Nếu chỉ làm ML optional sẽ đóng góp MVP muộn |

## 6. Workload Problems

1. **Knowledge role quá rộng:** Question Bank + imports + RAG + review UI + Lex tạo nhiều context switch. Chuyển toàn bộ import cho M2; review workflow cho M3; M4 giữ citations/evidence components và API dữ liệu nguồn.
2. **Lead thành nút thắt:** M1 quyết định contracts và điều phối, mỗi feature owner viết adapter/test của mình. M1 không sửa mọi module và không là người review duy nhất.
3. **Cloud role dễ thành người chỉ làm AWS:** M2 sở hữu import từ upload → DB → API → UI → S3/job → tests, đồng thời health UI/API cho deployment.
4. **ML role có nguy cơ ít đóng góp MVP:** M5 làm jobs/retries/monitoring UI từ tuần 1–6. ML được mở sau gate MVP, có phương án thay thế bằng hardening.
5. **Ownership review không rõ:** M3 sở hữu pending/approve/reject/audit/UI; M4 cung cấp câu hỏi, sources/citations, không viết một engine approval khác.
6. **Monitoring phân tán:** M2 quản host/network/log pipeline; M5 quản signal reliability/SNS routing; mỗi owner tự instrument metric của module và cùng xử lý incident.
7. **Ước lượng chưa hiệu chỉnh:** số giờ dưới đây là planning estimate, không là cam kết từ năng lực chưa kiểm tra. Cuối tuần 1 đo velocity; cắt extension trước khi kéo dài đường găng.

## 7. Proposed Reassignments

Không phải thay đổi đã áp dụng vào repository; là đề xuất phân công để đội duyệt.

| Task ID | Task | Owner hiện tại trong prompt | Owner đề xuất | Lý do / phụ thuộc bị ảnh hưởng | Thay đổi workload ước lượng | Giữ primary role? |
|---|---|---|---|---|---|---|
| M2-03, M2-04, M2-08 | Import CSV và màn hình import | AI Knowledge | M2 | Vertical slice coherent; gọi domain writer của M4 và job API của M5 | Chuyển khoảng 30–40 giờ khỏi M4 sang M2, đã nằm trong tổng | Có; M2 vẫn giữ IaC/deploy |
| M3-03, M3-05, M3-06 | Review/approval workflow + UI | Chia Security/AI | M3 | Quyền, transition và audit cùng owner; phụ thuộc revisions/citations M4 | Chuyển khoảng 20–30 giờ review UI/workflow khỏi M4 sang M3 | Có; M4 vẫn giữ AI evidence |
| M5-02, M5-03, M5-04, M5-06 | Job status, retry, failure UI | ML Reliability | M5 từ đầu dự án | Tăng giá trị MVP trước ML; worker dùng DB chung, M1/M2 tích hợp | Không chuyển giờ; đổi thứ tự ưu tiên | Có |
| M2-06, M5-05 và task từng module | Monitoring phân lớp | DevOps/ML Reliability | M2 infra, M5 app, từng owner module metrics | Giảm phụ thuộc một người; dùng chung naming/request IDs | Chuyển khoảng 2–4 giờ instrumentation/module từ M5 về owner | Có |
| M1-05 và adapter theo module | Shared integration | Backend Lead ngầm nhận tất cả | M1 contracts; M2–M5 tự nối module | Không dump integration vào lead; contract tests giữ thống nhất | Chuyển khoảng 12–20 giờ khỏi M1 sang owners, đã tính trong task | Có |

## 8. Revised Responsibility Matrix

### 8.1. Vai trò cụ thể và tiêu chí chọn member

Thang độ khó triển khai: **1/5** CRUD đơn giản có mẫu; **2/5** có validation/integration nhẹ; **3/5** có nhiều trạng thái hoặc cloud integration; **4/5** có concurrency/security/resilience; **5/5** có thiết kế liên module, failure recovery hoặc rủi ro nghiệp vụ cao. Đây là độ khó kỹ thuật của role/task, khác với độ khó sư phạm của câu hỏi thi.

| Member | Role cụ thể | Độ khó role | Năng lực bắt buộc để lựa chọn | Có thể học trong dự án | Không phù hợp nếu |
|---|---|---|---|---|---|
| **M1** | **Solution Architect & Exam Workflow Lead** | **5/5** | .NET/EF/SQL vững; thiết kế API; transaction/concurrency; thuật toán phân bổ câu; giao tiếp và chia contract; làm được UI workflow | Bedrock SDK orchestration và tooling IaC cơ bản | Chỉ mạnh CRUD, ít kinh nghiệm debug integration, không thể chốt quyết định |
| **M2** | **Cloud & DevOps Engineer + Import Module Owner** | **4/5** | TCP/IP/CIDR/routes/SG; Linux; Git/CI; IaC; .NET upload/parser; frontend bảng lỗi; SQL persistence | SSM, CloudWatch và AWS SDK S3 | Chỉ biết thao tác console, chưa đọc được pipeline hoặc không viết API/UI |
| **M3** | **Security & Approval Platform Engineer** | **4,5/5** | Auth/session/token, RBAC/resource permissions, HTTP security; .NET policies; audit/concurrency; UI quản trị và review | IAM/Guardrails sâu hơn | Coi ẩn nút UI là phân quyền hoặc không viết negative tests |
| **M4** | **Question Bank & RAG Product Engineer** | **4,5/5** | CRUD + versioning; EF queries/indexes; UI search/editor; LLM output validation; retrieval/citations; đánh giá groundedness | Bedrock KB/prompt tuning và Lex | Chỉ prompt engineering, không xử lý schema/revisions/UI/business validation |
| **M5** | **Reliability & ML Evaluation Engineer** | **4/5 MVP; 5/5 ML** | .NET background jobs; timeout/retry/idempotency; observability; frontend status UI; SQL job state | SageMaker nếu đã biết Python/data split/metrics cơ bản | Chỉ làm notebook/model, chưa đưa inference vào app hoặc chưa debug worker |

**Cách chọn người:** ưu tiên người mạnh .NET + thiết kế + điều phối vào M1; người mạnh networking/Linux/IaC vào M2; người cẩn thận và có nền auth/security vào M3; người mạnh fullstack dữ liệu và AI application vào M4; người mạnh debugging/async/metrics và có Python vào M5. Không gán theo tên hoặc độ thích AI.

Nếu đội chưa có người ML đủ năng lực, M5 vẫn hoàn thành reliability MVP; SageMaker được giữ ở SHOULD và không tuyên bố model đạt chất lượng khi chưa có evidence. Nếu M4 yếu backend, không bù bằng việc đẩy Question Bank sang M1; cần chọn lại role hoặc ghép mentor giới hạn thời gian.

**Bài kiểm tra chọn role, dùng chung tiêu chí 0–3:** 0 không giải thích được; 1 làm khi có hướng dẫn; 2 tự làm và test; 3 giải thích tradeoff/failure case. Chấm coding, integration, testing, domain reasoning và giao tiếp. Các năng lực bắt buộc nên đạt ít nhất 2; năng lực chính của role nên đạt 3.

| Role | Bài thực hành lựa chọn 60–90 phút | Điểm cần quan sát |
|---|---|---|
| M1 | Thiết kế chọn 10 câu theo 3 ô ma trận, xử lý thiếu và revision thay đổi | Constraints rõ; không lặp câu; không trả COMPLETE khi thiếu; API/UI cùng state |
| M2 | Vẽ public entry/private app/private DB và mô tả một pipeline deploy/rollback; thêm parser CSV báo lỗi từng dòng | Routes/egress đúng; secrets không trong workflow; parser bounded; tái chạy không nhân đôi |
| M3 | Thiết kế approve API và viết ca user không quyền, tự duyệt, duyệt revision cũ | Kiểm quyền server; transaction audit; concurrency; không chỉ kiểm UI |
| M4 | CRUD câu hỏi có revision + retrieval trả 2 citations, xử lý output AI sai schema | Revision bất biến; nguồn truy vết được; AI không approve; UI sửa/review có state |
| M5 | Thiết kế worker retry khi request timeout sau khi đã ghi DB; giải thích train/test leakage | Idempotency/lease; bounded retry; trạng thái dễ hiểu; split dữ liệu hợp lý |

### 8.2. Ownership và reviewer

Mỗi dòng có một owner chính. Reviewer là thành viên review chéo, không chuyển ownership. Trong module chung, chỉ owner module merge migration sau khi thống nhất; tránh cả đội sửa cùng một file DbContext/config mà không điều phối.

| Module / vertical slice | Owner | Reviewer | DB → API → UI → AWS/integration → tests/docs |
|---|---|---|---|
| Exam blueprint/generation/final preview | M1 | M4 nghiệp vụ, M5 resilience | Blueprint/Slot/Exam/Snapshot → exam APIs → matrix/generation/preview → Bedrock orchestration → constraints/integration |
| Import + infrastructure health | M2 | M4 schema, M5 jobs | ImportBatch/Row/DeploymentRecord → import/health APIs → import/health UI → S3/EC2/CI → idempotency/deploy |
| Identity/RBAC/review/audit | M3 | M1 transitions, M4 evidence | User/Role/ReviewDecision/Audit → auth/review APIs → login/admin/review → IAM/Guardrails integration → security tests |
| Question Bank/knowledge/RAG | M4 | M1 domain, M3 access | Question/Revision/Source/Citation → question/knowledge APIs → bank/editor/evidence → S3/Bedrock KB → version/RAG tests |
| Jobs/reliability/ML advisory | M5 | M2 infra, M1 worker contract | Job/Attempt/DifficultyAssessment → status/retry/inference APIs → reliability/ML UI → CloudWatch/SNS/SageMaker → recovery/evaluation |

### 8.3. Contracts và quy tắc tích hợp

- `Question` giữ identity; `QuestionRevision` giữ nội dung bất biến, trạng thái, tác giả, metadata và version token. Câu đã approved được chỉnh sửa sẽ tạo revision DRAFT mới; revision cũ không bị sửa tại chỗ.
- State đề xuất: `DRAFT → REVIEW_REQUIRED → APPROVED` hoặc `REJECTED`. AI chỉ được tạo DRAFT; submit chuyển REVIEW_REQUIRED; chỉ reviewer hợp lệ approve. Định nghĩa “Publish” trong MVP là revision được phép dùng trong đề; không mở public question sharing.
- `BlueprintSlot`: subject/topic/difficulty/count/points; difficulty là nhãn đã thống nhất. Validation tổng câu/tổng điểm, count > 0, không dùng taxonomy không tồn tại.
- `GenerationJob`: blueprint revision, requester, idempotency key, correlation ID, missing slots. .NET chỉ chọn approved revisions; Bedrock xử lý phần thiếu; khi câu AI chưa duyệt, exam ở NEEDS_REVIEW, không COMPLETE.
- `Job`: QUEUED/RUNNING/SUCCEEDED/PARTIAL/FAILED/CANCELLED; lease/heartbeat và attempts để recover. Worker DB-backed cho MVP; không bắt buộc SQS.
- Sau approve, M1 chạy lại assembly theo blueprint và chụp snapshot version-pinned. Có approved candidates không có nghĩa exam tự hoàn tất trước validation cuối.
- API errors dùng contract thống nhất với code/message/validation errors/correlation ID; 401 thiếu identity, 403 thiếu quyền, 409 conflict revision/state. Endpoint kiểm quyền server, frontend chỉ phản ánh quyền.
- Baseline quyền của gói này phải áp dụng `Teacher`, `DepartmentHead`, `SchoolAdmin`, `MinistryAdmin` theo permission matrix; kiểm tra school/department/event scope ở server. Người soạn không tự duyệt revision mình tạo, và quyền quản trị không mặc định cấp quyền xem đề thi mật hoặc duyệt nội dung.
- MVP nhiều trường và kỳ thi cấp Bộ theo ADR 0004. Dữ liệu có phạm vi trường, môn/bộ môn hoặc kỳ thi được giao; chưa mở self-service multi-tenant SaaS. Phải kiểm resource access trên question/exam/job/source, không chỉ role tổng quát.
- AWS: M2 provision tài nguyên; owner module cấu hình SDK/adapter/metrics; M3 review IAM/security. Không dùng access key hard-coded.
- IaC/state, contracts và shared migration được review trước merge. Feature branch ngắn, PR nhỏ, CI green; mỗi owner tự tích hợp, lead giải quyết hợp đồng xung đột.

## 9. Revised 10-Week Plan

### Quy ước đọc và Definition of Done chung

Mỗi gói dưới đây là một backlog epic tuần. Các bước đánh số trong gói là task con có thể đưa vào board; tách tiếp mỗi bước thành việc 2–4 giờ khi triển khai. Estimate là giờ công owner, bao gồm code/test/tài liệu; reviewer dành thêm dung lượng trong budget tuần của mình. Nơi không cần UI/DB/AWS mới được ghi rõ, không tạo feature thừa để đủ cột.

**DoD chung cho mọi gói:** đầu ra đúng scope; validation và authorization server phù hợp; test của gói pass; owner đã thử UI/integration liên quan; PR review xong; migrations/config reproducible; không hard-coded secret; README/runbook/API contract cập nhật; link commit/test report/evidence gắn ticket. Với AWS MUST phải có evidence trên môi trường AWS, mock chỉ dùng để chạy test hoặc phát triển offline. DoD riêng bên dưới bổ sung vào DoD chung.

Các API dưới đây là đề xuất: prefix `/api`; đổi tên theo convention repository khi có repo nhưng giữ semantics. “Nghiệm thu” ở từng gói là điều kiện đạt DoD riêng. “Rủi ro / blocker” phân biệt rủi ro có thể gặp và thông tin cần có; chưa khẳng định blocker đã xảy ra.

### Tuần 1 — Contracts, nền tảng và proof of access

#### M1-01 — Chốt kiến trúc, schema liên module và luồng đề

- **Owner/role:** M1, Exam Workflow Lead. **Mức:** MUST. **Độ khó/effort:** 5/5, 16 giờ. **Phụ thuộc:** phạm vi nguồn và khả năng cung cấp repo.
- **Các bước:** (1) Nếu có repo, map solution/module/API/tests; nếu chưa có, thiết kế skeleton modular monolith. (2) Viết ADR cho state questions/jobs/exams, revision ownership, transaction, cùng M4 chốt taxonomy/schema. (3) Chốt error/DTO contracts và dựng UI shell cho blueprint/generation.
- **Đầu ra BE/FE/DB:** contract draft + module interfaces; UI navigation/wireframe exam; ERD Blueprint/Slot/Exam và phần giao tiếp QuestionRevision/Job. **AWS:** sơ đồ đường đi app/worker/DB, không provision service tại gói này.
- **Security:** resource access và chỉ approved revision vào đề phải là invariant server. **Tests:** contract validation mẫu; fixture đề đủ/thiếu/trùng câu.
- **Docs/evidence:** ADR, ERD, endpoint inventory dự kiến, mapping owner. **Nghiệm thu:** 5 owner cùng đọc được contracts; không có hai state machines mâu thuẫn.
- **Rủi ro/blocker:** scope nở; thiếu repo chỉ chặn audit, chưa chặn thiết kế baseline.

#### M2-01 — Thiết kế deploy/network và contract import

- **Owner/role:** M2, Cloud & Import Owner. **Mức:** MUST. **Độ khó/effort:** 4/5, 16 giờ. **Phụ thuộc:** M1-01 đang chốt; quyền AWS từ đội.
- **Các bước:** (1) Chốt Region, ngân sách, frontend hosting/HTTPS entry, DB hosting và egress NAT/endpoints. (2) Vẽ VPC/CIDR/routes/SG, lựa chọn một công cụ IaC và quản state. (3) Với M4/M5 định nghĩa CSV contract, ImportBatch/Row và wireframe upload/preview/result.
- **Đầu ra BE/FE/DB:** import/health API contracts; wireframe import + health; ImportBatch/Row/DeploymentRecord ERD. **AWS:** network/IAM inventory, estimate chi phí theo region, kế hoạch SSM.
- **Security:** backend/DB private; SSH inbound không mặc định mở; upload giới hạn loại/kích thước. **Tests:** review routes/SG bằng bảng traffic; valid/invalid CSV fixtures.
- **Docs/evidence:** network diagram, cost sheet, IaC/deploy ADR. **Nghiệm thu:** phương án deploy có đường truy cập HTTPS và outbound AWS rõ; import có input/output contract.
- **Rủi ro/blocker:** account permission/quota, ngân sách chưa chốt; tránh build hạ tầng trước khi tính chi phí.

#### M3-01 — Identity, permission matrix và approval threat model

- **Owner/role:** M3, Security & Approval Owner. **Mức:** MUST. **Độ khó/effort:** 4/5, 16 giờ. **Phụ thuộc:** M1-01.
- **Các bước:** (1) Chọn auth theo repo; nếu greenfield cân nhắc ASP.NET Core Identity/session hoặc token flow có quản lý expiry, không tự viết crypto. (2) Chốt bốn role, school/department/event scope và self-review rule trong permission matrix. (3) Thiết kế review/audit contracts và login/admin/review wireframe.
- **Đầu ra BE/FE/DB:** policies và auth/review contracts; wireframe identity/review; User/Role/ReviewDecision/Audit schema. **AWS:** đề xuất runtime/CI roles và secret delivery.
- **Security:** threat model IDOR, privilege escalation, CSRF/XSS theo auth mode, revision conflict. **Tests:** permission matrix với allow/deny cases.
- **Docs/evidence:** role matrix, threat model, token/session ADR. **Nghiệm thu:** mỗi endpoint dự kiến có policy/resource scope; quyền approve không thuộc AI/ops runtime.
- **Rủi ro/blocker:** chọn auth quá phức tạp; cần thống nhất actor/resource identifiers.

#### M4-01 — Domain câu hỏi và chuẩn bị corpus RAG

- **Owner/role:** M4, Question Bank & RAG Owner. **Mức:** MUST. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M1-01; tài liệu nguồn từ đội.
- **Các bước:** (1) Chốt Question/Revision/subject/topic/difficulty và format MCQ một đáp án đúng cho MVP. (2) Chuẩn bị seed có metadata và intentionally missing blueprint slots. (3) Kiểm quyền dùng corpus; làm proof retrieval nhỏ để xác nhận region/model/embedding/vector-store khả dụng.
- **Đầu ra BE/FE/DB:** question/retrieval interfaces; bank/editor wireframe; Question/Revision/KnowledgeSource schema và seed spec. **AWS:** S3/KB requirement list, proof of access nhỏ với M2/M3, ghi chi phí vector store.
- **Security:** không ingest dữ liệu không được phép; chuẩn hoá nguồn và scope môn. **Tests:** schema cases thiếu đáp án, topic sai, số lựa chọn không hợp lệ.
- **Docs/evidence:** taxonomy/difficulty rubric, corpus manifest, AWS access evidence nếu có. **Nghiệm thu:** đủ fixture CRUD và RAG; đường dùng Bedrock có region/model xác nhận hoặc blocker cụ thể.
- **Rủi ro/blocker:** corpus kém chất lượng, model access/quota, vector-store cost; báo sớm trong tuần 1.

#### M5-01 — Reliability contracts và telemetry baseline

- **Owner/role:** M5, Reliability & ML Owner. **Mức:** MUST. **Độ khó/effort:** 4/5, 16 giờ. **Phụ thuộc:** M1-01, M2-01.
- **Các bước:** (1) Chốt Job/Attempt/lease/heartbeat, retry policy và idempotency semantics cho import/generation. (2) Định nghĩa correlation ID, metrics/log schema, error taxonomy. (3) Thiết kế status/retry UI và rubric dataset đánh giá độ khó, chưa huấn luyện model.
- **Đầu ra BE/FE/DB:** job contracts; reliability wireframe; Job/Attempt schema, dataset spec. **AWS:** CloudWatch/SNS requirements, chưa mở SageMaker endpoint.
- **Security:** job scoped theo requester/role; logs không lưu token/secret hay toàn bộ đề mặc định. **Tests:** bảng timeout trước/sau write, worker crash, duplicate request.
- **Docs/evidence:** failure matrix, metric catalog, initial SLO đề xuất. **Nghiệm thu:** M1/M2 thống nhất adapter jobs; mỗi failure có trạng thái UI dự kiến.
- **Rủi ro/blocker:** state machine quá nhiều trường; cần thống nhất thời gian timeout và durable store.

**Gate tuần 1:** architecture/schema/auth/IaC decision có owner; test fixtures dùng chung; quyền AWS và chi phí RAG được xác minh hoặc ghi blocker. Không bắt đầu ML/Lex.

### Tuần 2 — Một luồng CRUD chạy local và nền AWS

#### M1-02 — Blueprint CRUD và UI ma trận

- **Owner/role:** M1. **Mức:** MUST. **Độ khó/effort:** 4/5, 20 giờ. **Phụ thuộc:** M1-01, taxonomy M4-01, auth contract M3-01.
- **Các bước:** (1) Migration Blueprint/Slot với version token; validate count/points/taxonomy. (2) API `POST/GET/PUT /blueprints`, không trust ownership từ client. (3) UI editor ma trận, tổng câu/điểm, field errors và save conflict.
- **Đầu ra BE/FE/DB:** blueprint service/controllers; matrix editor/list; migration + seed blueprint. **AWS:** deploy config qua pipeline chung khi sẵn; chưa gọi Bedrock.
- **Security:** teacher chỉ sửa blueprint thuộc scope. **Tests:** slot invalid, count/score mismatch, 403, concurrent update 409.
- **Docs/evidence:** OpenAPI/examples và recording editor. **Nghiệm thu:** lưu và mở lại ma trận không mất constraints; invalid matrix bị server từ chối.
- **Rủi ro/blocker:** taxonomy chưa ổn; tạm dùng seed contract, không bỏ validation.

#### M2-02 — Provision nền VPC/EC2/SSM/S3 bằng IaC

- **Owner/role:** M2. **Mức:** MUST. **Độ khó/effort:** 4/5, 20 giờ. **Phụ thuộc:** M2-01, runtime roles M3-01, tài khoản AWS.
- **Các bước:** (1) IaC VPC/subnets/routes/IGW/egress/SG và EC2 private app, public entry theo ADR. (2) Instance role + SSM agent, S3 bucket private, state protection và outputs. (3) Dựng `GET /admin/infrastructure/health` skeleton + health UI, ghi deployment metadata từ pipeline.
- **Đầu ra BE/FE/DB:** health API stub có trạng thái UNKNOWN hợp lệ; health panel; migration DeploymentRecord. **AWS:** tài nguyên network/compute/storage và SSM session kiểm chứng.
- **Security:** least-privilege role; không hard-code credentials; API health chỉ Admin, không lộ secret/internal config. **Tests:** IaC validate/plan; kiểm port DB/app không public; SSM access.
- **Docs/evidence:** plan/apply outputs đã redact, resource inventory, đường SSM. **Nghiệm thu:** tái chạy IaC không thay đổi ngoài dự kiến; app host có inbound/outbound đúng ADR.
- **Rủi ro/blocker:** route/SG sai, IAM/SSM thiếu quyền; network một AZ demo không được mô tả là HA production.

#### M3-02 — Login, RBAC và user administration

- **Owner/role:** M3. **Mức:** MUST. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M3-01, schema M1-01.
- **Các bước:** (1) Implement login/logout/session expiry và seed role/user demo. (2) Policies + resource authorization cho câu hỏi/blueprint/jobs, API admin assign roles. (3) UI login/logout và user-role panel, hiển thị unauthorized/expired session.
- **Đầu ra BE/FE/DB:** auth/policy handlers; identity/admin UI; migrations User/Role/scope assignment. **AWS:** nhận secrets qua cơ chế runtime đã chọn, IAM runtime không quản user application thay cho RBAC.
- **Security:** password hashing bằng thư viện chuẩn; rate limit phù hợp; auth cookie/token security theo ADR. **Tests:** login sai/đúng/expiry/logout; 401/403; role escalation và IDOR.
- **Docs/evidence:** permission examples và negative test report. **Nghiệm thu:** gọi trực tiếp API bằng user thiếu quyền vẫn bị chặn dù UI bị bỏ qua.
- **Rủi ro/blocker:** role cache/session stale; hợp đồng issuer/audience/session chưa thống nhất.

#### M4-02 — Question Bank CRUD, metadata và search

- **Owner/role:** M4. **Mức:** MUST. **Độ khó/effort:** 4/5, 22 giờ. **Phụ thuộc:** M4-01, M3-02 khi tích hợp thật.
- **Các bước:** (1) Migration và domain writer tạo Question/Revision DRAFT, validate options/answer. (2) API `POST/GET /questions`, update tạo revision, filter subject/topic/difficulty/status, pagination. (3) UI list/filter/editor và empty/loading/error states.
- **Đầu ra BE/FE/DB:** question service + writer dùng cho import; bank/editor UI; tables/indexes/seed. **AWS:** config runtime DB từ nền M2; không gọi AI.
- **Security:** kiểm scope môn và quyền sửa; approved content không bị mutate tại chỗ. **Tests:** CRUD roundtrip, metadata invalid, paging/filter kết hợp, access denied.
- **Docs/evidence:** OpenAPI, CSV writer contract cho M2, recording. **Nghiệm thu:** tạo/sửa/tìm câu theo ma trận được; mọi nội dung mới bắt đầu DRAFT.
- **Rủi ro/blocker:** query/index kém; auth chưa merge thì phát triển bằng contract, gate cần auth thật.

#### M5-02 — Durable job engine và status UI

- **Owner/role:** M5. **Mức:** MUST. **Độ khó/effort:** 4/5, 20 giờ. **Phụ thuộc:** M5-01, DB contract M1-01, M3-02.
- **Các bước:** (1) Migration Job/Attempt + worker polling có lease/heartbeat, xử lý cancellation token. (2) APIs `GET /jobs/{id}`, list jobs scoped; handler registry cho import/generate. (3) UI jobs list/detail và polling có backoff/cleanup.
- **Đầu ra BE/FE/DB:** durable worker/status APIs; jobs panel; unique idempotency key, attempts/lease fields. **AWS:** worker chạy cùng app hoặc process riêng trên EC2 theo deploy ADR; log correlation IDs.
- **Security:** không xem job của user khác ngoài quyền admin; log input được redacted. **Tests:** job state transitions, cạnh tranh hai worker, authorization, UI polling dừng đúng.
- **Docs/evidence:** worker contract/example handler, state diagram, test report. **Nghiệm thu:** sample job qua QUEUED → RUNNING → SUCCEEDED; worker restart không mất queued job.
- **Rủi ro/blocker:** lock/lease không atomic; cần chọn SQL concurrency strategy theo DB.

**Gate tuần 2:** local login → tạo câu hỏi → tạo blueprint chạy được; durable job skeleton có test; EC2 private quản qua SSM. Chưa yêu cầu AI generation hoàn chỉnh.

### Tuần 3 — Approved-only generation, review và import

#### M1-03 — Chọn câu approved và phát hiện ô thiếu

- **Owner/role:** M1. **Mức:** MUST. **Độ khó/effort:** 5/5, 22 giờ. **Phụ thuộc:** M1-02, M4-02, review states M3-03, M5-02.
- **Các bước:** (1) Selection engine match slots, counts/points, loại duplicate question identities, version-pin revision. (2) `POST /exams/generate` và missing-slot result; selection có seed/reproducibility cho test. (3) UI progress/result, ô thiếu và trạng thái NEEDS_REVIEW/PARTIAL.
- **Đầu ra BE/FE/DB:** exam selection/gap service; generation UI; Exam/ExamItem/GapReport migrations. **AWS:** chưa gọi Bedrock; job adapter nối worker M5.
- **Security:** chỉ approved revisions trong candidate query; blueprint/question scope thống nhất. **Tests:** đủ/thiếu/no candidates/trùng/topic sai/concurrent revision; invariant total score/count.
- **Docs/evidence:** algorithm notes và gap report fixtures. **Nghiệm thu:** thiếu 2 câu trả đúng 2 slot deficits, không gọi thiếu là COMPLETE.
- **Rủi ro/blocker:** constraints khó thỏa; tuần này hỗ trợ blueprint MVP, không tối ưu tổ hợp phức tạp.

#### M2-03 — Import CSV: upload, validate và preview

- **Owner/role:** M2. **Mức:** MUST. **Độ khó/effort:** 3/5, 22 giờ. **Phụ thuộc:** M2-01, M4-02 writer, M5-02, M3-02.
- **Các bước:** (1) APIs `POST /imports`, `GET /imports/{id}` lưu S3 private và ImportBatch. (2) Parser bounded, schema validation từng dòng, duplicate detection, dry-run trước commit. (3) UI upload/progress/preview và tải danh sách lỗi.
- **Đầu ra BE/FE/DB:** upload/validation handler; import UI; ImportBatch/Row/checksum/row errors. **AWS:** S3 upload/download qua backend hoặc presigned hạn chế phạm vi, worker status.
- **Security:** size/type limits, không trust filename, không execute spreadsheet content, access scope theo teacher. **Tests:** bad encoding/header/large file/invalid row/duplicate rows/403.
- **Docs/evidence:** template CSV, sample valid/invalid, parser limits. **Nghiệm thu:** preview cho biết chính xác dòng hợp lệ/lỗi; chưa tự tạo approved question.
- **Rủi ro/blocker:** CSV format drift; commit được triển khai ở M2-04, không có hai domain writers.

#### M3-03 — Approval state machine và pending/reject API

- **Owner/role:** M3. **Mức:** MUST. **Độ khó/effort:** 5/5, 22 giờ. **Phụ thuộc:** M3-02, M4-02 revision model, M1-01.
- **Các bước:** (1) `POST /questions/{id}/submit-review`, `GET /reviews/pending`, `POST /reviews/{id}/decision`. (2) Transaction decision + revision transition; reject requires reason, approve expects revision token. (3) Pending/detail UI baseline cho câu manual, AI evidence bổ sung tuần 5.
- **Đầu ra BE/FE/DB:** review service/policies; review list/actions; ReviewDecision + concurrency constraints. **AWS:** chỉ role .NET có quyền dữ liệu, AI identity không gọi approval path.
- **Security:** reviewer scope/self-review rule; server kiểm expected state/revision. **Tests:** approve/reject, 403, tự duyệt, stale revision 409, hai reviewers cùng duyệt.
- **Docs/evidence:** transition table và request/response examples. **Nghiệm thu:** câu DRAFT không vào đề; sau reviewer approve mới thành candidate.
- **Rủi ro/blocker:** M4 sửa revision model; chốt immutable revision để tránh lost update.

#### M4-03 — Version history và query quality

- **Owner/role:** M4. **Mức:** MUST. **Độ khó/effort:** 4/5, 22 giờ. **Phụ thuộc:** M4-02, M3-03 transition contract.
- **Các bước:** (1) Hoàn thiện immutable revisions và current revision pointer; edit approved tạo DRAFT mới. (2) Version history API + UI compare/view, submit-review action tích hợp M3. (3) Tune pagination/filter indexes và bổ sung nguồn gốc/manual-import identifiers.
- **Đầu ra BE/FE/DB:** history/query service; history/editor integration; indexes/version constraints/source-origin fields. **AWS:** runtime DB migration theo cơ chế deploy chung, không service mới.
- **Security:** lịch sử và đáp án chỉ cho user trong scope; revision approved không writable. **Tests:** edit-after-approve, stale update 409, history order, combined filter, N+1 inspection.
- **Docs/evidence:** migration guide, query evidence trên seed, revision demo. **Nghiệm thu:** đề đã pin revision cũ không đổi khi có edit mới; revision mới phải review lại.
- **Rủi ro/blocker:** nhầm trạng thái question với revision; cần dùng revision làm nguồn approval.

#### M5-03 — Retry, idempotency và integration adapters

- **Owner/role:** M5. **Mức:** MUST. **Độ khó/effort:** 5/5, 20 giờ. **Phụ thuộc:** M5-02, handlers M1-03/M2-03.
- **Các bước:** (1) Bounded retries exponential backoff/jitter và phân loại retryable/permanent errors. (2) Idempotency store và result reuse, timeout-after-write recovery; adapters import/generation. (3) `POST /jobs/{id}/retry` với policy và UI retry button/reason/attempts.
- **Đầu ra BE/FE/DB:** retry/handler interfaces; job attempts/actions UI; idempotency uniqueness và result reference. **AWS:** AWS SDK retry không nhân số lần vô hạn với job retry; log elapsed/provider errors.
- **Security:** retry không đổi user scope và không nhân action approval; admin/owner permissions rõ. **Tests:** duplicate request, expired lease, retry nonretryable error, timeout sau write, worker race.
- **Docs/evidence:** failure taxonomy và retry configuration. **Nghiệm thu:** retry import/generation không nhân đôi business result; duplicate call cùng key trả job cũ.
- **Rủi ro/blocker:** provider timeout không đảm bảo đúng một lần gọi AI; cần đảm bảo không ghi trùng và ghi nhận possible extra invocation/cost.

**Gate tuần 3:** manual question → submit → approve → approved-only exam/gap report chạy end-to-end local; import preview và retry không phá dữ liệu.

### Tuần 4 — RAG, AI safety và staging deploy

#### M1-04 — Bedrock orchestration theo missing slots

- **Owner/role:** M1. **Mức:** MUST. **Độ khó/effort:** 5/5, 20 giờ. **Phụ thuộc:** M1-03, M5-03, RAG adapter contract M4-04.
- **Các bước:** (1) Orchestrator chỉ dispatch các ô thiếu, giới hạn batch/tokens/time. (2) Validate response schema/count/taxonomy trước lưu; trả per-slot status/correlation ID. (3) UI theo dõi generating/review-required/failed, retry thao tác qua M5.
- **Đầu ra BE/FE/DB:** `IAiDraftGenerator` orchestration; generation UI states; generation request/result references. **AWS:** SDK adapter interface, integration thật theo M4-04/05; mock cho contract tests.
- **Security:** không nhận status APPROVED từ AI output; content untrusted; không đưa secret vào prompt. **Tests:** extra/missing questions, invalid JSON, timeout, zero retrieval result, all-approved path không gọi AI.
- **Docs/evidence:** sequence diagram và contract fixtures. **Nghiệm thu:** orchestration bằng mock đạt invariant; ticket chưa coi live RAG hoàn thành trước gate tuần 5.
- **Rủi ro/blocker:** coupling provider response; tách provider DTO và domain DTO.

#### M2-04 — Import commit và CI/deploy staging baseline

- **Owner/role:** M2. **Mức:** MUST. **Độ khó/effort:** 4/5, 20 giờ. **Phụ thuộc:** M2-02, M2-03, M4-03 writer, M5-03.
- **Các bước:** (1) `POST /imports/{id}/commit` explicit, chunk transaction/row result và idempotency theo batch/checksum. (2) UI commit/result hỗ trợ partial rows và lỗi cụ thể. (3) CI build/backend/frontend/tests, artifact version; deploy staging qua SSM/đường đã chọn.
- **Đầu ra BE/FE/DB:** commit handler dùng writer M4; import result UI; committed revision IDs/row state. **AWS:** CI role, staging artifacts, deployment record và logs.
- **Security:** upload không đồng nghĩa commit; chỉ tạo DRAFT; CI secrets/runtime config qua cơ chế được chốt. **Tests:** commit lặp, crash giữa batch, invalid rows, scope, pipeline smoke.
- **Docs/evidence:** CSV commit semantics, pipeline/deploy README. **Nghiệm thu:** cùng batch commit hai lần không tăng số câu; staging có login/question/blueprint.
- **Rủi ro/blocker:** hai nhiệm vụ lớn; giới hạn MVP một CSV format, không thêm Excel/PDF import.

#### M3-04 — AI safety, IAM review và input threat defenses

- **Owner/role:** M3. **Mức:** MUST app policies; Bedrock Guardrails nâng cao SHOULD. **Độ khó/effort:** 4/5, 20 giờ. **Phụ thuộc:** M3-03, M4-04, M2-02.
- **Các bước:** (1) Validate/escape AI content và revision states, deny approval mutation từ generation paths. (2) Review IAM policies theo resources, S3 public block, SG/NACL, secret delivery. (3) Kiểm proof Guardrails cơ bản nếu khả dụng; record policy version/action cho UI review.
- **Đầu ra BE/FE/DB:** content/state enforcement; review safety badges/reasons; policy decision fields. **AWS:** least-privilege policy PRs; Guardrails config nếu scope/budget cho phép.
- **Security:** retrieved content là untrusted; prompt injection defense không chỉ dựa Guardrails. **Tests:** injected instructions, fake approval status, malformed options, XSS display, access denied S3.
- **Docs/evidence:** IAM policy diff, threat mitigations, safety fixtures. **Nghiệm thu:** AI không thể tạo câu approved dù output yêu cầu; citations được kiểm scope ở app.
- **Rủi ro/blocker:** Guardrails không kiểm soát toàn bộ retrieval references; không dùng làm thay thế authorization.

#### M4-04 — S3 corpus, Knowledge Base và retrieval citations

- **Owner/role:** M4. **Mức:** MUST. **Độ khó/effort:** 5/5, 24 giờ. **Phụ thuộc:** M4-01, M2-02, M3-04 IAM contract.
- **Các bước:** (1) Upload corpus có manifest/version/subject/topic metadata; sync ingestion và track status. (2) Tạo KB với embeddings/vector store đã chốt, implement retrieval/filter và source references. (3) UI source catalog/status + evidence viewer có citations/excerpts.
- **Đầu ra BE/FE/DB:** knowledge-source/retrieval APIs; source/evidence UI; KnowledgeSource/Ingestion/Citation metadata. **AWS:** S3 + Bedrock KB, vector store và IAM/egress có kiểm chứng.
- **Security:** filter theo scope server; mở source có authorization; source URI không tự thành public URL. **Tests:** query đúng/sai môn, empty retrieval, deleted source, ingestion fail, cited source matching.
- **Docs/evidence:** corpus manifest, ingestion logs, retrieval request/response đã redacted, chi phí nền vector store. **Nghiệm thu:** truy vấn thật trả nguồn có thể truy vết và đúng phạm vi; ingestion status không bị giả là success.
- **Rủi ro/blocker:** embeddings/vector index/permission/region; tuần 1 proof giúp giảm rủi ro, nhưng phải kiểm live.

#### M5-04 — Failure handling cho import và AI provider

- **Owner/role:** M5. **Mức:** MUST. **Độ khó/effort:** 4/5, 20 giờ. **Phụ thuộc:** M5-03, M1-04, M4-04.
- **Các bước:** (1) Timeout/cancellation, retry budgets/circuit-breaker nếu hợp lý, recover stale RUNNING jobs. (2) Error mapping quota/throttle/permission/retrieval/schema; expose actionable reason. (3) UI partial/failed attempts và retry eligibility, tránh indefinite spinner.
- **Đầu ra BE/FE/DB:** resilience policies; failed-job UI; error code/lease recovery data. **AWS:** capture Bedrock/S3 errors và provider metrics, không mở ML endpoint.
- **Security:** không trả stack trace/token/source content cho user ngoài scope. **Tests:** throttling, AWS access denied, network drop, process kill, cancelled UI request không làm mất job.
- **Docs/evidence:** failure drill scripts/checklist và recovery examples. **Nghiệm thu:** mọi lỗi thử có terminal/recoverable state rõ, không stuck RUNNING vô hạn.
- **Rủi ro/blocker:** mock không phản ánh live AWS; chạy ít nhất một drill staging khi deploy sẵn.

**Gate tuần 4:** staging manual flow chạy; RAG retrieval thật có citations; orchestration và state enforcement có tests. Nếu AWS access chưa chạy, ưu tiên gỡ blocker, chưa mở extension.

### Tuần 5 — Tích hợp live AI draft → review

#### M1-05 — Nối generation thật và luồng chờ duyệt

- **Owner/role:** M1. **Mức:** MUST. **Độ khó/effort:** 5/5, 20 giờ. **Phụ thuộc:** M1-04, M4-05, M3-03, M5-04.
- **Các bước:** (1) Bind live draft adapter và persist mapping gap → draft revision → job. (2) Chỉ gọi AI cho thiếu, aggregate partial results và return NEEDS_REVIEW. (3) UI link từng ô thiếu đến câu nháp/review; preview provisional ghi trạng thái rõ.
- **Đầu ra BE/FE/DB:** live workflow orchestrator; generation-to-review links; exam/draft mappings. **AWS:** Bedrock live invocation theo IAM role, correlation end-to-end.
- **Security:** AI draft không xuất hiện như approved exam item; job resource scope giữ nguyên. **Tests:** mixed approved+AI, retry cùng key, rejected draft, live AWS smoke nhỏ.
- **Docs/evidence:** một recording từ missing slot đến draft thật, request/job/revision IDs. **Nghiệm thu:** live draft có thể chuyển review nhưng exam chưa COMPLETE khi còn unapproved.
- **Rủi ro/blocker:** M4 output schema không ổn; giới hạn schema/batch và giữ rejection actionable.

#### M2-05 — Harden staging deploy và config delivery

- **Owner/role:** M2. **Mức:** MUST. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M2-04, live services tuần 4–5.
- **Các bước:** (1) Version artifacts/config, migration order + pre-deploy backup. (2) HTTPS/proxy health checks và smoke login/import/generate; rollback application version, xác định khi nào DB restore cần làm. (3) Health UI/API hiển thị release/config version/service status không lộ secrets.
- **Đầu ra BE/FE/DB:** health aggregation; deployment/health UI; release/deploy records. **AWS:** staging deploy, HTTPS, SSM/IAM runtime/config và rollback rehearsal.
- **Security:** least privilege CI vs runtime; frontend không chứa AWS credentials. **Tests:** deploy từ clean artifact, failed release rollback, public/private reachability, migration smoke.
- **Docs/evidence:** runbook deploy/rollback/migration và staging URL. **Nghiệm thu:** owner module deploy được theo runbook; staging live AI flow không phụ thuộc laptop cá nhân.
- **Rủi ro/blocker:** migration irreversible; dùng backward-compatible change và backup, không hứa code rollback sẽ đảo mọi DB mutation.

#### M3-05 — Audit bền vững và review evidence

- **Owner/role:** M3. **Mức:** MUST. **Độ khó/effort:** 4/5, 20 giờ. **Phụ thuộc:** M3-03, M4-05, M5-01 log contract.
- **Các bước:** (1) Persist audit cho create/edit/import/submit/approve/reject/generate/finalize/role change; review decision và audit commit atomic theo DB boundary. (2) `GET /audit` có filter actor/action/resource/time và scope. (3) Review detail tích hợp citations/provenance; audit UI/history.
- **Đầu ra BE/FE/DB:** audit writer/query; review evidence/audit UI; immutable audit records/indexes. **AWS:** CloudWatch log correlation với audit ID; DB là nguồn audit nghiệp vụ.
- **Security:** audit không sửa qua app CRUD; không lưu password/token; failure events không chứa secrets. **Tests:** approval không commit nếu audit bắt buộc thất bại; query scopes; actor spoof; event coverage.
- **Docs/evidence:** audit event catalog và ví dụ trace request → decision → exam. **Nghiệm thu:** biết ai duyệt revision nào/lúc nào/vì sao; không có approve thiếu audit decision.
- **Rủi ro/blocker:** duplicate event writers; mỗi mutation ghi đúng một audit event có correlation.

#### M4-05 — Grounded question draft generation và AI evidence

- **Owner/role:** M4. **Mức:** MUST. **Độ khó/effort:** 5/5, 24 giờ. **Phụ thuộc:** M4-04, M3-04 safety, M1-04 interface.
- **Các bước:** (1) Prompt version và retrieval/generation adapter cho MCQ JSON schema, giải thích đáp án/citations. (2) Validate answer/options/metadata/references, reject ungrounded/empty-source output; persist DRAFT revisions. (3) Editor/evidence component cho reviewer, xem nguồn và origin/model/prompt version.
- **Đầu ra BE/FE/DB:** Bedrock draft adapter/domain validation; draft/evidence UI components; AI provenance + citations. **AWS:** live Bedrock model + KB retrieval/generation, không auto-publish.
- **Security:** citations scope/server validation; source text không ghi đè business instructions. **Tests:** schema invalid, unsupported claim/no citation, answer mismatch, prompt injection, live sample nhỏ.
- **Docs/evidence:** prompt version, evaluation fixtures và source linkage. **Nghiệm thu:** mỗi draft có revision/origin/citation truy vết; output không đạt bị giữ lỗi hoặc review, không approved.
- **Rủi ro/blocker:** model hallucination; human review vẫn bắt buộc, không đánh đồng có citation với đúng nội dung.

#### M5-05 — Application metrics, alarms và SNS

- **Owner/role:** M5. **Mức:** MUST. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M5-04, M2-06 infra chuẩn bị song song, instrumentation từ M1–M4.
- **Các bước:** (1) Emit job queued/running/fail/retry/latency và provider error metrics; owner module emit question/import/review/generation counters. (2) Alarm failed jobs/stale queue/provider failure với M2; SNS routing cho sự cố có hành động. (3) UI reliability summary và drill trigger→alarm→delivery.
- **Đầu ra BE/FE/DB:** metrics endpoints/aggregates; reliability dashboard; chỉ thêm aggregate persistence nếu cần, dùng Job/Attempt hiện có. **AWS:** CloudWatch metrics/alarms + SNS config qua IaC M2.
- **Security:** dimensions không dùng raw user/question IDs; notification redacted. **Tests:** alarm threshold, missing data behavior, delivery/recovery state, UI empty period.
- **Docs/evidence:** dashboard/alarm screenshots, notification test có xác nhận delivery. **Nghiệm thu:** lỗi worker tạo metric và một alert actionable; normal state không spam.
- **Rủi ro/blocker:** SNS recipient/subscription chưa xác nhận; không coi publish API success là notification đã nhận.

**Gate tuần 5:** missing slots → Bedrock RAG thật → DRAFT → reviewer xem nguồn và audit trên staging; retry/alerts hoạt động. Đây là gate để đánh giá khả năng hoàn thành MVP tuần 6.

### Tuần 6 — MVP hoàn chỉnh và cổng mở extension

#### M1-06 — Finalize và preview đề bất biến

- **Owner/role:** M1. **Mức:** MUST. **Độ khó/effort:** 5/5, 20 giờ. **Phụ thuộc:** M1-05, approval M3-05, M4-03.
- **Các bước:** (1) Reassemble sau approval và validate đủ slot/count/score; `POST /exams/{id}/finalize`. (2) Persist version-pinned snapshot atomically, block draft/rejected items. (3) `GET /exams/{id}/preview` + final UI, answer key chỉ đúng role; provisional/final phân biệt rõ.
- **Đầu ra BE/FE/DB:** finalization/snapshot APIs; preview UI; FinalExamSnapshot/items/revision refs. **AWS:** staging API/worker/DB deployment; không thêm service.
- **Security:** ownership và answer-key permission; finalize server recheck trạng thái, chống race. **Tests:** question edit sau finalize, approve/reject races, thiếu 1 slot, duplicate item, totals.
- **Docs/evidence:** snapshot model, final preview recording. **Nghiệm thu:** đề hoàn chỉnh chỉ chứa approved revisions; sửa nguồn sau đó không đổi snapshot.
- **Rủi ro/blocker:** approval concurrent với finalization; transaction/optimistic concurrency cần rõ.

#### M2-06 — Infra observability, Flow Logs và health slice

- **Owner/role:** M2. **Mức:** MUST baseline. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M2-05, metric spec M5-05.
- **Các bước:** (1) Agent/log collection và host/disk/service alarms; IaC log retention + SNS topics/alarms do M5 định nghĩa. (2) Flow Logs ở phạm vi phù hợp và kiểm trace blocked connection. (3) Health API/UI đọc trạng thái deploy/DB/worker/provider với timeout, không gọi inference trả phí chỉ để health check.
- **Đầu ra BE/FE/DB:** protected health aggregation; infrastructure dashboard; dùng DeploymentRecord hiện có, không tạo DB mới nếu không cần. **AWS:** CloudWatch/Flow Logs/IaC alerts/log retention.
- **Security:** Admin-only; logs redact; health provider state UP/DOWN/UNKNOWN, không lộ credentials. **Tests:** kill service/disk threshold simulation/DB unavailable/log pipeline access.
- **Docs/evidence:** infra dashboard, Flow Logs evidence và alarm drill. **Nghiệm thu:** phân biệt được app failure với host/network failure từ UI/logs.
- **Rủi ro/blocker:** log volume/cost; retention hữu hạn theo môi trường, không keep forever mặc định.

#### M3-06 — Hoàn thiện review UX và security gate MVP

- **Owner/role:** M3. **Mức:** MUST. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M3-05, M1-06, M4-05.
- **Các bước:** (1) Hoàn thiện review queue/filter/detail/reason/confirm và revision conflict UX. (2) Chạy RBAC/resource/self-review security suite toàn flow. (3) Chặn approval/finalize bypass từ user/worker không quyền; kiểm audit completeness.
- **Đầu ra BE/FE/DB:** policy/transition fixes; review/admin UX; migration chỉ nếu phát hiện thiếu constraints. **AWS:** verify runtime IAM và app policy khác nhau, staging configuration audit.
- **Security:** backend guard cho mọi mutation, answer-key access, XSS rendering. **Tests:** browser/API direct calls cho Teacher/Reviewer/Admin/anonymous; forced 403/409 states.
- **Docs/evidence:** permission test matrix và review guide. **Nghiệm thu:** chưa có quyền hoặc revision cũ không approve được; AI không bypass review.
- **Rủi ro/blocker:** lỗ hổng cross-resource; unresolved security P0 chặn extension.

#### M4-06 — Chuẩn hoá chất lượng ngân hàng và RAG MVP

- **Owner/role:** M4. **Mức:** MUST. **Độ khó/effort:** 4/5, 20 giờ. **Phụ thuộc:** M4-05, M3-06.
- **Các bước:** (1) Curate seed đủ cho demo đủ/thiếu ô và rejected AI draft; validate taxonomy/difficulty labels. (2) Chạy benchmark RAG tối thiểu 20 yêu cầu đại diện, báo schema-valid/citation-present/groundedness review riêng. (3) Fix source viewer/editor/filter và cases no-source/deleted-source.
- **Đầu ra BE/FE/DB:** retrieval/output fixes; bank/evidence UX; dataset/corpus versions và benchmark results. **AWS:** live Bedrock sample có quota/token budget.
- **Security:** output vẫn review-required dù benchmark tốt; corpus và sources scope đúng. **Tests:** regression samples, import-created drafts, version + review integration.
- **Docs/evidence:** RAG quality report có sample size/limitations; seeded demo manifest. **Nghiệm thu:** 100% câu được persist tuân schema/state; draft quality được đánh giá người, không giả định model đúng.
- **Rủi ro/blocker:** tuning vô hạn; giới hạn schema và corpus, ưu tiên flow ổn định hơn prompt tối ưu.

#### M5-06 — Recovery drill và jobs UX MVP

- **Owner/role:** M5. **Mức:** MUST. **Độ khó/effort:** 4/5, 20 giờ. **Phụ thuộc:** M5-05, core flow M1-06/M2-04/M3-06/M4-06.
- **Các bước:** (1) Run crash/timeout/throttle/cancel drills trên staging. (2) Hoàn thiện job details/attempts/retry reasons/result links và stale-state recovery. (3) Publish reliability test report và gate report với mỗi owner.
- **Đầu ra BE/FE/DB:** recovery fixes; jobs UI hoàn chỉnh; consistent job/result state sau restart. **AWS:** CloudWatch/SNS evidence từ staging, worker restart qua M2 runbook.
- **Security:** retry không thay approval/finalization quyền; notifications không lộ đề. **Tests:** 5 failure scenarios, duplicate generation/import effect, long-running lease recovery.
- **Docs/evidence:** failure drill reports, error-to-action guide. **Nghiệm thu:** không mất job/câu hỏi khi restart; không ghi trùng revision/result sau retry.
- **Rủi ro/blocker:** cổng MVP chưa đạt; dùng tuần 7–8 sửa core thay ML/Lex.

**Gate MVP cuối tuần 6:** mọi yêu cầu MUST cốt lõi chạy AWS end-to-end; auth/RBAC/audit/approved-only/snapshot invariants pass; không còn P0. Extension chỉ mở khi gate này đạt và tuần 7 vẫn còn dung lượng.

### Tuần 7 — Hardening và ML evaluation có điều kiện

#### M1-07 — Hiệu năng generation và contract regression

- **Owner/role:** M1. **Mức:** MUST hardening. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M1-06, gate MVP.
- **Các bước:** (1) Profile selection/gap/finalization trên dataset mục tiêu chốt tuần 1. (2) Fix query indexes/N+1/bulk fetch và giữ transaction ngắn. (3) UI loading/progress/conflict/retry đồng nhất contracts.
- **Đầu ra BE/FE/DB:** query/contract improvements; generation UX fixes; indexes nếu evidence cần. **AWS:** measure staging latency/cost signals, không thêm compute chỉ để che query lỗi.
- **Security:** tối ưu không bỏ scope/status filter. **Tests:** representative concurrent generation, regression count/score/no-duplicate, query count.
- **Docs/evidence:** before/after profile và target/measurement conditions. **Nghiệm thu:** invariants giữ nguyên, bottleneck chính được xử lý; latency có evidence.
- **Rủi ro/blocker:** tối ưu quá sớm; nếu core lỗi thì task dành cho core fixes.

#### M2-07 — Backup/restore, Reachability và infrastructure reproducibility

- **Owner/role:** M2. **Mức:** MUST backup/IaC; Reachability SHOULD. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M2-06.
- **Các bước:** (1) Backup DB/corpus manifests, restore vào môi trường kiểm thử riêng. (2) Reconcile IaC drift và test public entry → private app → private DB; chạy Reachability Analyzer nếu có ngân sách. (3) Health UI hiển thị backup/deploy evidence status được ghi bởi pipeline/runbook, không giả real-time nếu chưa có tích hợp.
- **Đầu ra BE/FE/DB:** restore validation scripts/status API; health evidence fields; restore data kiểm checksum/counts. **AWS:** backup storage/IaC drift/reachability evidence.
- **Security:** backup private/encrypted/access-limited; không restore đè môi trường chính khi thử. **Tests:** restore auth/question/exam/audit relationships; denied network path.
- **Docs/evidence:** RPO/RTO đo trong drill, IaC inventory, network findings. **Nghiệm thu:** phục hồi data demo theo runbook và kiểm referential integrity.
- **Rủi ro/blocker:** backup có mà chưa restore; thiếu access để tạo test environment.

#### M3-07 — Security regression và permissions hardening

- **Owner/role:** M3. **Mức:** MUST. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M3-06 và endpoints đã hoàn thiện.
- **Các bước:** (1) Review endpoints mới/role assignment/session expiry/CORS-CSRF theo auth mode. (2) Scan secrets/dependencies; verify log/input sanitization và scope source/job/exam. (3) UI account/admin conflict states và permission changes có xử lý session stale.
- **Đầu ra BE/FE/DB:** security fixes/policy tests; admin UI fixes; scope/index constraints nếu cần. **AWS:** review IAM resources/secrets/network config với M2.
- **Security:** không broad IAM chỉ để fix 403; credentials runtime short-lived theo IAM role. **Tests:** IDOR, escalation, malicious content, stale roles, unauthorized citations.
- **Docs/evidence:** security findings với severity/evidence/remediation; không ghi confirmed bug nếu chưa tái hiện. **Nghiệm thu:** security P0/P1 có fix hoặc scope-gate rõ, core critical findings đóng.
- **Rủi ro/blocker:** phát hiện mới tốn thời gian; ưu tiên trước extension.

#### M4-07 — Source quality, provenance và RAG regression

- **Owner/role:** M4. **Mức:** MUST hardening. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M4-06.
- **Các bước:** (1) Review retrieval relevance/metadata filters với benchmark đã version. (2) Hiển thị rõ nguồn không còn tồn tại hoặc citation chưa xác minh; nguồn bị cập nhật không âm thầm đổi provenance cũ. (3) Fix bank editor/history accessibility và API error states.
- **Đầu ra BE/FE/DB:** retrieval/version fixes; bank/evidence UI polish; source version/hash và evaluation manifest. **AWS:** sync source changes có status/error monitoring.
- **Security:** ingestion/retrieval filter theo app scope, không chỉ metadata người dùng gửi. **Tests:** source update/delete, citation access, prompt/schema regression, UI keyboard/errors.
- **Docs/evidence:** RAG regression report, source lifecycle guide. **Nghiệm thu:** reviewer truy vết đúng source version hoặc thấy cảnh báo thiếu evidence rõ ràng.
- **Rủi ro/blocker:** corpus changes làm benchmark drift; đóng băng demo corpus trước tuần 9.

#### M5-07 — Dataset độ khó, baseline và SageMaker evaluation

- **Owner/role:** M5. **Mức:** SHOULD có điều kiện; nếu gate chưa đạt, thay bằng reliability fixes. **Độ khó/effort:** 5/5, 18 giờ. **Phụ thuộc:** MVP gate, difficulty rubric M4-01, nguồn labels được review, ML budget.
- **Các bước:** (1) Làm dataset có provenance/labels; split theo question identity/source group để tránh revision leakage. (2) Baseline đơn giản và training/evaluation reproducible, confusion matrix/macro-F1/per-class recall. (3) API/UI evaluation report, thiết kế DifficultyAssessment advisory, chưa thay nhãn approved.
- **Đầu ra BE/FE/DB:** evaluation report API; ML report UI; DatasetVersion/ModelVersion/Assessment schema. **AWS:** SageMaker training/evaluation job nếu dữ liệu đủ; artifacts S3, chưa duy trì idle endpoint.
- **Security:** data được phép sử dụng, access-limited; no automatic approval/relabel. **Tests:** duplicate/leakage/label validation, baseline comparison, schema contract.
- **Docs/evidence:** dataset card, model card, split/metrics/limitations. **Nghiệm thu:** report tái lập, không tuyên bố generalization nếu holdout quá nhỏ; no endpoint nếu không đạt gate.
- **Rủi ro/blocker:** thiếu nhãn chất lượng hoặc mỗi lớp quá ít mẫu; chuyển sang evaluation prototype/local report, giữ MVP ổn định.

**Gate tuần 7:** không còn core P0/P1 ảnh hưởng demo; backup restore có evidence. ML chỉ tiến tới endpoint khi dữ liệu đủ và model không tệ hơn baseline theo rubric đã chốt.

### Tuần 8 — Extension tích hợp có kiểm soát

#### M1-08 — Adapter extension vào blueprint, giữ invariants

- **Owner/role:** M1. **Mức:** SHOULD extension; MUST core fixes nếu gate chưa đạt. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M4-08/M5-08 nếu mở extension; M1-07.
- **Các bước:** (1) Contract tạo blueprint từ Lex slots vẫn qua validation/permissions .NET. (2) Difficulty assessment chỉ metadata advisory, teacher/reviewer quyết định. (3) UI blueprint prefill/confirmation và assessment indicators, feature flags rõ.
- **Đầu ra BE/FE/DB:** adapters/reuse existing APIs; extension UI integration; blueprint origin/assessment refs nếu cần. **AWS:** Lex/SageMaker qua adapters owners, không viết business rules trong AWS.
- **Security:** intent/ML không bypass review hoặc tự finalize. **Tests:** extension off, invalid slots, confidence thấp, API quyền, core flow không AWS extension.
- **Docs/evidence:** feature flags/extension contracts, recording. **Nghiệm thu:** tắt Lex/ML vẫn hoàn thành core demo; extension chỉ prefill/advisory.
- **Rủi ro/blocker:** extension pull requests muộn; không merge khi làm hỏng core contracts.

#### M2-08 — Import hardening và extension infrastructure support

- **Owner/role:** M2. **Mức:** MUST import; extension infra SHOULD. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M2-04/M2-07, optional M4-08/M5-08.
- **Các bước:** (1) Load/error/retry tests import, cleanup orphan uploads theo policy. (2) UI lỗi từng dòng và import result navigation hoàn chỉnh. (3) Provision IAM/config/teardown cho Lex/SageMaker nếu gate cho phép, cost tags/budget estimate cập nhật.
- **Đầu ra BE/FE/DB:** import cleanup/result fixes; import UI; batch lifecycle/retention fields nếu cần. **AWS:** S3 lifecycle, extension IaC theo scope, resource inventory.
- **Security:** cleanup không xóa corpus/revisions referenced; extension role scoped. **Tests:** resume partial batch, duplicate uploads, expired source URL, optional service disabled.
- **Docs/evidence:** import runbook + updated cost/teardown plan. **Nghiệm thu:** CSV demo không mất/trùng câu; tắt extension không ảnh hưởng import/deploy.
- **Rủi ro/blocker:** cleanup sai prefix; test trên bucket/test dataset riêng.

#### M3-08 — Security review extension và review UX refinement

- **Owner/role:** M3. **Mức:** MUST core UX; extension security SHOULD. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M3-07, optional M4-08/M5-08.
- **Các bước:** (1) Review Lex session binding/slots và inference request scope/IAM. (2) Validate AI/model output không ghi trạng thái approval; audit source/model/actor. (3) Review UI advisory explanation/error badges; role permissions trên extension APIs.
- **Đầu ra BE/FE/DB:** extension policies + audit hooks; review explanation UI; model/intent provenance fields nếu mở extension. **AWS:** IAM/bot/endpoint policy review.
- **Security:** không dùng session ID client gửi làm identity; model score không thay reviewer. **Tests:** cross-user chat session, malformed slot, unauthorized inference, extension disabled.
- **Docs/evidence:** extension threat addendum + security tests. **Nghiệm thu:** extension không tạo đường bypass auth/approval; nếu chưa mở extension, core regression pass.
- **Rủi ro/blocker:** bot locale/model output khác kỳ vọng; validation ở .NET vẫn là chuẩn.

#### M4-08 — Lex V2 intent/slot collection đến blueprint

- **Owner/role:** M4. **Mức:** SHOULD có điều kiện; nếu thiếu capacity dùng tuần này sửa RAG/core. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** MVP gate, M1-08 contract, locale/region xác minh, IAM M2/M3.
- **Các bước:** (1) Bot intent CreateExamBlueprint, slots subject/topic/count/difficulty/time, hỏi thiếu và confirmation. (2) .NET Lex adapter gọi bot runtime; nhận ready/confirmed intent rồi validate và gọi blueprint API bằng user context. (3) Chat/prefill UI có confirmation, fallback form và timeout.
- **Đầu ra BE/FE/DB:** Lex session/slot adapter; teacher assistant UI; session↔user mapping/intent audit, blueprint dùng bảng cũ. **AWS:** Lex V2 bot/version/alias, app-controlled fulfillment, không bắt buộc Lambda mới.
- **Security:** intent không trực tiếp generate/approve; user scope từ auth server. **Tests:** missing/invalid slots, denial/cancel, session isolation, Lex unavailable, core form vẫn chạy.
- **Docs/evidence:** bot locale/alias/version, chat transcript + blueprint output. **Nghiệm thu:** slots được user xác nhận rồi .NET lưu ma trận hợp lệ; không bịa hỗ trợ tiếng Việt nếu locale chưa xác minh.
- **Rủi ro/blocker:** locale/quota/capacity; chọn locale AWS hỗ trợ thực tế và mô tả giới hạn, không chặn MVP.

#### M5-08 — SageMaker inference advisory và fallback

- **Owner/role:** M5. **Mức:** SHOULD có điều kiện; nếu chưa đủ data/gate dùng reliability fixes. **Độ khó/effort:** 5/5, 18 giờ. **Phụ thuộc:** M5-07 acceptance, IAM/endpoint infra M2-08, M1-08.
- **Các bước:** (1) Deploy model endpoint theo ADR và budget; .NET inference adapter timeout/schema validation. (2) Persist DifficultyAssessment label/confidence/model version, không overwrite approved difficulty. (3) ML UI report/advisory/override explanation, feature flag và endpoint-off fallback UNAVAILABLE.
- **Đầu ra BE/FE/DB:** inference APIs/adapter; advisory/eval UI; Assessment records và user decision audit. **AWS:** SageMaker endpoint + S3 artifact/IAM/CloudWatch, kế hoạch xóa endpoint khi không dùng.
- **Security:** model output untrusted; đánh giá sư phạm không tương đương accuracy dự đoán thực tế học sinh. **Tests:** endpoint down/timeout/invalid response/low confidence; flags off; override audit.
- **Docs/evidence:** model card, endpoint invoke proof, cost/teardown runbook. **Nghiệm thu:** evaluator chỉ gợi ý; reviewer quyết định; endpoint tắt vẫn chạy core.
- **Rủi ro/blocker:** idle endpoint cost, chất lượng model chưa đạt; bỏ live inference khỏi demo nếu không có evidence chất lượng.

**Gate tuần 8:** feature freeze; extensions chỉ giữ nếu có test/evidence và không làm core mất ổn định. VPN không nằm trong lịch baseline.

### Tuần 9 — Acceptance, performance và tài liệu bàn giao

#### M1-09 — E2E business acceptance và đóng lỗi exam

- **Owner/role:** M1. **Mức:** MUST. **Độ khó/effort:** 4/5, 16 giờ. **Phụ thuộc:** M1-08/core gate; feature freeze.
- **Các bước:** (1) Chạy đủ/manual/missing-AI/reject/retry/final snapshot E2E cases. (2) Fix selection/finalization contracts và UI blockers. (3) Đóng traceability requirements→task→test→demo.
- **Đầu ra BE/FE/DB:** business fixes; generation/preview UI accepted; final seed/snapshots. **AWS:** E2E staging trace IDs, optional extensions off test.
- **Security:** no draft in final exam và resource permissions regression. **Tests:** toàn bộ business acceptance matrix, concurrent finalize.
- **Docs/evidence:** acceptance report + demo script. **Nghiệm thu:** core scenarios pass từ browser tới DB/AWS; no core P0/P1.
- **Rủi ro/blocker:** feature creep; tuần này không mở module mới.

#### M2-09 — Release rehearsal và cost/reproducibility verification

- **Owner/role:** M2. **Mức:** MUST. **Độ khó/effort:** 4/5, 18 giờ. **Phụ thuộc:** M2-08, code freeze candidates.
- **Các bước:** (1) Rehearse clean deploy, rollback và DB restore trên môi trường kiểm thử. (2) Verify DNS/HTTPS/config/roles/log retention/cost inventory. (3) Fix import/health UI issues từ acceptance, hoàn thiện deployment evidence.
- **Đầu ra BE/FE/DB:** deployment/health/import fixes; release health panel; restored test dataset. **AWS:** reproducible IaC/pipeline evidence và resource cost review.
- **Security:** không để debug routes/public DB/temporary SSH mở sau rehearsal. **Tests:** fresh deployment smoke, restore invariants, health false positive, network deny.
- **Docs/evidence:** install/deploy/rollback/backup guide, chi phí theo config thực tế. **Nghiệm thu:** một member khác follow runbook deploy được không dựa vào thao tác bí mật của owner.
- **Rủi ro/blocker:** tài nguyên console không trong IaC; import/drift vào IaC hoặc ghi ngoại lệ cụ thể.

#### M3-09 — Final security acceptance và audit evidence

- **Owner/role:** M3. **Mức:** MUST. **Độ khó/effort:** 4/5, 16 giờ. **Phụ thuộc:** M3-08, release candidate.
- **Các bước:** (1) Retest P0/P1 fixes và toàn permission matrix. (2) Verify audit cho demo actions/role changes và provenance. (3) Fix review/login/admin usability ảnh hưởng acceptance, tạo sanitized evidence pack.
- **Đầu ra BE/FE/DB:** security fixes; review/admin final UX; audit records cho seeded demo. **AWS:** final IAM/config review với M2.
- **Security:** evidence không chứa password/token/key; không dùng shared admin cho mọi vai trò trong demo. **Tests:** negative cases direct API + browser, expired session, source access.
- **Docs/evidence:** security acceptance checklist và threat model final. **Nghiệm thu:** không còn confirmed critical findings; demo có chứng minh 403/approval rule.
- **Rủi ro/blocker:** unresolved finding; phải sửa hoặc tắt extension liên quan, không bỏ auth/review.

#### M4-09 — Content/RAG acceptance và corpus freeze

- **Owner/role:** M4. **Mức:** MUST. **Độ khó/effort:** 4/5, 16 giờ. **Phụ thuộc:** M4-07/08, release candidate.
- **Các bước:** (1) Freeze corpus/prompt/seed versions; kiểm citations từng demo question. (2) Rerun RAG regression, reject/bad-output cases; Lex transcript nếu mở. (3) Fix bank/editor/evidence UX blockers và viết content workflow guide.
- **Đầu ra BE/FE/DB:** RAG fixes; final question/source UI; demo source/seed manifest. **AWS:** KB ingestion/version state và live smoke evidence.
- **Security:** câu AI vẫn người duyệt; source permissions không thay sau freeze. **Tests:** corpus hash, citation resolution, approved revision retention, optional Lex off.
- **Docs/evidence:** prompt/corpus versions, RAG quality report, user guide. **Nghiệm thu:** demo có đủ câu đúng metadata và một case thiếu để live RAG tạo draft.
- **Rủi ro/blocker:** model variability; không dựa exact wording output để test, dùng schema/grounding/human review.

#### M5-09 — Reliability acceptance và observability proof

- **Owner/role:** M5. **Mức:** MUST; ML evidence nếu đã mở. **Độ khó/effort:** 4/5, 16 giờ. **Phụ thuộc:** M5-06/08, staging release candidate.
- **Các bước:** (1) Rerun failure/concurrency/idempotency drills và latency measurement. (2) Verify dashboard/alarms/SNS end-to-end, fix jobs UI. (3) Model acceptance report và endpoint-off rehearsal nếu dùng ML.
- **Đầu ra BE/FE/DB:** resilience fixes; reliability/ML UI accepted; job/attempt/assessment evidence. **AWS:** alarms/delivery/endpoint-off test evidence.
- **Security:** drill không lộ data; retry/recovery giữ actor permissions. **Tests:** worker kill, throttle, duplicate submit, stale queue, provider unavailable.
- **Docs/evidence:** reliability report, model report có limitations nếu dùng. **Nghiệm thu:** mọi drill có result/action rõ, core chạy được khi Lex/ML tắt.
- **Rủi ro/blocker:** alert test chưa nhận; phối hợp subscription trước demo.

**Gate tuần 9:** release candidate không còn core P0/P1, toàn MUST có evidence. Mỗi người có thể giải thích vertical slice và test/failure case mình sở hữu.

### Tuần 10 — Final demo, handover và verification cuối

#### M1-10 — Final business demo và kiến trúc bàn giao

- **Owner/role:** M1. **Mức:** MUST. **Độ khó/effort:** 3/5, 14 giờ. **Phụ thuộc:** M1-09 và acceptance report toàn đội.
- **Các bước:** (1) Tag release candidate đã nghiệm thu, run core demo với actors riêng. (2) Explain blueprint/gap/AI draft/review/snapshot và version changes. (3) Chốt architecture/domain/API handover; chỉ sửa blocker đã tái hiện.
- **Đầu ra BE/FE/DB:** release exam modules; preview demo; version-pinned demo data. **AWS:** live staging demo có resource/job IDs.
- **Security:** final exam invariants có evidence. **Tests:** final smoke + business E2E cho release tag.
- **Docs/evidence:** architecture handover/demo recording/traceability final. **Nghiệm thu:** người khác theo script chạy được core flow; release tag và reports trùng phiên bản.
- **Rủi ro/blocker:** sửa gấp sau tag; retest affected path và cập nhật evidence nếu có.

#### M2-10 — Live deployment, vận hành và cleanup handover

- **Owner/role:** M2. **Mức:** MUST. **Độ khó/effort:** 3/5, 18 giờ. **Phụ thuộc:** M2-09, release tag.
- **Các bước:** (1) Verify infra/HTTPS/SSM/logs/backup trước demo. (2) Demo import và infra health, run deployment verification. (3) Bàn giao inventory/cost/teardown guide; chỉ thực hiện teardown môi trường khi đội xác nhận đã bàn giao, không xóa dữ liệu cần giữ.
- **Đầu ra BE/FE/DB:** release import/health slice; UI evidence; backup/deployment records. **AWS:** final IaC outputs, access/runbook/cost evidence.
- **Security:** credentials không trong tài liệu; revoke temporary access theo lịch bàn giao. **Tests:** final deploy/health/import smoke và restore evidence còn dùng được.
- **Docs/evidence:** infra/pipeline/rollback/backup/cleanup runbooks. **Nghiệm thu:** người nhận có thể vận hành/deploy/restore và biết tài nguyên nào phát sinh chi phí.
- **Rủi ro/blocker:** account ownership chưa bàn giao; ghi người giữ tài khoản và quy trình tiếp nhận.

#### M3-10 — Security, role demo và review/audit handover

- **Owner/role:** M3. **Mức:** MUST. **Độ khó/effort:** 3/5, 14 giờ. **Phụ thuộc:** M3-09, release tag.
- **Các bước:** (1) Demo teacher không approve được, reviewer approve/reject revision có nguồn. (2) Trace audit theo actor/revision/exam, show direct API 403/409. (3) Chốt security policy/known limitations/user administration guide.
- **Đầu ra BE/FE/DB:** release auth/review/audit; role/review UI; signed-off audit evidence. **AWS:** IAM/security evidence pack đã redacted.
- **Security:** demo dùng separate accounts; chỉ dùng dữ liệu demo. **Tests:** final security smoke và audit coverage của release.
- **Docs/evidence:** permission matrix, review/audit guide, findings status. **Nghiệm thu:** người nhận hiểu và kiểm được separation of duties; không có path AI tự approve.
- **Rủi ro/blocker:** demo role mapping sai; verify trước buổi trình bày.

#### M4-10 — Bank/RAG demo và knowledge handover

- **Owner/role:** M4. **Mức:** MUST; Lex optional evidence. **Độ khó/effort:** 3/5, 14 giờ. **Phụ thuộc:** M4-09, release tag.
- **Các bước:** (1) Demo CRUD/search/versioning và nguồn gốc câu AI. (2) Live RAG missing-slot sample, reviewer xem citations; optional Lex prefill riêng nếu đạt. (3) Bàn giao source/prompt/ingestion/evaluation guide.
- **Đầu ra BE/FE/DB:** release question/RAG modules; bank/evidence UI; corpus/seed/provenance versions. **AWS:** KB/model configuration evidence và bot alias nếu dùng.
- **Security:** người duyệt xác nhận AI output; không public corpus để làm demo. **Tests:** final schema/citation/source-access smoke.
- **Docs/evidence:** content guide, RAG report, optional Lex limitations. **Nghiệm thu:** có thể truy từ draft về tài liệu nguồn và prompt version; nguồn cập nhật có quy trình sync.
- **Rủi ro/blocker:** live AI error; dùng job retry/error path để demo recovery; cached sample phải ghi rõ không thay thế evidence live RAG MUST.

#### M5-10 — Reliability/ML demo và failure recovery handover

- **Owner/role:** M5. **Mức:** MUST reliability; ML optional. **Độ khó/effort:** 3/5, 14 giờ. **Phụ thuộc:** M5-09, release tag.
- **Các bước:** (1) Demo lỗi có kiểm soát → jobs UI → bounded retry → recovery/alert. (2) Nếu dùng ML, trình bày evaluation/model limitations và endpoint-off fallback. (3) Bàn giao reliability dashboard/runbook/model artifacts, phối hợp M2 lịch dừng tài nguyên trả phí sau bàn giao.
- **Đầu ra BE/FE/DB:** release worker/observability; jobs/ML UI; final drill records/model versions. **AWS:** CloudWatch/SNS evidence, endpoint inventory nếu dùng.
- **Security:** recovery không thay review approvals, không dùng data thật trong drill. **Tests:** final failure/idempotency smoke và optional endpoint-off.
- **Docs/evidence:** incident/retry runbook, reliability/model report. **Nghiệm thu:** người nhận chẩn đoán và retry được failure; hiểu ML là advisory.
- **Rủi ro/blocker:** endpoint còn chạy sau demo; handover có owner và lịch cleanup rõ, không chỉ nhắc miệng.

**Verification cuối:** tag release, CI report, live AWS E2E, security negative tests, recovery drill, restore evidence và tài liệu phải cùng phiên bản. Dừng triển khai tính năng mới sau nghiệm thu; kế hoạch này không tự cấp phép thay đổi mã nguồn hoặc AWS hiện có.

## 10. Critical Path

### 10.1. Đường găng sản phẩm

`M1-01 + M4-01 schema/taxonomy → M4-02 Question API → M3-03 review/approval + M4-03 revisions → M1-02/03 blueprint/selection/gaps → M4-04/05 live RAG drafts + M1-04/05 orchestration → M3-05/06 review/evidence/audit → M1-06 reassembly/final snapshot → tuần 9 E2E acceptance → tuần 10 demo`.

`M3-01 auth contract → M3-02 Auth/RBAC → M3-03 Review Permission → Approval → approved revision được dùng → Finalize/Publish theo nghĩa MVP`.

`M5-01 job contract → M5-02 durable worker → M5-03 idempotency/retry → M1/M2 handlers → M5-04 failure states → M5-05/06 monitoring/recovery`.

`M2-01 network/deploy ADR → M2-02 IaC/private EC2/SSM → M2-04 CI/staging → M2-05 live config/rollback → M2-06 observability → M2-07 restore → acceptance`.

M1-02 và M4-02 có thể làm song song sau schema contract; M3 auth và M5 jobs làm song song. Không chờ UI hoàn chỉnh mới nối APIs, nhưng không tính mock thành evidence deploy thật.

### 10.2. Prerequisites theo integration

| Integration | Phải có trước khi tích hợp thật | Owner xác nhận | Gate |
|---|---|---|---|
| Bedrock | Region/model/quota; corpus có quyền dùng; S3/KB ingestion; embedding/vector store; IAM/egress; question schema/draft states; JSON schema; retrieval filter/citations; durable jobs/timeouts; cost budget | M4 app, M2 infra, M3 policy, M1 workflow, M5 resilience | Retrieval live tuần 4, generation live tuần 5 |
| SageMaker | MVP gate; rubric labels; data provenance; group split chống leakage; baseline/metrics/model acceptance; training budget/artifact storage; .NET advisory contract; endpoint timeout/fallback/cleanup | M5 chính, M4 labels, M2 infra, M3 access | Evaluation tuần 7; endpoint tuần 8 có điều kiện |
| Lex fulfillment | MVP gate; blueprint API stable; supported locale xác minh; slots validation/confirmation; session-user binding; IAM; .NET fulfills và audit | M4 chính, M1 business, M3 identity, M2 infra | Tuần 8 có điều kiện |
| AWS deployment | Artifact build/test; IaC/state; private network + HTTPS entry; secrets/runtime IAM; DB migration/backup; health; egress AWS; logs/retention; CI deploy/rollback | M2 chính; module owners tự smoke | Baseline tuần 4, live AI tuần 5 |
| Final E2E demo | Approved-only selection; missing-slot live RAG; draft review/audit; snapshot; role accounts; reproducible seed; retry/alert; IaC/deploy evidence; no core P0/P1 | Cả 5 owner | Tuần 9–10 |

## 11. Top Blockers

Đây là **điều kiện cần xác minh/giải quyết**, không phải danh sách lỗi đã quan sát trong repository.

| # | Blocker/rủi ro chặn | Owner | Hạn xử lý | Nếu chưa giải quyết |
|---:|---|---|---|---|
| 1 | Chưa có file kế hoạch Markdown gốc/repository để audit | M1 + người giữ repo | Trước chốt backlog chính thức | Giữ audit N/A; dùng baseline, không tuyên bố completion |
| 2 | AWS account/Region/IAM/model access/quota | M2/M4, M3 review | Tuần 1 | Escalate account access; phát triển qua mock nhưng MVP vẫn cần live proof |
| 3 | Chưa chốt QuestionRevision/status/taxonomy/DTO | M1/M4/M3 | Tuần 1 | Đóng contracts trước generation/import/review |
| 4 | Thiếu corpus hợp lệ hoặc metadata nguồn | M4 | Tuần 1–2 | Thu hẹp môn/chủ đề; lấy bộ tài liệu nhỏ chất lượng, có quyền |
| 5 | Chưa chọn vector store hoặc chi phí nền RAG quá cao | M4/M2 | Tuần 1–2 | So phương án hỗ trợ theo Region và budget, không bỏ Bedrock RAG |
| 6 | Auth/resource permissions chưa chạy thật | M3 | Tuần 2–3 | Không mở anonymous staging hoặc coi UI-only permission là xong |
| 7 | Review state/concurrency/audit chưa atomic | M3/M4/M1 | Tuần 3–5 | Chặn finalize; fix invariant trước extension |
| 8 | Worker retry gây trùng dữ liệu hoặc job stuck | M5, M1/M2 handlers | Tuần 3–6 | Fix durable lease/idempotency và UI error, không retry vô hạn |
| 9 | Private backend thiếu egress/SSM/config/DB migration | M2, M3 | Tuần 2–5 | Verify traffic path/config, không public backend để né vấn đề |
| 10 | Data ML/locale Lex/năng lực và dung lượng extension không đủ | M5/M4 | Gate tuần 6–7 | Không mở extension; dùng tuần 7–8 cho core hardening |

## 12. P0/P1 Technical Issues

Chưa có mã nguồn nên **không có finding kỹ thuật được xác nhận**. Bảng sau là audit checklist ưu tiên; chỉ chuyển thành issue khi có path/line/request/test tái hiện.

| Priority | Điều kiện nếu phát hiện | Impact | Owner xử lý | Evidence cần để xác nhận |
|---|---|---|---|---|
| P0 | AI/teacher không quyền có thể approve; draft/rejected vào final exam | Vi phạm business/security cốt lõi | M3/M1/M4 theo boundary | API call + DB revision/snapshot + policy path |
| P0 | Hard-coded/exposed secrets hoặc DB/backend mở công khai trái thiết kế | Truy cập trái phép | M2/M3 + owner code | Config/IaC/workflow path đã redacted; network/access proof |
| P0 | IDOR cho questions/exams/jobs/citations, privilege escalation | Đọc/sửa nội dung ngoài quyền | M3 + endpoint owner | Hai accounts, request 200 trái policy, code path |
| P1 | Approval audit không atomic, revision approved bị edit tại chỗ | Không truy được người duyệt/nội dung đề thay đổi | M3/M4/M1 | Transaction + migration paths, concurrent integration test |
| P1 | Retry/idempotency/lease sai; timeout sau write tạo trùng hoặc job mất | Dữ liệu và chi phí không kiểm soát | M5 + handler owner | Job/attempt logs, unique constraints, crash/retry reproducer |
| P1 | Generation sai count/score/slot, duplicate question | Đề không đạt ma trận | M1 | Fixtures và selection/finalization code paths |
| P1 | Không validate AI schema/citations/source scope | Draft lỗi/nguồn sai hoặc lộ nội dung | M4/M3 | Model response fixture + validator/access test |
| P1 | Unhandled errors, swallowed exceptions, retry vô hạn, spinner không dừng | Không khôi phục được/demo fail | M5 + module owner | Stack/log/correlation path, UI/backend failure test |
| P1 | EF query N+1/thiếu index/blocking async hoặc transaction lớn gây timeout | Generation/import chậm, worker nghẽn | Owner query; M1 điều phối | Query plan/counts/latency measurements và file paths |
| P1 | CI/IaC/migration không tái lập, không backup/rollback evidence | Không deploy/handover an toàn | M2 | Workflow/IaC/runbook + fresh deploy/restore test |

Audit bổ sung P2/P3: domain leakage/circular dependencies, duplicate DTO/services/repositories, controller bloat, frontend state duplication, unused AWS code/dead code, docs drift. Không refactor lớn chỉ vì naming/style khi MVP còn thiếu. Ưu tiên fix issue ảnh hưởng invariant/testability/deploy.

## 13. AWS Review

Chưa xác minh tài nguyên AWS hiện có; mọi service dưới đây là **planned**, trừ proof phải thu thập trong các task. “Keep” nghĩa giữ trong thiết kế đề xuất, không khẳng định tài nguyên đã được tạo. M2 provision, module owner implement SDK integration, M3 review permissions.

| Service/tài nguyên | Requirement giải quyết / owner | Cấu hình/evidence phải có | Security/cost concern | Failure behavior | Keep / Remove / Optional |
|---|---|---|---|---|---|
| VPC, public/private subnets, route tables | Phân tách entry/app/DB; M2 | CIDR/routes/AZ/traffic diagram + IaC | CIDR overlap, route sai; không tự nhận HA khi một AZ | Fail health; diagnose routes | Keep |
| Internet Gateway | Public HTTPS entry/egress route nếu dùng; M2 | Attachment/routes | IGW không biến private backend thành public | Entry unavailable, alert | Keep theo network ADR |
| NAT Gateway | Outbound cho private app khi cần; M2 | Egress targets/volume/budget, routing evidence | Tính phí theo giờ và GB; dừng EC2 không dừng NAT billing | AWS calls fail; jobs retry bounded | Optional phương án egress; Keep nếu ADR cần |
| VPC endpoints | Private service access/S3 egress tối ưu khi hợp lý; M2/M3 | Service/Region support, endpoint policy/DNS | Interface endpoint có chi phí; không giả mọi endpoint rẻ hơn NAT | Provider unavailable; health UNKNOWN/DOWN | Optional theo cost/traffic |
| EC2 + storage | .NET API/worker/public entry theo ADR; M2 | Roles, user data/service manager, disk, release/version | Port public, storage/IPv4/instance cost, capacity | Host alarm, worker lease recover, restore | Keep |
| SG / NACL | Traffic least access; M2, M3 review | SG-to-SG rules và traffic matrix; NACL review | NACL stateless/ephemeral return paths; không tạo rule phức tạp vì service count | Reachability/Flow Logs diagnosis | Keep SG; NACL default hoặc rules có lý do |
| IAM | Runtime/CI/KB/model access; M3 policy, M2 attach | Resource-scoped roles/policies, trust policy | Wildcard rộng, long-lived keys | AccessDenied actionable; không tự tăng quyền | Keep |
| SSM Session Manager | Quản EC2 không mở SSH inbound; M2 | Agent/role/connectivity, access evidence | Session permissions/log content; network egress cần đúng | Node offline; runbook xác định agent/route/IAM | Keep |
| S3 | Import/corpus/artifacts/backups; M2 infra, M4 corpus, M5 model | Private buckets/prefixes/version/retention/access | Public bucket/unsafe URLs, orphan objects/storage cost | Import/ingestion FAILED/PARTIAL | Keep |
| Bedrock models + Knowledge Bases | Retrieval và AI draft có nguồn; M4, M1 orchestrates | Model/embedding/KB/vector store/Region/IAM, source/version/citations | Token/index/embedding cost; untrusted retrieval | No source → không bịa completion; failure/review state | Keep MUST |
| Vector store cho KB | Lưu embeddings phục vụ retrieval; M4/M2 | Một backend hỗ trợ Region và budget được chốt | Chi phí nền, access/index security; không thêm OpenSearch chỉ để tăng service | Retrieval failed/empty; draft blocked | Keep capability; chọn backend sau proof |
| Bedrock Guardrails | Lọc nội dung/injection theo policy; M3/M4 | Policy/action/version và test fixtures | Không thay authorization/validation; references không được bảo vệ toàn bộ bởi generation guardrail | BLOCKED/REVIEW_REQUIRED với lý do | SHOULD cho cấu hình nâng cao |
| CloudWatch Logs/Metrics/Alarms | App/host/provider observability; M2 infra, M5 app, owners emit | Namespace/dimensions/retention/alarms/dashboard | Excess retention/cardinality; không log secret/full prompt mặc định | Alert và job state rõ; không chỉ log rồi nuốt lỗi | Keep MUST |
| SNS | Alert cần hành động; M5 routing, M2 IaC | Topic/policy/subscription confirmation/delivery proof | Spam/PII và chưa nhận delivery | Record delivery failure/runbook alternate channel | Keep baseline |
| VPC Flow Logs | Điều tra network deny/reachability; M2 | Destination/IAM/scope/retention | Volume/log storage cost và metadata access | Diagnose ACCEPT/REJECT, không thay app logs | Keep baseline workshop có mục đích |
| Reachability Analyzer | Phân tích cấu hình đường network; M2 | Paths/analysis evidence | Analysis cost; không thay live app smoke | Report blocked component | SHOULD |
| Lex V2 | Thu intent/slots để tạo blueprint; M4 | Locale/bot/version/alias/session/IAM | Per-request cost, locale support, cross-user sessions | Fallback form; .NET business validation | SHOULD |
| SageMaker AI | Difficulty evaluation/advisory; M5 | Dataset/model card/train/eval/endpoint/adapter/cleanup | Idle real-time endpoint cost, data leakage, model chất lượng kém | UNAVAILABLE advisory; core vẫn chạy | SHOULD |
| Site-to-Site VPN | Kết nối legacy Question Bank thật nếu có; M2 | Customer gateway/routes/CIDR/dataset mapping/demo | Tunnel cost, complex routes, fake hybrid demo | Legacy sync fail rõ; core không phụ thuộc | STRETCH; bỏ nếu không có use case |
| IaC: Terraform/CloudFormation/CDK | Reproducible infrastructure; M2 | Chọn một; state/access/variables/modules/outputs | State chứa sensitive values, drift | Plan errors block deploy, không console patch tùy tiện | Keep MUST, không dùng cả ba |
| Parameter Store SecureString hoặc Secrets Manager | Runtime secret/config delivery nếu app cần; M2/M3 | Chọn phương án, IAM, rotation/retention phù hợp | Đừng thêm cả hai nếu không có nhu cầu; secret logs/state | Startup/readiness fail rõ, không dùng credential fallback hard-coded | Chọn một khi cần |
| ALB / RDS | Managed entry/DB nếu ADR và budget hỗ trợ; M2 | HTTPS/health hoặc backup/DB subnet/SG | Chi phí cố định/managed overhead với demo nhỏ | Health/restore behavior rõ | Optional có lý do; không tự thêm vào MUST |
| Kubernetes/microservices không cần thiết | Không có requirement từ nguồn | Không provision | Complexity và scope nở | Không áp dụng | Remove khỏi baseline |

Không báo giá USD cố định khi Region/cấu hình chưa chốt. Cost sheet phải tính compute/storage/IPv4, NAT hoặc endpoints, KB vector-store baseline, embeddings/tokens, logs, training/endpoint và thời gian tồn tại tài nguyên. Teardown plan phân biệt tài nguyên dừng được với tài nguyên cần xóa để ngừng billing, giữ backup/evidence cần bàn giao.

Các lưu ý đã đối chiếu tài liệu AWS chính thức:

- `RetrieveAndGenerate` có thể trả citations; Guardrails áp dụng lên input/generated response, không bao phủ references retrieved tại runtime. Vì vậy app phải tự kiểm source access và reviewer kiểm grounding. [Bedrock Knowledge Bases](https://docs.aws.amazon.com/bedrock/latest/userguide/kb-test-retrieve-generate.html).
- Lex V2 có thể trả intent/slots cho client application fulfillment; kế hoạch dùng .NET kiểm nghiệp vụ, không bắt buộc thêm Lambda chỉ để chuyển tiếp. [Lex core concepts](https://docs.aws.amazon.com/lexv2/latest/dg/how-it-works.html).
- NAT Gateway có khoản theo giờ và dữ liệu; so sánh egress/endpoints theo traffic thực tế trước khi giữ tài nguyên cả 10 tuần. [Amazon VPC pricing](https://aws.amazon.com/vpc/pricing/).
- Session Manager không yêu cầu mở inbound port trên instance cho kết nối SSM; role/agent/service connectivity vẫn phải đúng. [Systems Manager connectivity](https://docs.aws.amazon.com/systems-manager/latest/userguide/setup-create-vpc.html).
- Real-time SageMaker endpoint cần được xóa khi không dùng để ngừng phí endpoint; endpoint configuration/model artifacts có vòng đời riêng. [Delete SageMaker endpoints](https://docs.aws.amazon.com/sagemaker/latest/dg/realtime-endpoints-delete-resources.html).
- Bedrock KB cần vector-store setup tương thích; phải đưa backend lưu vector vào architecture và cost sheet. [Knowledge Base vector-store prerequisites](https://docs.aws.amazon.com/bedrock/latest/userguide/knowledge-base-setup.html).

## 14. MVP

**Phạm vi MVP hiện hành:** nhiều trường và kỳ thi cấp Bộ theo ADR 0004; một loại câu hỏi MCQ một đáp án đúng, một CSV format, một frontend và .NET modular monolith + durable workers trên AWS. Demo phải chứng minh cô lập ít nhất hai trường và hai tài khoản Bộ độc lập; các hạng mục cũ bên dưới cần re-estimate.

1. Login/logout/session expiry; bốn role trong permission matrix; server-side authorization theo trường/bộ môn/kỳ thi và user-role management tối thiểu.
2. Question Bank CRUD, subject/topic/difficulty metadata, search/filter/pagination, immutable revisions/history, edit approved tạo draft mới.
3. CSV upload → validation preview → commit → row results; imported questions là DRAFT; retry không tạo trùng.
4. Blueprint CRUD/UI ma trận; count/score/taxonomy validation.
5. Generate từ approved revisions, không trùng question, đúng ma trận; phát hiện missing slots với lý do/count cụ thể.
6. Live Bedrock RAG từ corpus S3/KB đã ingest; generate AI drafts có metadata/provenance/citations và schema validation.
7. AI chỉ DRAFT/REVIEW_REQUIRED; human reviewer approve/reject với reason, concurrency check và audit. AI không tự approve/publish.
8. Reassembly/finalize sau review; final preview dùng snapshot bất biến, chỉ approved revisions; quyền xem answer key theo role.
9. Durable jobs/status UI, timeout/bounded retry/idempotency/recovery; partial/failure states rõ.
10. Audit trail tra được actor/action/revision/resource/time/correlation ID; business approval audit atomic.
11. AWS deploy có private app/DB, HTTPS entry, IAM role/secret delivery, SSM; IaC/CI build/test/deploy và runbook rollback/backup/restore.
12. CloudWatch app/infra monitoring, một baseline SNS alert có delivery evidence; Flow Logs cho một network diagnostic case có mục đích.
13. Unit/integration/security/E2E tests theo flow và invariants; source/prompt/dataset/deploy versions; đủ README/user/runbooks/demo evidence.

MVP **không phụ thuộc** SageMaker, Lex, VPN, ALB/RDS, auto-scaling hoặc hệ thống thi trực tuyến cho học sinh. Bedrock RAG không bị cắt khỏi MVP; nếu access chưa chạy, project vẫn chưa đạt scope này. Không thay live evidence bằng mock và tuyên bố hoàn thành.

Baseline nghiệm thu đề xuất: 10 scenarios E2E ở mục 17; 100% mutation APIs có authorization test; critical state/selection/idempotency invariants pass; không còn core P0/P1. Performance target và data volume chốt tuần 1 rồi ghi điều kiện đo, không tự hứa đáp ứng tải production.

## 15. Should Have

| Item | Owner | Điều kiện mở / tiêu chí giữ |
|---|---|---|
| Lex V2 teacher assistant | M4, M1 contracts | MVP gate đạt, locale/region khả dụng, có fallback form, test permissions |
| SageMaker difficulty advisory | M5 | Labels/split/evaluation đủ evidence, model acceptance, endpoint budget/cleanup, không auto-approve |
| Bedrock Guardrails nâng cao | M3/M4 | Baseline app validation đã chạy, benchmark có mục tiêu và policy action visible |
| Reachability Analyzer demo | M2 | Có đường network cần kiểm và ngân sách; giữ live smoke riêng |
| PDF/export đề đơn giản | M1 | Snapshot/preview ổn định, còn capacity; không nằm baseline 50 gói |
| RAG evaluation UI sâu hơn / dashboard mở rộng | M4/M5 | Không kéo dài core; metrics không che thiếu human review |
| Managed DB/ALB | M2 | ADR xác định lợi ích vận hành và budget, không làm lại core chỉ vì thêm service |

Chỉ mở SHOULD khi core gate đạt; estimate 926 giờ không bao gồm mọi SHOULD trong bảng này. Lex và ML có sẵn 18 giờ/member ở tuần 8 và ML evaluation tuần 7; export/managed migration/nâng cao cần đổi scope hoặc bổ sung effort riêng.

## 16. Stretch

- Site-to-Site VPN với legacy Question Bank có dữ liệu thật và mapping/sync/audit rõ; bỏ nếu chỉ tạo tunnel để tăng service count.
- Multi-tenant SaaS, nhiều trường, billing, subscription.
- Hệ thống học sinh làm bài, chấm điểm, proctoring; không thuộc Exam Bank MVP.
- Essay/multi-answer/image-rich questions hoặc OCR/PDF/Excel ingestion nhiều format.
- Advanced item response theory/adaptive testing/difficulty học từ kết quả học sinh; cần dữ liệu và thiết kế riêng.
- Auto-scaling/multi-AZ HA/DR automation khi có yêu cầu vận hành và ngân sách cụ thể.
- Bedrock fine-tuning/agents, reranking nâng cao hoặc đổi vector backend không dựa benchmark.

Không tự triển khai STRETCH trong 10 tuần baseline. Không đưa RBAC, Question Bank, blueprint, Bedrock RAG, review, audit, tests, AWS deploy, monitoring hoặc IaC vào STRETCH.

## 17. Final E2E Demo Path

**Demo core 15–20 phút đề xuất từ baseline cũ**; bản MVP mới cần thêm hai trường độc lập và một kỳ thi cấp Bộ có hai `MinistryAdmin` khác nhau soạn/xác nhận. Corpus/seed version cố định, một blueprint đủ và một blueprint thiếu. Tài nguyên AWS đang hoạt động và mọi bước có job/revision/correlation ID để truy vết.

1. **M3:** Teacher login; thử gọi approve API khi không quyền và nhận 403. Giới thiệu DepartmentHead/SchoolAdmin/MinistryAdmin theo scope; chứng minh không đọc được dữ liệu trường khác hoặc đề Bộ chưa được giao.
2. **M4:** Teacher tạo/sửa/tìm câu hỏi; xem metadata và revision history. Tạo revision mới của câu đã approved, chứng minh draft mới không tự được duyệt.
3. **M2:** Upload CSV có dòng hợp lệ/lỗi; preview chỉ ra lỗi; commit tạo draft; commit/retry lặp không tăng số câu.
4. **M3:** Reviewer mở pending manual/imported draft, approve/reject có reason; audit ghi actor và revision.
5. **M1:** Blueprint đủ câu → generate đúng count/score, chỉ chọn approved, không trùng question.
6. **M1/M4:** Blueprint thiếu slot → gap report → live Bedrock RAG tạo draft; mở sources/citations/prompt provenance; exam ở NEEDS_REVIEW.
7. **M3/M4:** Reviewer kiểm đáp án/nguồn; reject một draft để chứng minh không tự đưa vào đề, approve drafts phù hợp và xử lý phần còn thiếu theo workflow.
8. **M1:** Reassemble và finalize; final preview/snapshot đúng ma trận. M4 sửa câu nguồn sau finalize; preview final không đổi nội dung/version.
9. **M5/M2:** Trigger một lỗi có kiểm soát; job UI báo reason/attempt, bounded retry/recovery; CloudWatch metric/alarm và SNS delivery evidence, health UI phân biệt host/app status.
10. **M3/M2:** Trace audit Teacher→RAG→Reviewer→FinalExam; show IaC/pipeline/deploy/restore evidence và các test reports của release tag.

Lex và SageMaker demo riêng sau core nếu đạt gate: Lex thu slots rồi .NET xác nhận/validate tạo blueprint; ML chỉ gợi ý difficulty kèm model version/metrics; tắt cả hai rồi core vẫn chạy. VPN chỉ demo khi được mở STRETCH với use case hybrid thực.

E2E tests nên bám 10 scenarios trên và có negative cases cho permissions, review stale revision, source access, worker restart và duplicate submit. Không cần test chỉ kiểm copy text hoặc mirror private implementation.

## 18. Next 10 Tasks

Đây là thứ tự khởi động backlog, chưa phải lệnh triển khai. Các task cùng tuần có thể làm song song sau contracts liên quan; giữ dependency gates. DoD chung mục 9 vẫn áp dụng.

| Ưu tiên | Task ID / owner | Task / dependency | Concrete output | Definition of Done |
|---:|---|---|---|---|
| 1 | M1-01 / M1 | Xác minh repo nếu có; architecture/schema/contracts; phụ thuộc file nguồn/repo để audit | ADR/module map/ERD/state/error contracts | 5 owners thống nhất invariant và module boundaries; audit claims có evidence hoặc N/A |
| 2 | M4-01 / M4 | Question schema/taxonomy/corpus; phụ thuộc M1-01 đang chốt | QuestionRevision schema, seed/corpus manifest, model/KB access proof | Metadata/MCQ/versions có validation cases; corpus có quyền dùng; AWS access rõ |
| 3 | M3-01 / M3 | Auth/permission/review threat model; phụ thuộc M1-01 | Permission matrix, identity ADR, Review/Audit contracts | Mọi mutation có policy; AI không approve; self-review rule chốt |
| 4 | M2-01 / M2 | Network/deploy/cost/import design; phụ thuộc M1-01 và AWS access | Network diagram, IaC ADR/cost sheet, CSV/API/UI contract | Private app/DB + HTTPS entry/egress rõ; import preview/commit semantics được M4/M5 đồng ý |
| 5 | M5-01 / M5 | Job/retry/telemetry contracts; phụ thuộc M1-01, M2-01 | Job state/failure matrix, idempotency spec, status UI design | Duplicate/timeouts/crash có recovery behavior; M1/M2 dùng chung contract |
| 6 | M2-02 / M2 | Provision private AWS baseline; phụ thuộc M2-01, IAM contract M3-01 | IaC VPC/EC2/SSM/S3, DeploymentRecord/health skeleton | IaC repeatable; SSM dùng được; DB/app không public; evidence redacted |
| 7 | M3-02 / M3 | Auth/RBAC/UI user management; phụ thuộc M3-01, schema | Login/logout/admin UI/API và user/role migrations | Direct API 401/403/IDOR tests pass; permissions enforced server |
| 8 | M4-02 / M4 | Question CRUD/search/editor; phụ thuộc M4-01, auth integration | Domain writer/API/UI/migrations/indexes | Create/edit/filter roundtrip; revision mới DRAFT; invalid MCQ rejected |
| 9 | M5-02 / M5 | Durable worker/status UI; phụ thuộc M5-01, DB/auth contract | Job/Attempt migrations, worker, status APIs/UI | Sample job complete; restart không mất job; hai workers không commit duplicate |
| 10 | M1-02 / M1 | Blueprint CRUD/matrix; phụ thuộc M1-01, M4-01, auth | Blueprint/Slot migration/API/editor/fixtures | Counts/points/taxonomy validated; reload giữ constraints; unauthorized/stale edits bị chặn |

**Điểm dừng:** chỉ lập kế hoạch và phân công; chưa triển khai/refactor mã nguồn hoặc tạo/thay đổi AWS resources. Khi đội cung cấp Markdown nguồn và repository, hiệu chỉnh backlog từ bằng chứng thực tế rồi duyệt ownership/scope/capacity trước triển khai lớn.
