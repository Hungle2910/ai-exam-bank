# Security policy

## Project status

Repository đang planning/bootstrap, chưa có supported runtime release. Security controls trong docs là requirements phải được triển khai và kiểm chứng, không là assurance đã vận hành.

## Reporting a vulnerability

Không đăng secret/access key/token/password hoặc learner/question data nhạy cảm vào public issue. Liên hệ riêng repository owner qua kênh đã thống nhất; dùng GitHub private vulnerability reporting nếu tính năng đã được bật cho repository. Chưa công bố SLA phản hồi hoặc email không được xác nhận.

Report cần scope/affected release, reproduction steps, authorization context, impact và redacted evidence. Không mở rộng kiểm thử sang AWS accounts/systems ngoài quyền của đội.

## Required controls

- Server-side authentication, role và resource authorization cho mutation/read endpoints.
- AI chỉ DRAFT/REVIEW_REQUIRED, human approval; immutable approved content/final exam snapshots.
- Audit decisions/role changes, transaction/concurrency protection và idempotency.
- Private backend/DB, HTTPS, least-privilege IAM/runtime roles, controlled SSM access.
- Safe secret delivery, no committed secrets; logs/evidence redacted; corpus có quyền sử dụng.
- Validate upload/model/retrieval inputs; escape UI; scope citations/job sessions.
- Relevant negative/security tests; critical findings block core release.

## Secrets and evidence handling

Không thêm `.pem`, `.ppk`, private keys, `.env` runtime, IaC state, production DB dumps hoặc real credentials vào Git. Khi credential leak được xác nhận, repository owner/security owner phối hợp revoke/rotate và xử lý affected history theo scope đã thống nhất. Ignore patterns hỗ trợ hygiene, không thay secret scanning/review.
