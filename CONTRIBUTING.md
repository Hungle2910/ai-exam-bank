# Contributing

## Before implementation

Đọc README, architecture, team ownership, issue scope/dependencies và implementation plan. Mỗi module có primary owner; phối hợp contracts/migrations/config trước khi sửa shared files. Không overwrite/revert thay đổi người khác hoặc refactor module ngoài scope chỉ để đổi style.

## Branches and commits

Branch đề xuất: `feat/M4-02-question-bank`, `fix/M5-03-idempotency`, `docs/M2-05-deploy-runbook`. PR nhỏ, một outcome coherent, link issue bằng `Refs #N` hoặc `Closes #N` khi toàn DoD đạt. Commits có purpose rõ; không commit credentials/keys/DB dumps/datasets không được phép.

Tạo nhánh từ `dev` và mở PR trở lại `dev`; dùng thống nhất tiền tố `feat/`, `fix/`, `docs/`, `chore/`. Nhánh `dev` chữ thường là nơi tích hợp; nhánh `Dev` chữ hoa cũ đã được gỡ vì không có commit riêng. Môi trường AWS dev là môi trường triển khai riêng. Khi đã kiểm thử tích hợp, tạo PR `dev → main` để phát hành. Xem [ADR về repo](docs/adr/0001-repository-workflow.md).

## Pull request requirements

Describe problem/resulting behavior, scope, validation và material limitations. Include backend/frontend/data/AWS changes, migration/rollback notes nếu có, security/resource authorization effects và evidence links. Không claim test/AWS deploy đã pass nếu chưa chạy.

Owner tự test relevant paths và xử lý CodeRabbit findings; chủ động mời peer reviewer cho code/domain invariants, failure behavior, authorization và migrations có rủi ro cao. Critical state/selection/idempotency behavior cần meaningful tests. Avoid tests chỉ mirror private implementation hoặc assert wording.

## Code and architecture

.NET controls business rules; domain không depend AWS DTOs; controller thin; EF queries có scope/paging/index evidence; async dùng cancellation/timeouts hợp lý. Frontend reflects API state/permissions, không dùng local-only business state thay server. AI/model/retrieval outputs validated/untrusted. Không introduce microservices/Kubernetes hay extra AWS services nếu chưa có ADR/requirement.

## Repository checks and merge policy

Chạy `python -m unittest discover -s tests/repository -v` và `python tools/check_repository.py` trước PR. Checker chỉ kiểm tracked files; stage files mới trước khi chạy. Với thay đổi ứng dụng, chạy thêm `dotnet build AiExamBank.slnx --configuration Release` và `python tests/smoke/test_api_health.py`. `Repository quality` và `Application CI` chạy trên PR và mỗi lần push vào `dev`/`main`; không dùng các check này để claim product tests hoặc AWS deployment đã pass.

`dev` và `main` đều yêu cầu PR, code up-to-date, resolved conversations và không cho force-push/delete. Trên `dev`, không bắt buộc human approval; required checks là `Repository quality`, `Application build and smoke` và `CodeRabbit`. Tác giả xử lý findings, kiểm tra bot thật sự đã review (trạng thái “skipped” không phải review), rồi tự bấm **Create a merge commit** khi tất cả checks xanh. Trên `main`, PR chỉ lấy từ `dev` đã kiểm thử, ghi SHA/evidence/phạm vi release và vẫn cần một independent human approval. CODEOWNERS định tuyến peer reviewer theo module nhưng required code-owner approval chưa bật. Branch protection áp dụng cả với admin, bao gồm Gia Hưng.

## Bot review rollout

See [bot review and manual author merge](docs/BOT_REVIEW.md) for installation,
verification and daily workflow. The author clicks Create a merge commit after
the required bot/CI checks on `dev`; promotion to `main` retains human approval.

## Documentation and evidence

Update OpenAPI/config examples/runbooks/ADRs cùng PR. Evidence redacted; source/prompt/model/release versions rõ. AWS MUST cần live proof; mock providers chỉ dùng isolated tests/dev. Issue chỉ Done khi DoD đạt; cập nhật Actual Hours và status/issue state nhất quán.

## Decisions and escalation

M1 chốt contracts/domain integration; M2 infra/deploy; M3 security/approval; M4 question/RAG; M5 resilience/ML. Xung đột scope ưu tiên MVP; security P0 hoặc missing approved-only/snapshot invariants chặn release. Ghi blocker có owner/next action, không tăng IAM quyền hoặc public backend để né lỗi.
