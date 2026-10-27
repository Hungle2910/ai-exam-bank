# Architecture Decision Records

Create `NNNN-short-title.md` when a foundation issue makes a meaningful decision.

Required sections: Status, Context/requirement, Options considered, Decision, Security/reliability/cost implications, Validation evidence, Consequences and related issues/owner. Proposed and Accepted status must be explicit; no accepted ADR until reviewed.

Foundation decisions: app/frontend versions, DB engine/hosting, auth/session strategy, question revisions/transactions, durable worker lease/idempotency, IaC tool/state, AWS Region/entry/egress, Bedrock model/embedding/vector store, corpus licensing and ML/Lex gates.

Current records: [ADR 0001 — repository workflow](0001-repository-workflow.md), [ADR 0002 — .NET module boundaries](0002-dotnet-module-boundaries.md), [ADR 0003 — data, identity and runtime baseline](0003-data-identity-runtime-baseline.md), [ADR 0004 — multi-school/Ministry MVP scope](0004-multi-school-ministry-mvp.md), [ADR 0005 — durable worker lease, retry policy and idempotency](0005-durable-worker-retry-idempotency.md), [ADR 0006 — identity session and resource authorization](0006-identity-session-and-authorization.md), and [ADR 0007 — failure handling, stale lease recovery, and provider resilience](0007-failure-handling-stale-lease-recovery-resilience.md). ADR 0004 records the project owner's product-scope decision; ADR 0005 defines worker retry and idempotency; ADR 0006 records session and authorization boundaries; ADR 0007 defines failure mapping, stale lease recovery, and circuit breaker resilience.
