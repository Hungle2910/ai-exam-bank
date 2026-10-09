# M5-01 — Durable jobs contract and reliability baseline

**Status:** Design and compiled .NET contracts only. No DB migration, queue, Worker, job API, UI, telemetry emitter or AWS deployment exists yet. M1/M2/M5 must review the integration contract before implementing handlers.

## Ownership and isolation

`src/Modules/Jobs` owns the public `IJobPublisher`, `IJobTracker`, `IJobHandler`, `JobRequest`, `JobScope`, status types and draft Job/Attempt shapes. M1/M2 call Jobs contracts; they do not create a parallel job store. A request carries the authenticated requester, a `School` or assigned `MinistryExam` scope, a stable correlation ID, an idempotency key and a reference to access-controlled payload data. It must not store raw exam content, answer keys, credentials or personal data in a log or status response. The API derives/validates scope from the actor and target resource; it never trusts a client-supplied scope. Status, retry, cancellation and result access recheck current authorization, including revoked roles.

## Proposed persistence model

The draft `BackgroundJob` and `JobAttempt` classes are **not** mapped entities yet. EF mapping, migrations and transaction tests belong to the first durable Worker slice. A job needs ID, type, scoped requester, payload reference, idempotency key, request fingerprint, correlation ID, status, next available time and timestamps. An attempt needs ID, job ID, sequence number, worker ID, lease token, heartbeat/lease expiry, terminal status and safe error code. Use UTC `DateTimeOffset` consistently.

The idempotency uniqueness key is `(scope kind, scope resource ID, requester ID, job type, idempotency key)`; a duplicate with the same request fingerprint returns the original job, while the same key with different inputs is a conflict. Enqueue and the initiating business request must commit atomically; if that cannot be achieved with the chosen transaction boundary, document and test an outbox before implementing. A worker claims one job atomically with a lease token/version. Heartbeat extends the lease only for its owner. Recovery of an expired lease fences the old owner and creates the next attempt, so late workers cannot commit duplicate effects. Each downstream effect needs its own idempotency key.

## Proposed state and retry policy

`Pending → Running → Completed/Failed/Cancelled`; a transient failure may return to `Pending` for the next attempt. Maximum **three total attempts**: first run, then at most two retries after **1 minute** and **5 minutes**. A 15-minute third retry would require four total attempts and is outside this baseline. Heartbeat interval is 30 seconds; an attempt lease expires after 2 minutes without a successful renewal. The recovery query uses persisted `LeaseExpiresAt`, not process memory. These are proposed operational values to validate with load/failure tests before production use.

Validation errors are terminal and user-correctable; provider timeouts/429/5xx and expired worker leases may retry if budget remains. When the DB is unavailable before enqueue commits, return a safe error and do not claim that a job exists. If the DB fails while running, the worker must stop committing effects and allow lease recovery; it cannot reliably record a new state until the DB returns. Manual retry is a new authorized request or explicitly defined transition, never an unbounded reset of attempt count.

## Status, monitoring and test gates

Status provides job ID, state, optional progress, result ID or safe error code/message. Progress is **unknown** when a handler has no measurable steps; UI must not invent a percentage. Logs and metrics use job type, status, error category and correlation ID with bounded dimensions and retention. Do not log payloads, raw exception traces, answer keys or student/teacher PII. The [error matrix](M5_01_ERROR_MATRIX.md), [metrics proposal](M5_01_METRICS_SLO.md) and [UI wireframe](M5_01_RELIABILITY_UI_WIREFRAME.md) are design inputs, not operational evidence. [ML dataset spec](M5_01_DATASET_SPEC.md) is gated and not part of the durable-job MVP.

Before calling this runtime-ready, tests must prove duplicate enqueue, changed-payload key conflict, cross-school and unassigned Ministry read denial, revoked-role denial, crash after effect, stale lease fencing, retry exhaustion, validation failure, cancellation, and DB failure/rollback. CI must compile the contracts and run those tests; staging must demonstrate alert delivery and recovery. Issue #5 may close the design task after peer review, but it must not imply Worker/DB/UI/AWS behavior already exists.
