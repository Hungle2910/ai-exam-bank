# OpenAPI

[questions.json](questions.json) mô tả API ngân hàng câu hỏi của M4-02 (OpenAPI 3.1): `GET /catalog`, `GET/POST /questions`, `GET/PUT /questions/{id}` và hai route gán môn cho bộ môn của trường. Nó chỉ gồm các route đó, không gồm đăng nhập (`/auth/*`), quản trị người dùng hay Jobs.

Tài liệu được sinh từ các endpoint thật (`Microsoft.AspNetCore.OpenApi`), nên route chưa được map sẽ không xuất hiện. Các response là record có kiểu (`Backend/Api/Endpoints/QuestionApiContracts.cs`), chính các record mà handler trả về, nên schema và JSON trên đường truyền dùng chung một định nghĩa. Ví dụ request và ví dụ lỗi nằm ở `QuestionOpenApi.cs`; các thông báo lỗi trong ví dụ là thông báo thật của server.

## Xem trực tiếp

Ở môi trường Development, API phục vụ tài liệu tại `/openapi/questions.json`. Route này không được map ngoài Development.

## Tạo lại file

Mỗi khi đổi route, kiểu request/response hoặc metadata, sinh lại và commit cùng thay đổi đó. Chạy API ở Development rồi tải về, ví dụ từ thư mục `Backend/Api`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__DefaultConnection = "<chuỗi kết nối của database thử>"
dotnet run --no-launch-profile --urls http://localhost:5000
# cửa sổ khác:
Invoke-WebRequest http://localhost:5000/openapi/questions.json -OutFile ..\docs\openapi\questions.json
```

File được commit đúng như API phục vụ, không chỉnh tay, nên diff chỉ phản ánh thay đổi thật. Chưa có kiểm tra tự động nào bắt việc file này lệch khỏi code, nên người review nên chạy lại lệnh trên khi PR đổi API.
