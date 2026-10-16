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

Chỉ chạy sau khi đã áp dụng EF migrations lên đúng database. Từ thư mục gốc repository, chạy `dotnet ef database update --project src/Persistence/AiExamBank.Persistence --startup-project src/Api` với `ConnectionStrings__DefaultConnection` trỏ tới database đích. Cấp hai biến môi trường `AIEXAMBANK_BOOTSTRAP_USERNAME` và `AIEXAMBANK_BOOTSTRAP_PASSWORD` bằng cơ chế secret của môi trường chạy. Sau đó chạy lệnh từ thư mục `src/Api`:

```powershell
dotnet run --no-launch-profile -- --bootstrap-ministry-admin
```

Lệnh không mở web server, không in mật khẩu, tạo tài khoản + role + audit trong một transaction, và từ chối chạy lần thứ hai khi đã có MinistryAdmin. Không đặt mật khẩu trong `appsettings.json`, tham số dòng lệnh hoặc Git. Sau khi chạy, xóa biến mật khẩu khỏi phiên shell nếu đã đặt thủ công.

## Đã kiểm tra và còn lại

Build API sạch; 20 bài MSTest hiện có đạt (gồm 4 ca chính sách tài nguyên); model EF khớp migrations. Các luồng quản trị và IDOR trường khác đã được thử thủ công trên database riêng, rồi database thử được xóa. Lệnh bootstrap production mới được xác nhận nhánh thiếu thông tin đăng nhập; chưa chạy tạo tài khoản trên database thật. Script kiểm tra phiên hết hạn đã viết nhưng chưa chạy xác nhận.

Giao diện đăng nhập/quản trị được chừa lại theo yêu cầu. Chính sách tài nguyên M3 đã có, nhưng Question, Blueprint và Jobs chưa có API hoặc bản ghi tài nguyên để gọi chính sách sau khi tải tài nguyên thật từ DB; vì vậy các bài test IDOR trực tiếp vào ba API đó chỉ thực hiện được khi các API tương ứng xuất hiện. Quyền kỳ thi cấp Bộ cũng cần bảng gán người tham gia từng kỳ thi trước khi mở nội dung kỳ thi; chính sách trường hiện tại mặc định từ chối phạm vi này.
