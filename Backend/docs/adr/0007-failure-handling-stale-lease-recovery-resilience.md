# ADR 0007 — Failure Handling, Stale Lease Recovery, and Provider Resilience

**Owner:** M5 / Tran Gia Bao (@TranGiaBao2005) — Reliability & ML Evaluation Engineer. **Date:** 2026-10-27.  
**Release Tag:** `v1.2.0-rc1` · **Job Engine Version:** `job-engine/v1.2`  
**Related:** [M5_04_FAILURE_HANDLING_DESIGN.md](../M5_04/M5_04_FAILURE_HANDLING_DESIGN.md), [M5_04_FAILURE_DRILL_RUNBOOK.md](../M5_04/M5_04_FAILURE_DRILL_RUNBOOK.md), [ADR 0005](0005-durable-worker-retry-idempotency.md), Issue #20.

## Status

Proposed for team review. Implemented in branch `feat/M5-04-failure-handling`.

## Context

Following [ADR 0005](0005-durable-worker-retry-idempotency.md) for retry backoff and idempotency, production operations on AWS require explicit failure containment for external providers (Amazon Bedrock AI, Amazon S3 storage) and catastrophic worker failure modes:
1. **Stale Running Jobs (Process Crash / OOM / Network Partition):** When a worker executing a job crashes abruptly, the job remains stuck in `Running` indefinitely because no terminal event or heartbeat renewal occurs, causing infinite UI spinners.
2. **Provider Failures (Bedrock Throttling, Quotas, S3 Access Denied):** External cloud providers fail with specific HTTP/gRPC codes (429, 403, 503) that must be classified accurately into transient vs permanent categories with actionable instructions for end users.
3. **Information Disclosure (CWE-209):** Unhandled provider exceptions must never leak raw stack traces, file paths, AWS account IDs, or credentials to client APIs or UI.
4. **Cascading Failure & Thundering Herd:** When an external AI provider experiences an outage, workers must not exhaust API rate limits or budgets by continuously retrying.

## Options Considered

1. **Option 1 — Rely solely on manual operator recovery and unbounded retries:**
   - *Pros:* Simple to implement; no background sweeping loop needed.
   - *Cons:* Crashed worker tasks remain stuck in `Running` forever (indefinite UI spinner); unconstrained retries during Bedrock outages exhaust AWS service quotas and increase cloud costs. (Rejected).
2. **Option 2 — Store-level stale lease recovery loop, in-memory circuit breaker, and strict error sanitization:**
   - *Pros:* Immediate containment within the current modular monolith; automatic recovery of orphaned jobs after 2 minutes; fast-fail circuit protection for Bedrock without external orchestration dependencies; Zero-Trust CWE-209 sanitization.
   - *Cons:* Circuit breaker state is currently in-process (per host); multi-node scaling in later weeks will require Redis or centralized telemetry. (Selected).
3. **Option 3 — Fully external workflow orchestration (AWS Step Functions / SQS DLQ):**
   - *Pros:* Offloads state machines and lease timeouts entirely to managed AWS services.
   - *Cons:* High infrastructure overhead for Week 4 local development and unit testing; exceeds MVP boundary constraints prior to M2 cloud provisioning gate. (Rejected for MVP).

## Decision

1. **Stale Lease Recovery Loop:**
   - Background workers continuously scan for abandoned running jobs where `LeaseExpiresAt < UtcNow` (lease expired without heartbeat for > 2 minutes).
   - Stale attempts are marked as `AttemptStatus.Abandoned`.
   - If `AttemptCount < MaxAttempts`, the job is recovered back to `Pending` with an actionable error code `STALE_LEASE_RECOVERED` and scheduled backoff delay.
   - If `AttemptCount >= MaxAttempts`, the job is marked `Failed` with `RETRY_BUDGET_EXHAUSTED`.

2. **AWS Error Mapping & Sanitization:**
   - Dedicated error code registry (`AwsErrorCodes`) maps AWS Bedrock (`AWS_BEDROCK_THROTTLED`, `AWS_BEDROCK_QUOTA_EXCEEDED`, `AWS_BEDROCK_ACCESS_DENIED`, `AWS_BEDROCK_MODEL_TIMEOUT`), S3 (`AWS_S3_ACCESS_DENIED`, `AWS_S3_FILE_NOT_FOUND`, `AWS_S3_SLOW_DOWN`), and RAG (`RAG_RETRIEVAL_EMPTY`).
   - Every error code is assigned a localized, human-readable, and actionable user message (`ActionableReason`).
   - `SafeErrorMessage` is strictly sanitized with regular expressions to strip all stack traces, Windows/Linux system paths, AWS ARNs, and access credentials (`AKIA`/`ASIA`) before exposing to client APIs or UI.

3. **AI Provider Circuit Breaker:**
   - An in-process circuit breaker (`IAiProviderCircuitBreaker`) tracks consecutive transient provider failures.
   - Tripping threshold: 5 consecutive failures trips state to `Open` for 60 seconds (cooling period).
   - While `Open`, new invocations fail fast with `CIRCUIT_BREAKER_OPEN`, protecting provider quota and application throughput.
   - After cooldown, state transitions to `HalfOpen` to allow a single probe request to reset or re-open the circuit; probe timeout and permanent error releases prevent deadlocks.

4. **Status UI & API Retry Eligibility Governance:**
   - The Job Status UI and API endpoint (`POST /api/v1/jobs/{id}/retry`) dynamically enforce retry eligibility based on `ErrorCategory`.
   - Permanent errors (e.g., S3 file missing, invalid schema, quota exceeded, access denied) disable the Retry button on the UI and return HTTP 409 Conflict on direct API calls.

## Security, Reliability, and Cost Implications

- **Security (CWE-209):** Eliminates information exposure through automated regex sanitization of all diagnostic error messages, preventing leaked stack traces or IAM access keys from reaching browser clients or unauthorized users.
- **Reliability:** Guarantees zero orphaned `Running` jobs through automated lease recovery; resilient against worker process crashes, host reboots, and transient Bedrock network timeouts.
- **Cost Protection:** Fast-failing via `CIRCUIT_BREAKER_OPEN` protects AWS Bedrock spending by stopping invocation storms during downstream provider outages.

## Validation Evidence

- **Unit & Resilience Test Suite:** 126/126 tests passed across `AiExamBank.Modules.Jobs.Tests` (143/143 tests across solution), including 42 dedicated resilience and failure handling tests in `FailureHandlingAndResilienceTests.cs`.
- **Failure Drill Runbook:** Standardized recovery procedures documented in [M5_04_FAILURE_DRILL_RUNBOOK.md](../M5_04/M5_04_FAILURE_DRILL_RUNBOOK.md).
- **Redacted Benchmark Artifact:** Redacted illustrative failure drill data recorded in [drill_evidence.json](../M5_04/drill_evidence.json) with version `job-engine/v1.2`.

## Consequences

- Zero jobs stuck indefinitely in `Running` status even across hard worker crashes.
- Clean separation between internal diagnostics (logs) and public UI messages (actionable reasons).
- Protected downstream AWS services against retry storms during outages.
