# AI Exam Bank - Troubleshooting & Lessons Learned

Các bài học kinh nghiệm thực chiến và cách giải quyết sự cố kỹ thuật trong quá trình phát triển dự án AI Exam Bank.

---

## 🛠️ 1. Lệch phiên bản .NET SDK (`global.json`)
- **Hiện tượng:** Chạy lệnh `dotnet build` hoặc `dotnet test` bị báo lỗi không tìm thấy SDK `10.0.400`.
- **Nguyên nhân gốc rễ:**
  - File `Backend/global.json` ghim cứng `"version": "10.0.400"` để đồng bộ với môi trường CI GitHub Actions (`ubuntu-24.04`).
  - Môi trường máy local của Gia Bảo hiện cài đặt SDK preview/RC: `10.0.100-rc.1.25451.107`.
- **Quy tắc giải quyết:**
  1. Khi cần build/test/run bằng CLI ở local: Tạm thời sửa `Backend/global.json` thành:
     ```json
     {
       "sdk": {
         "version": "10.0.100-rc.1.25451.107",
         "rollForward": "latestMajor",
         "allowPrerelease": true
       }
     }
     ```
  2. **Bắt buộc trước khi commit/push:** Luôn hoàn nguyên file này bằng lệnh:
     ```powershell
     git restore Backend/global.json
     ```
     để tránh làm fail luồng CI GitHub Actions.

---

## 🔒 2. Quản lý File Lock & Tiến trình Chạy ngầm
- **Hiện tượng:** Không thể build lại hoặc file job store JSON bị lock bởi tiến trình khác (`The process cannot access the file because it is being used by another process`).
- **Nguyên nhân:** Lần chạy `dotnet run` trước đó chưa dừng hẳn hoặc worker daemon vẫn đang giữ Mutex.
- **Cách khắc phục:**
  - Mở PowerShell và tắt toàn bộ tiến trình dotnet đang chạy ngầm:
    ```powershell
    Stop-Process -Name dotnet -Force
    ```

---

## 🌐 3. Quy chuẩn Link Markdown trong `check_repository.py`
- **Hiện tượng:** Chạy `python Backend/tools/check_repository.py` báo lỗi broken markdown link hoặc CI báo fail ở bước verify hygiene.
- **Nguyên nhân:** Runner GitHub Actions chạy trên Linux (hệ thống file phân biệt chữ hoa/thường - case-sensitive), trong khi Windows không phân biệt.
- **Quy tắc:**
  - Mọi đường dẫn tương đối trong các file `.md` phải khớp chính xác từng ký tự hoa/thường với tên file thực tế (Ví dụ: `M5_03_RETRY_IDEMPOTENCY_DESIGN.md`).
  - Luôn chạy `python Backend/tools/check_repository.py` để đạt 0 lỗi trước khi mở PR.

---

## 🤖 4. Quy trình Phê duyệt CodeRabbit AI Review
- **Hiện tượng:** CodeRabbit đưa ra review `CHANGES_REQUESTED` và chặn không cho merge PR vào nhánh `dev`.
- **Nguyên nhân:** Mặc định CodeRabbit yêu cầu tác giả hoặc reviewer giải quyết các mục cần sửa đổi.
- **Cách giải quyết:**
  - Cấu hình file `.coderabbit.yaml` tại thư mục gốc với cờ:
    ```yaml
    reviews:
      allow_author_approval: true
    ```
  - Sau khi đẩy các commit fix lỗi, bình luận `@coderabbitai review` để bot quét lại, hoặc dismiss review trên giao diện GitHub PR khi tất cả các điểm đã được giải quyết triệt để.

---

## 🔑 5. Git Push qua Windows Terminal
- Do Git Credential Manager trên Windows yêu cầu giao diện xác thực người dùng tương tác với GitHub, lệnh `git push origin <branch>` nên được chạy trực tiếp trên Terminal tương tác của người dùng thay vì chạy qua script ngầm không có UI.
