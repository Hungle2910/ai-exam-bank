# M3-02: chức năng Identity/RBAC đã làm

## Đã có trong backend

| Chức năng | Đường dẫn hoặc cách dùng | Mục đích |
|---|---|---|
| Đăng nhập/đăng xuất | `POST /auth/login`, `POST /auth/logout` | Dùng ASP.NET Core Identity và cookie phiên; mật khẩu được hash bởi Identity. |
| Xem phiên và lấy CSRF | `GET /auth/me`, `GET /auth/csrf` | Lấy tài khoản/role hiện tại và token chống CSRF cho thao tác ghi. |
| Giới hạn đăng nhập | 5 lần/phút theo IP | Giảm thử mật khẩu liên tục; lần vượt mức trả `429`. |
| Thời hạn phiên | `Auth:SessionLifetimeSeconds` | Mặc định 1800 giây; có thể đặt ngắn cho bài thử hết hạn. |
| Trường/bộ môn | `GET/POST /api/v1/admin/schools`, `GET/POST /api/v1/admin/schools/{schoolId}/departments` | MinistryAdmin tạo trường; SchoolAdmin chỉ quản lý bộ môn trường mình. |
| Tài khoản | `GET/POST /api/v1/admin/schools/{schoolId}/users` | MinistryAdmin quản lý SchoolAdmin; SchoolAdmin quản lý Teacher/DepartmentHead cùng trường. |
| Role | `GET /api/v1/admin/schools/{schoolId}/role-assignments`, `POST /api/v1/admin/users/{userId}/role-assignments`, `DELETE /api/v1/admin/role-assignments/{assignmentId}` | Cấp/thu hồi role theo quyền và phạm vi thật trong database; không được tự nâng quyền. |
| Audit role | `RoleAssignmentAudits` | Lưu người thao tác, người nhận, role, scope và thời điểm cùng giao dịch cấp/thu hồi. |
| Chính sách tài nguyên trường | `ResourceAccessPolicy` và `ResourceAuthorizationService` | Kiểm quyền Question/Blueprint theo chủ sở hữu, trường, bộ môn và trạng thái đã gửi duyệt; Job theo người yêu cầu và role còn hiệu lực. Scope/trạng thái phải lấy từ tài nguyên đã lưu trong DB. |
| MinistryAdmin đầu tiên | Lệnh `--bootstrap-ministry-admin` | Tạo một lần ở backend khi chưa có MinistryAdmin; không có API công khai để tự cấp quyền. |

Các thao tác POST/DELETE yêu cầu token `X-CSRF-TOKEN`. Cookie được đặt HttpOnly, SameSite Lax và Secure ngoài môi trường Development. API trả `401` khi chưa đăng nhập và `403` khi không có quyền.

## Tạo MinistryAdmin đầu tiên

Chỉ chạy sau khi đã áp dụng EF migrations lên đúng database. Từ thư mục Backend, chạy `dotnet ef database update --project Persistence/AiExamBank.Persistence --startup-project Api` với `ConnectionStrings__DefaultConnection` trỏ tới database đích. Cấp hai biến môi trường `AIEXAMBANK_BOOTSTRAP_USERNAME` và `AIEXAMBANK_BOOTSTRAP_PASSWORD` bằng cơ chế secret của môi trường chạy. Sau đó chạy lệnh từ thư mục `Api`:

```powershell
dotnet run --no-launch-profile -- --bootstrap-ministry-admin
```

Lệnh không mở web server, không in mật khẩu, tạo tài khoản + role + audit trong một transaction, và từ chối chạy lần thứ hai khi đã có MinistryAdmin. Không đặt mật khẩu trong `appsettings.json`, tham số dòng lệnh hoặc Git. Sau khi chạy, xóa biến mật khẩu khỏi phiên shell nếu đã đặt thủ công.

## Giao diện và cấu hình phát triển

Frontend có trang đăng nhập/đăng xuất, màn Trường học/Bộ môn, Người dùng và Phân quyền. Bộ/Trường/Giáo viên chỉ thấy các thao tác phù hợp với vai trò; BE vẫn kiểm quyền cho mọi yêu cầu trực tiếp. Giao diện chọn bộ môn khi tạo hoặc gán quyền Giáo viên/Trưởng Bộ Môn, và chuyển về trang đăng nhập với thông báo khi API quản trị trả `401` sau khi phiên hết hạn. Hướng dẫn chạy BE bằng Visual Studio và FE bằng VS Code nằm trong `Frontend/README.md`; `.env.local` chỉ lưu địa chỉ BE tại máy phát triển và không được commit.

## Kiểm tra đã thực hiện

