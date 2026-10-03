# Test strategy

Planned behavior tests cover domain constraints/approved-only selection, immutable revisions/snapshots, resource authorization/review concurrency, import/generation idempotency, worker crash/recovery, source citations scope and E2E live-AWS evidence.

`tests/repository/` hiện có regression tests cho repository hygiene checker; chạy bằng `python -m unittest discover -s tests/repository -v`. Đây không phải product tests hoặc coverage của ứng dụng. Foundation owners chọn tools theo actual backend/frontend versions; application tests thuộc feature owners, cross-module cases do M1/M5 điều phối.
