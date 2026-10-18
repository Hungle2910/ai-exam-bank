# Test strategy

Planned behavior tests cover domain constraints/approved-only selection, immutable revisions/snapshots, resource authorization/review concurrency, import/generation idempotency, worker crash/recovery, source citations scope and E2E live-AWS evidence.

`tests/repository/` có regression tests cho repository hygiene checker; chạy bằng `python -m unittest discover -s tests/repository -v`. `tests/Modules/` dùng MSTest cho quy tắc revision reference, blueprint slot và scoped job request; `tests/Architecture.Tests/` bảo vệ hướng phụ thuộc của bảy module bằng cả assembly và khai báo `.csproj`. Chạy `dotnet test AiExamBank.slnx --configuration Release`. Các test này chỉ chứng minh những quy tắc nhỏ hiện có, chưa phải coverage của workflow sản phẩm. Application, persistence, API, security, worker và AWS integration tests thuộc feature PR của từng owner; M1/M5 điều phối case liên module.

## Jobs dashboard browser regressions

Start the API in Development with an absolute `Jobs__StorePath` as described in the
[M5-02 runbook](../docs/M5_02/M5_02_JOB_ENGINE_DESIGN.md). The browser tests intercept
job requests with synthetic responses; real HTTP response contracts are covered by
`python tests/smoke/test_api_health.py`.

Install the test browser dependency outside the checkout and run from the repository root:

```bash
npm install --prefix /tmp/ai-exam-bank-ui-tests --no-save --package-lock=false playwright@1.55.0
NODE_PATH=/tmp/ai-exam-bank-ui-tests/node_modules /tmp/ai-exam-bank-ui-tests/node_modules/.bin/playwright install chromium
NODE_PATH=/tmp/ai-exam-bank-ui-tests/node_modules JOBS_UI_BASE_URL=http://localhost:5000 node --test tests/smoke/jobs_ui.test.cjs
```

When using an existing Chromium session, set `PLAYWRIGHT_CDP_URL` to its CDP endpoint.
The tests cover job type badges/search, an initially hidden page with no polling,
and creation/submission at short, narrow and full-height viewports.