- `npm run build` cho Frontend thành công; `dotnet test AiExamBank.slnx --no-build` đạt 64/64 bài. API build đã đạt ở lần kiểm tra trước; khi Visual Studio đang chạy API, tránh build đè tệp thực thi bị khóa.
- Script auth smoke xác nhận anonymous `401`, thiếu CSRF `400`, sai mật khẩu `401`, đăng nhập `204`, `/auth/me` `200`, đăng xuất `204` rồi `/auth/me` `401`. Script expiry smoke với phiên 2 giây xác nhận sau 4 giây `/auth/me` trả `401`.
- Test giao diện `Frontend/src/app/page.test.tsx` dựng trang thật với `AuthProvider` và `AppShell`, giả lập `/auth/me` chuyển từ `200` sang `401`, rồi xác nhận quay về trang đăng nhập và hiện thông báo phiên hết hạn. `npm run test:unit` đạt 9/9 bài; `npm run build -- --webpack` đạt trên worktree dùng chung `node_modules`. Đây là kiểm thử UI với phản hồi HTTP giả lập, chưa phải diễn tập trình duyệt với backend/database thật.
- Trên database thử riêng `AiExamBankM3Smoke`, bootstrap MinistryAdmin một lần; tạo hai trường A/B, tài khoản SchoolAdmin A và Teacher thuộc bộ môn A. Trên UI, Bộ thấy hai trường, SchoolAdmin A chỉ thấy A, Teacher không thấy menu quản trị. Yêu cầu API trực tiếp của SchoolAdmin A đến trường B và của Teacher đến `/api/v1/admin/schools` đều trả `403`; tài khoản không đăng nhập trả `401`.
- SchoolAdmin A tạo Teacher, gán rồi thu hồi vai trò Trưởng Bộ Môn thành công trên UI. Các kiểm tra API thủ công trước đó cũng xác nhận không thể tự nâng lên MinistryAdmin hoặc tạo người dùng ở trường B (`403`). Database thử vẫn giữ lại để kiểm tra tiếp; không chứa mật khẩu trong Git.

## Phụ thuộc tích hợp

Blueprint (M1-02, PR #97) và Question (M4-02, issue #9) đã vào `dev`. Blueprint dùng truy vấn SQL ràng buộc chủ sở hữu, trường, bộ môn và vai trò Teacher còn hiệu lực; Question áp dụng `ResourceAccessPolicy` của M3 cho đọc/sửa và lọc danh sách trước phân trang. `Backend/tests/blueprint-smoke.ps1` hiện bao gồm cả hai API: kiểm truy cập cùng bộ môn khác chủ, khác bộ môn, khác trường, ẩn danh, danh sách không lộ tài nguyên, và thu hồi Teacher trên cùng phiên đăng nhập. Script chỉ được chạy trên SQL Server thử nghiệm riêng với tài khoản bootstrap của môi trường đó.

**Bằng chứng bổ sung ngày 17/11/2026:** áp toàn bộ EF migrations và bootstrap MinistryAdmin trên database thử riêng `AiExamBankM3W02_20261117044809` ở SQL Server cục bộ; chạy API thực và `Backend/tests/blueprint-smoke.ps1` đạt toàn bộ ca, kết thúc bằng `M3_W02_SMOKE_PASSED`. Mọi actor ngoài scope nhận 404 khi đọc/sửa theo ID; danh sách không chứa tài nguyên của người khác; sau thu hồi Teacher, chính cookie cũ nhận 404 khi đọc/sửa và 403 khi tạo mới trên cả Blueprint/Question. Ẩn danh nhận 401. Cú pháp PowerShell sạch, API build không cảnh báo/lỗi, và 9/9 bài Identity unit test đạt. Database này chỉ là bằng chứng local, chưa phải staging/AWS. Script giữ lại fixtures để có thể đối chiếu, không chứa mật khẩu trong Git.

Jobs (M5) vẫn dùng lược đồ Bearer `JobEngineAuth` với `UserId`/`ScopeId` dạng chuỗi và token cấu hình riêng. Chưa ánh xạ an toàn từ `AppUser.Id`/`RoleAssignments` hiện tại, nên chưa thể coi Job API đã tích hợp RBAC M3. M3 và M5 cần chốt danh tính, phạm vi lấy từ tài nguyên lưu trong DB, quy tắc thu hồi và quyền của service identity trước khi thêm kiểm thử Job qua phiên Identity. Bằng chứng staging/AWS chờ môi trường và cấu hình runtime từ M2. Quyền kỳ thi cấp Bộ cần bảng gán người tham gia từng kỳ thi; chính sách trường hiện tại mặc định từ chối phạm vi này.
