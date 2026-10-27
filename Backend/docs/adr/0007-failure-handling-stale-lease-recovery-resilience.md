# ADR 0007 — Failure Handling, Stale Lease Recovery, and Provider Resilience

**Owner:** M5 / Tran Gia Bao (@TranGiaBao2005) — Reliability & ML Evaluation Engineer. **Date:** 2026-10-27.  
**Related:** [M5_04_FAILURE_HANDLING_DESIGN.md](../M5_04/M5_04_FAILURE_HANDLING_DESIGN.md), [ADR 0005](0005-durable-worker-retry-idempotency.md), Issue #20.

## Status

Accepted and implemented in branch `feat/M5-04-failure-handling`.

## Context

Following [ADR 0005](0005-durable-worker-retry-idempotency.md) for retry backoff and idempotency, production operations on AWS require explicit failure containment for external providers (Amazon Bedrock AI, Amazon S3 storage) and catastrophic worker failure modes:
1. **Stale Running Jobs (Process Crash / OOM / Network Partition):** When a worker executing a job crashes abruptly, the job remains stuck in `Running` indefinitely because no terminal event or heartbeat renewal occurs, causing infinite UI spinners.
2. **Provider Failures (Bedrock Throttling, Quotas, S3 Access Denied):** External cloud providers fail with specific HTTP/gRPC codes (429, 403, 503) that must be classified accurately into transient vs permanent categories with actionable instructions for end users.
3. **Information Disclosure (CWE-209):** Unhandled provider exceptions must never leak raw stack traces, file paths, AWS account IDs, or credentials to client APIs or UI.
4. **Cascading Failure & Thundering Herd:** When an external AI provider experiences an outage, workers must not exhaust API rate limits or budgets by continuously retrying.

## Decision

1. **Stale Lease Recovery Loop:**
   - Background workers continuously scan for abandoned running jobs where `LeaseExpiresAt < UtcNow` (lease expired without heartbeat for > 2 minutes).
   - Stale attempts are marked as `AttemptStatus.Abandoned`.
   - If `AttemptCount < MaxAttempts`, the job is recovered back to `Pending` with an actionable error code `STALE_LEASE_RECOVERED` and scheduled backoff delay.
   - If `AttemptCount >= MaxAttempts`, the job is marked `Failed` with `RETRY_BUDGET_EXHAUSTED`.

2. **AWS Error Mapping & Sanitization:**
   - Dedicated error code registry (`AwsErrorCodes`) maps AWS Bedrock (`AWS_BEDROCK_THROTTLED`, `AWS_BEDROCK_QUOTA_EXCEEDED`, `AWS_BEDROCK_ACCESS_DENIED`, `AWS_BEDROCK_MODEL_TIMEOUT`), S3 (`AWS_S3_ACCESS_DENIED`, `AWS_S3_FILE_NOT_FOUND`, `AWS_S3_SLOW_DOWN`), and RAG (`RAG_RETRIEVAL_EMPTY`).
   - Every error code is assigned a localized, human-readable, and actionable user message (`ActionableReason`).
   - `SafeErrorMessage` is strictly sanitized to exclude all stack traces, internal paths, and secrets.

3. **AI Provider Circuit Breaker:**
   - An in-process circuit breaker (`IAiProviderCircuitBreaker`) tracks consecutive transient provider failures.
   - Tripping threshold: 5 consecutive failures trips state to `Open` for 60 seconds (cooling period).
   - While `Open`, new invocations fail fast with `CIRCUIT_BREAKER_OPEN`, protecting provider quota and application throughput.
   - After cooldown, state transitions to `HalfOpen` to allow a single probe request to reset or re-open the circuit.

4. **Status UI Retry Eligibility:**
   - The Job Status UI dynamically evaluates retry eligibility based on `ErrorCategory` and attempt limit.
   - Permanent errors (e.g., S3 file missing, invalid schema, quota exceeded) disable the Retry button with an informative tooltip, preventing fruitless manual retries.

## Consequences

- Zero jobs stuck indefinitely in `Running` status even across hard worker crashes.
- Clean separation between internal diagnostics (logs) and public UI messages (actionable reasons).
- Protected downstream AWS services against retry storms during outages.
