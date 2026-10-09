# Test strategy

Planned behavior tests cover domain constraints/approved-only selection, immutable revisions/snapshots, resource authorization/review concurrency, import/generation idempotency, worker crash/recovery, source citations scope and E2E live-AWS evidence.

`tests/repository/` có regression tests cho repository hygiene checker; chạy bằng `python -m unittest discover -s tests/repository -v`. `tests/Modules/` dùng MSTest cho quy tắc revision reference, blueprint slot và scoped job request; `tests/Architecture.Tests/` bảo vệ hướng phụ thuộc của bảy module. Chạy `dotnet test AiExamBank.slnx --configuration Release`. Các test này chỉ chứng minh những quy tắc nhỏ hiện có, chưa phải coverage của workflow sản phẩm. Application, persistence, API, security, worker và AWS integration tests thuộc feature PR của từng owner; M1/M5 điều phối case liên module.
