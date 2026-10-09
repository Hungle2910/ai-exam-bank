# ADR 0003 — Data, identity and runtime baseline

**Owner:** M1 / Le Doan Gia Hung. **Date:** 2026-10-09. Related: [solution design](../DOTNET_SOLUTION_DESIGN.md), [ADR 0002](0002-dotnet-module-boundaries.md), #1, #2, #3, #4 and #5.

## Status

Proposed for M1–M5 review. No DB engine, identity provider or frontend framework has been implemented or accepted by this document.

## Context

The MVP includes multiple schools and Ministry-level exams, as accepted in [ADR 0004](0004-multi-school-ministry-mvp.md). Five contributors have an initial ten-week plan whose estimates require revision. Approval and audit, question revision state, import commit and exam finalization need consistent relational writes. The current repository has one .NET API host and no database, identity, Web or Worker implementation. Region, demo budget, hosting and final frontend framework remain open. This ADR proposes technology defaults so the first vertical slices can be built without incompatible storage or authentication.

## Options considered and trade-offs

| Concern | Proposed default | Alternative | Accepted cost / revisit trigger |
|---|---|---|---|
| Relational persistence | PostgreSQL with EF Core, one DbContext and migration stream | SQL Server or separate stores per module | Shared migration coordination; revisit if actual hosting, team expertise or transaction tests favor another engine |
| User authentication | ASP.NET Core Identity, secure same-origin server session cookie | External OIDC/Cognito or self-managed bearer tokens | Requires CSRF protection and session lifecycle; compare federation/provisioning needs for multiple schools before accepting |
| Frontend | One TypeScript Web app served through the same HTTPS origin as API; React proposed | Server-rendered UI or another team-approved framework | Frontend build pipeline and contract tests; framework is not accepted until M1/M3 review |
| Background execution | DB-backed Job/Attempt + separate .NET Worker host | Inline HTTP tasks or a managed queue | Must implement leases, retries and monitoring; move to a managed queue only if throughput/operations evidence justifies it |
| Deployment | Private API/Worker/DB behind one HTTPS entry, runtime roles and SSM | Public API/DB or multiple independently deployed services | Entry/egress and private network cost; M2 must compare EC2/RDS, NAT/endpoints and Region before provisioning |

## Decision

The following is the proposed baseline, pending team acceptance:

1. Keep one relational transaction boundary for revision approval + review decision + audit, and for import batch commit + question writes. Use optimistic version checks and database uniqueness for duplicate prevention; do not rely on UI checks or distributed events for these invariants.
2. Use the four-role, multi-school/Ministry model in ADR 0004 and its permission matrix. Check resource scope, self-review and current state server-side for every read and mutation. Use framework-managed password/session facilities; do not invent password hashing, JWT issuance or authorization shortcuts.
3. Version business routes under `/api/v1`; use Problem Details with stable error codes and trace IDs. Add OpenAPI and consumer tests with each implemented endpoint.
4. Start the Worker only with the first durable handler. A persisted request and its job record must commit together; lease/attempt/retry policy is an M5 contract before production use.
5. Keep Lex and SageMaker behind explicit feature gates. Multi-school and Ministry exam flows are part of MVP scope per ADR 0004; implementation still needs accepted contracts and tests.

## Security, reliability and cost implications

Cookie mutations need antiforgery protection; cookies are Secure/HttpOnly/SameSite with expiry and logout. API authorization checks actor and resource scope again at use-case boundaries. Runtime IAM grants only the S3/Bedrock/CloudWatch/SSM actions actually used. Logs omit credentials, answer keys and personal data by default.

The shared DB simplifies atomic writes but needs migration coordination and restore testing. A separate Worker isolates long tasks but requires durable lease/retry handling and monitoring. Private networking, DB hosting, NAT/endpoints and the selected identity path have costs M2 must quantify before provisioning.

## Validation evidence required

Before accepting this ADR, M1/M2/M3/M4/M5 confirm the DB/hosting decision, migration owner, role/scope matrix, frontend origin/framework, job persistence contract and cost ceiling. The first implementation PRs need a small executable proof: migration on a clean DB, one protected endpoint with 401/403 cases, one atomic write/concurrency test, and CI evidence. No empty project or paid AWS resource is required merely to approve the design.

## Consequences

If the team chooses a different technology default, update this ADR and the solution design in the same PR, with the reason and affected contracts. After approval, mark this ADR **Accepted**; until then the technology defaults are recommendations, not implemented facts. The accepted multi-school/Ministry product scope in ADR 0004 already requires the separate permission matrix and migration path before implementation.
