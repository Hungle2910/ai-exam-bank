# Test strategy

Planned behavior tests cover domain constraints/approved-only selection, immutable revisions/snapshots, resource authorization/review concurrency, import/generation idempotency, worker crash/recovery, source citations scope and E2E live-AWS evidence.

`tests/repository/` có regression tests cho repository hygiene checker; chạy bằng `python -m unittest discover -s tests/repository -v`. `tests/Modules/` dùng MSTest cho quy tắc revision reference, blueprint slot và scoped job request; `tests/Architecture.Tests/` bảo vệ hướng phụ thuộc của bảy module bằng cả assembly và khai báo `.csproj`. Chạy `dotnet test AiExamBank.slnx --configuration Release`. Các test này chỉ chứng minh những quy tắc nhỏ hiện có, chưa phải coverage của workflow sản phẩm. Application, persistence, API, security, worker và AWS integration tests thuộc feature PR của từng owner; M1/M5 điều phối case liên module.

`tests/auth-smoke.ps1` kiểm tra API auth đang chạy local: login sai/đúng, CSRF, logout, 401 và 403. Chạy API ở môi trường Development với database đã migrate và một tài khoản thử **không có SchoolAdmin**. Đặt mật khẩu tài khoản đó vào biến môi trường `AIEXAMBANK_SMOKE_PASSWORD` rồi chạy `powershell -File tests/auth-smoke.ps1`; có thể truyền `-BaseUrl` và `-UserName`. Script không lưu hoặc in mật khẩu. Sau khi login, client phải lấy token CSRF mới trước khi logout vì token cũ gắn với phiên chưa đăng nhập.

`tests/auth-expiry-smoke.ps1` kiểm tra cookie hết hạn thật: chạy API thử nghiệm với `Auth__SessionLifetimeSeconds=2`, rồi chạy script với cùng biến mật khẩu và `-WaitSeconds 4`. Script đòi `/auth/me` trả `200` ngay sau login và `401` sau khi hết hạn. Chỉ dùng thời hạn 2 giây ở môi trường thử nghiệm; mặc định ứng dụng là 30 phút. Bài test này cần chạy riêng vì nó thay đổi cấu hình thời hạn phiên của API đang chạy.

M3-02 đã được thử thủ công trên một database riêng cho các trường hợp tạo trường/bộ môn/tài khoản, phân quyền cùng trường, cấm truy cập khác trường, cấm nâng quyền, thu hồi role có hiệu lực trong phiên cũ, và audit. `ResourceAccessPolicyTests` kiểm tra quyền Question/Blueprint/Job theo chủ sở hữu và trường/bộ môn, gồm self-review và thiếu role. Các API Question/Blueprint/Jobs chưa có trong repository nên chưa thể kiểm thử quyền trên tài nguyên thật; các module sở hữu API cần gọi `ResourceAuthorizationService` sau khi tải scope từ bản ghi thật, rồi bổ sung direct-API IDOR tests.

## Jobs dashboard browser regressions

Start the API in Development with an absolute `Jobs__StorePath` as described in the
[M5-02 runbook](../docs/M5_02/M5_02_JOB_ENGINE_DESIGN.md). The browser tests intercept
job requests with synthetic responses; real HTTP response contracts are covered by
`python tests/smoke/test_api_health.py`.

Install the test browser dependency outside the checkout and run from the repository root:

```bash
npm install --prefix /tmp/ai-exam-bank-ui-tests --no-save --package-lock=false playwright@1.55.0
NODE_PATH=/tmp/ai-exam-bank-ui-tests/node_modules /tmp/ai-exam-bank-ui-tests/node_modules/.bin/playwright install chromium
NODE_PATH=/tmp/ai-exam-bank-ui-tests/node_modules JOBS_UI_BASE_URL=http://localhost:5000 node --test Backend/tests/smoke/jobs_ui.test.cjs
```

When using an existing Chromium session, set `PLAYWRIGHT_CDP_URL` to its CDP endpoint.
The tests cover job type badges/search, an initially hidden page with no polling,
and creation/submission at short, narrow and full-height viewports.
