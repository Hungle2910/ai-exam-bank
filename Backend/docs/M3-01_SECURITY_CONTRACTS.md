# M3-01 — Hợp đồng bảo mật, thiết kế và bằng chứng tuần 1

**Owner:** M3 / Minh Phúc. **Trạng thái:** bản bàn giao để M1/M3 và nhóm review; phân biệt rõ phần đã chạy với phần còn là hợp đồng thiết kế. Đọc cùng [bảng quyền](IDENTITY_AND_PERMISSIONS.md) và [ADR phiên đăng nhập](adr/0006-identity-session-and-authorization.md).

## Ranh giới danh tính và dữ liệu

`AppUser` (`AspNetUsers`) có ID ổn định và `SchoolId` nhà; `RoleAssignment` chứa `UserId`, role, `SchoolId`, `DepartmentId`. Trường/bộ môn thuộc về tài nguyên phải đọc từ DB. Một tài khoản có nhiều role nhưng chỉ role được gán **đúng scope** mới dùng được cho hành động đó. Sau thu hồi, quyền được đọc lại từ DB ở lần gọi tiếp theo. Schema đã triển khai cho user, school, department, role và `RoleAssignmentAudits` được mô tả trong [M3-02 handoff](M3-02_identity_schema_handoff.md); SQL Server là lựa chọn đang dùng, còn ADR 0003 cần M1 cập nhật/chấp thuận.

Review là hợp đồng **chưa có bảng/migration/API**. Đề xuất `ReviewDecision(Id, RevisionId, ReviewerUserId, Decision, Reason, ExpectedVersion, DecidedAtUtc, SchoolId/ExamEventId)` và `AuditEvent(Id, ActorUserId, Action, TargetType, TargetId, Scope, RevisionVersion, OccurredAtUtc, CorrelationId)`. `Reason` bắt buộc khi reject; `ReviewerUserId` phải lấy từ phiên, không tin body. M4/M1 chốt khóa tham chiếu Revision và chủ sở hữu; M3 chốt policy; M5 dùng correlation ID. `ReviewDecisionRequest` hiện mới là contract C#, không phải endpoint. Quyết định, trạng thái revision, con trỏ lựa chọn của `ExamEvent` và audit phải ghi cùng một transaction với kiểm tra `ExpectedVersion`. Cần unique/concurrency guard để chỉ một phiên bản được chọn cho một event. Không tạo migration Review trước khi M1/M4 chốt schema chung.

## Endpoint và policy

| Route/nhóm | Trạng thái | Actor, scope và kiểm tra tài nguyên |
|---|---|---|
| `POST /auth/login`, `POST /auth/logout`, `GET /auth/me`, `GET /auth/csrf` | Đã có | Login dùng Identity, giới hạn thử; mutation có CSRF. Logout/me yêu cầu phiên; `/auth/me` lấy assignments hiện tại. |
| `GET/POST /api/v1/admin/schools` | Đã có | MinistryAdmin quản lý trường; SchoolAdmin chỉ xem trường được gán. POST cần CSRF. |
| `GET/POST /api/v1/admin/schools/{schoolId}/departments` | Đã có | MinistryAdmin hoặc SchoolAdmin của đúng trường; kiểm tra school thực, không lấy quyền từ ID URL. POST cần CSRF. |
| `GET/POST /api/v1/admin/schools/{schoolId}/users` | Đã có | SchoolAdmin quản lý Teacher/DepartmentHead trường mình; MinistryAdmin quản lý SchoolAdmin. POST cần CSRF. |
| `GET /api/v1/admin/schools/{schoolId}/role-assignments`, `POST /api/v1/admin/users/{userId}/role-assignments`, `DELETE /api/v1/admin/role-assignments/{assignmentId}` | Đã có | Đọc assignment/target từ DB; cấm tự cấp/thu hồi, vượt trường, gán MinistryAdmin hoặc scope không hợp lệ. Mutation cần CSRF và audit cùng giao dịch. |
| Question/Blueprint read, edit, review (đường dẫn do M4/M1 chốt) | Policy đã có; API tài nguyên chưa có | Sau khi tải tài nguyên thật, Teacher chỉ xem/sửa bản của mình trong trường–bộ môn; DepartmentHead xem bản đã nộp đúng scope và không tự review; admin không mặc nhiên xem nội dung. List/search/download dùng cùng filter. |
| Job của người yêu cầu (API M5) | Policy trường đã có; chưa tích hợp với DB resource của M3 | Chỉ requester có assignment còn hiệu lực được đọc theo `ResourceAccessPolicy`; Job engine hiện dùng bearer riêng nên M5/M3 phải thống nhất identity/scope khi tích hợp, không coi bearer là role người dùng. |
| Review decision/audit read, exam selection (M3/M1/M4) | Chưa có API/persistence | Human reviewer có assignment đúng resource/event; cấm self-review và AI/worker/ops; kiểm tra revision hiện hành và version; atomic audit/selection. Audit list lọc theo scope, không lộ answer key. |
| Ministry exam author/confirm | Chưa có event assignment | Chỉ hai MinistryAdmin **khác nhau** được gán vào đúng event; không dựa vào role toàn cục. Mặc định từ chối trước khi có bảng gán và test. |

