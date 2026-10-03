# Contributing

## Before implementation

Đọc README, architecture, team ownership, issue scope/dependencies và implementation plan. Mỗi module có primary owner; phối hợp contracts/migrations/config trước khi sửa shared files. Không overwrite/revert thay đổi người khác hoặc refactor module ngoài scope chỉ để đổi style.

## Branches and commits

Branch đề xuất: `feat/M4-02-question-bank`, `fix/M5-03-idempotency`, `docs/M2-05-deploy-runbook`. PR nhỏ, một outcome coherent, link issue bằng `Refs #N` hoặc `Closes #N` khi toàn DoD đạt. Commits có purpose rõ; không commit credentials/keys/DB dumps/datasets không được phép.

## Pull request requirements

Describe problem/resulting behavior, scope, validation và material limitations. Include backend/frontend/data/AWS changes, migration/rollback notes nếu có, security/resource authorization effects và evidence links. Không claim test/AWS deploy đã pass nếu chưa chạy.

Owner tự test relevant paths; peer reviewer kiểm code/domain invariants, failure behavior, authorization và migrations. Critical state/selection/idempotency behavior cần meaningful tests. Avoid tests chỉ mirror private implementation hoặc assert wording.

## Code and architecture

.NET controls business rules; domain không depend AWS DTOs; controller thin; EF queries có scope/paging/index evidence; async dùng cancellation/timeouts hợp lý. Frontend reflects API state/permissions, không dùng local-only business state thay server. AI/model/retrieval outputs validated/untrusted. Không introduce microservices/Kubernetes hay extra AWS services nếu chưa có ADR/requirement.

## Documentation and evidence

Update OpenAPI/config examples/runbooks/ADRs cùng PR. Evidence redacted; source/prompt/model/release versions rõ. AWS MUST cần live proof; mock providers chỉ dùng isolated tests/dev. Issue chỉ Done khi DoD đạt; cập nhật Actual Hours và status/issue state nhất quán.

## Decisions and escalation

M1 chốt contracts/domain integration; M2 infra/deploy; M3 security/approval; M4 question/RAG; M5 resilience/ML. Xung đột scope ưu tiên MVP; security P0 hoặc missing approved-only/snapshot invariants chặn release. Ghi blocker có owner/next action, không tăng IAM quyền hoặc public backend để né lỗi.
