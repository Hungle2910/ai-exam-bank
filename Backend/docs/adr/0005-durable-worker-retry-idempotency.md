# ADR 0005 — Durable Worker Lease, Retry Policy and Idempotency Baseline

**Owner:** M5 / Tran Gia Bao (@TranGiaBao2005) — Reliability & ML Evaluation Engineer. **Date:** 2026-10-22.  
**Related:** [M5_03_RETRY_IDEMPOTENCY_DESIGN.md](../M5_03/M5_03_RETRY_IDEMPOTENCY_DESIGN.md), [ADR 0003](0003-data-identity-runtime-baseline.md), [ADR 0004](0004-multi-school-ministry-mvp.md), Issue #15.

## Status

Proposed for team review (M1–M5). Implemented and verified in branch `feat/M5-03-retry-idempotency` with 84/84 unit and integration tests passing.

## Context

In [ADR 0003 (Item 4)](0003-data-identity-runtime-baseline.md), the baseline architecture established that durable background execution requires an explicit lease/attempt/retry and idempotency contract before production use. Long-running AI generation (AWS Bedrock) and bulk CSV question imports are vulnerable to:
1. Transient provider throttling and network socket timeouts (HTTP 429, 503, Bedrock throttling).
2. Worker process crashes or abrupt EC2 instance termination leading to abandoned leases.
3. Timeout-after-write anomalies where an external resource (e.g. Exam snapshot, Question batch) is successfully committed by the AI provider/DB, but the worker crashes before marking the job completed, causing duplicate generation/import on retry.
4. Malicious or accidental concurrent retries and cross-tenant data leakage.

## Options considered and trade-offs

| Concern | Proposed Default (M5-03) | Alternative Considered | Accepted Cost / Trade-off |
|---|---|---|---|
| **Failure Taxonomy** | 3-tier classification (`Transient`, `Permanent`, `Terminal`) with explicit error code registry | Binary retry/fail flag or retry on all unhandled exceptions | Requires maintaining error code mapping; prevents blind retries on unrecoverable validation/auth errors |
| **Retry Backoff & Jitter** | Bounded Exponential Backoff ($30\text{s} \times 2^{(\text{attempt}-1)}$, max 15m, 3 attempts) + Full/Decorrelated Jitter ($\pm 20\%$) | Immediate retry or linear fixed backoff | Retries take longer to exhaust; completely prevents thundering herd on Bedrock/RDS |
| **Idempotency Store** | Scoped compound key (`ScopeKind:ScopeId:RequesterId:JobType:ClientKey`) with Result Reference reuse | Client-only UUID or non-scoped global idempotency table | Requires callers to pass valid scope and payload fingerprint; guarantees tenant isolation |
| **Timeout-after-write Recovery** | Check-before-write pattern inside integration adapters (`ExamGenerationJobHandler`, `CsvImportJobHandler`) | Two-phase commit (2PC) or distributed sagas | Adapters must support idempotent lookup by idempotency key/fingerprint; avoids distributed transaction overhead |
| **Manual Retry Governance** | `POST /api/v1/jobs/{id}/retry` with Zero-Trust Scoped Auth (`JobScope`), status validation, and audit reason | Auto-retry on all errors or unrestricted retry endpoint | Endpoints enforce HTTP 403 on cross-scope access and 400/409 on invalid job states; caller must provide reason |

## Decision

The following durable worker, retry, and idempotency contract is established:

1. **Strict 3-Tier Error Taxonomy:**
   - `Transient`: Network/Bedrock timeout, throttling, DB concurrency conflict. Handled via bounded exponential backoff + jitter up to 3 automatic attempts.
   - `Permanent`: Data validation error, invalid CSV schema, insufficient questions, unauthorized token. Fails immediately without retry.
   - `Terminal`: Exhausted retry budget (`RETRY_BUDGET_EXHAUSTED`) or cancelled job (`JOB_CANCELLED`). Locks the terminal state.

2. **Bounded Exponential Backoff with Jitter:**
   - Base delay: 30s. Max delay: 15m. Max attempts: 3.
   - Jitter factor: $\pm 20\%$ to disperse thundering herd effects across distributed workers.
   - Rescheduled jobs remain in `Pending` state with `AvailableAt = UtcNow + Delay`.

3. **Multi-Tenant Idempotency & Result Reuse:**
   - Uniqueness is partitioned by `ScopeKind` + `ScopeResourceId` + `RequesterId` + `JobType` + `ClientKey`.
   - Re-submitting an identical request for a `Completed` job returns the existing `ResultId` and job status without queueing work.
   - Re-submitting with modified parameters under the same key triggers HTTP 409 Conflict (`IDEMPOTENCY_FINGERPRINT_MISMATCH`).

4. **Integration Adapter Resilience:**
   - `ExamGenerationJobHandler` and `CsvImportJobHandler` verify external state (check-before-write) before executing calls to AWS Bedrock or relational storage.

5. **Operational Retry & Attempt Observability:**
   - Exposed endpoints: `POST /api/v1/jobs/{id}/retry` (with optional `reason`) and `GET /api/v1/jobs/{id}/attempts`.
   - Embedded Operator Status UI (`Backend/Api/wwwroot/jobs/`) provides interactive manual retry triggers, status badges, and execution timeline drawer.

## Security, reliability and cost implications

- **Security & Multi-Tenancy:** Scoped authorization prevents tenant impersonation or unauthorized job restarts across schools. Sensitive errors, PII, and raw exam content are redacted from `SafeErrorMessage`.
- **Reliability:** Dead worker recovery automatically reclaims expired leases after a 1-minute cooldown, marking crashed attempts as `Abandoned`.
- **Cost:** Preventing runaway retries on permanent errors protects AWS Bedrock invocation budgets and RDS IOPS.

## Validation evidence

- **Unit & Integration Tests:** 13 new targeted tests in `RetryAndIdempotencyTests.cs` validating duplicate requests, expired lease cooldown, permanent error fail-fast, timeout-after-write recovery, and scoped authorization.
- **Suite Status:** 84/84 tests passing in `AiExamBank.Modules.Jobs.Tests`.
- **Repo Hygiene:** 0 errors across 412 tracked files via `check_repository.py`.
- **Smoke Tests:** 100% pass rate in `test_api_health.py` and `test_infrastructure_health.py`.

## Consequences

- All future background job handlers (e.g. Export, Notification, Vector Indexing) must implement `IJobHandler` conforming to the 3-tier failure taxonomy and check-before-write adapter pattern.
- If storage migrates from file/in-memory store to PostgreSQL RDS, the schema must implement table uniqueness on `(ScopeKind, ScopeResourceId, RequesterId, JobType, IdempotencyKey)`.