`401` khi không có phiên, `403` khi thiếu quyền; với tài nguyên ngoài scope, endpoint có thể dùng `404` nhất quán để không tiết lộ sự tồn tại. `409` cho revision/version hoặc selection conflict. Phân biệt lỗi nhập liệu `400` với lỗi quyền. Bảng này là hợp đồng tích hợp cho các route tương lai, không phải cam kết các route đó đã chạy.

## Threat model và ca kiểm thử

| Đe dọa/đường tấn công | Kiểm soát bắt buộc | Bằng chứng hiện có / cần thêm |
|---|---|---|
| IDOR qua ID trường, user, assignment, Question/Blueprint/Job | Tải bản ghi thật, đối chiếu actor với scope DB; list lọc cùng policy | Admin API và `ResourceAccessPolicyTests`; direct API IDOR cho Question/Blueprint/Job đợi API + persistence thật. |
| Tự nâng quyền, role cũ trong cookie | Cấm self-grant/Ministry grant; DB assignment hiện tại; audit grant/revoke cùng transaction | `SchoolRoleAssignmentPolicyTests`, M3-02 handoff; thử revoke khi cookie còn sống ở integration. |
| CSRF trên cookie mutation | Antiforgery token `X-CSRF-TOKEN`, cookie SameSite, kiểm tra origin khi triển khai khác origin | API cấu hình và UI AuthContext; cần CI/API test thiếu/sai token. |
| XSS lấy phiên hoặc kích hoạt thao tác | HttpOnly; React escape mặc định; tránh unsafe HTML, CSP khi deploy | UI hiện có; cần review mọi nơi render HTML/Markdown và CSP của môi trường. |
| Người soạn tự duyệt / AI, worker, ops duyệt | Tách actor human, so creator ID, assignment đúng scope, không cấp approve cho service identity | `ResourceAccessPolicyTests`; review endpoint, Ministry event tests còn chờ tài nguyên. |
| Revision cũ hoặc hai reviewer chọn đồng thời | Revision bất biến, `ExpectedVersion`, unique/concurrency guard; transaction state + pointer + audit | Contract đã có `ExpectedVersion`; cần DB/API test `409`, rollback, one-winner khi M1/M4 tạo resource. |
| Rò secret, answer key hoặc audit không đầy đủ | Secret qua SSM/KMS đúng role, không commit/log secret; audit tối thiểu và cùng transaction | M2 policy draft; cần AWS IAM simulation/live allow-deny và integration transaction. |

## Wireframe/luồng UI

- [LoginPage](../../Frontend/src/components/pages/LoginPage.tsx): nhập tài khoản/mật khẩu, lỗi `401`/`429`; [AuthContext](../../Frontend/src/contexts/AuthContext.tsx) gọi login, `/auth/me`, CSRF và logout.
- [UsersPage](../../Frontend/src/components/pages/UsersPage.tsx) và [RolesPage](../../Frontend/src/components/pages/RolesPage.tsx): danh sách theo trường, tạo user, cấp/thu hồi role; nút chỉ là tiện ích UI, backend vẫn chặn quyền.
- [ReviewPage](../../Frontend/src/components/pages/ReviewPage.tsx) và [AuditPage](../../Frontend/src/components/pages/AuditPage.tsx): wireframe cho hàng chờ, quyết định và lịch sử. Hiện dùng mock/fallback; chưa chứng minh review/audit API hay phân quyền nội dung đã hoạt động.

## AWS handoff và điều kiện nghiệm thu

M3 đề xuất ba ranh giới: human browser session chỉ gọi API; app runtime đọc đúng secret ứng dụng, không sửa IAM/duyệt đề; DB runtime đọc đúng secret DB; CI/Terraform chỉ dùng role triển khai giới hạn tài nguyên/state/PassRole, không đọc secret value. Bootstrap user dùng secret channel, không ghi password vào Git hoặc CLI argument. M2 đã chuẩn bị [IAM/KMS policy drafts](../infrastructure/security/README.md) với role/profile tách biệt, SSM SecureString + CMK riêng, và nêu rõ chưa attach/kiểm chứng trên AWS. M2 và account admin phải xác nhận principal/ARN thực, IAM simulation và live allow/deny trước deploy; không coi draft là bằng chứng AWS live.

Đã có role matrix và các policy/unit tests. Chưa thể nghiệm thu các đường duyệt Question/Blueprint/Exam hoặc Ministry event bằng API vì M1/M4/M5 chưa có đủ tài nguyên và endpoint tích hợp; các mục đó cần issue/PR riêng. M1 cần review ADR 0006 và cập nhật ADR 0003 theo SQL Server. Thời gian thực tế và peer review của M3-01 do chủ task/reviewer xác nhận; không suy ra từ estimate 16 giờ. Khi tích hợp, thêm direct API allow/deny, self-review, `409` cạnh tranh, rollback audit, và test không dùng được quyền cũ sau revoke trước khi đánh dấu các tiêu chí đó hoàn tất.
