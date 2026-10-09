# M5-01 — Proposed job failure matrix

No Worker or status API implements these rules yet. See [the canonical reliability design](M5_01_RELIABILITY_DESIGN.md) for lease and retry values.

| Failure | Backend decision | Safe UI state | Required proof |
|---|---|---|---|
| Duplicate enqueue with same scope/request/fingerprint | Return existing job | Existing job status | Concurrent unique-key test |
| Same idempotency key with different request | Conflict; no new job | Explain request conflict | Fingerprint conflict test |
| Invalid CSV or business validation | Terminal `Failed`, no retry | Row-level safe reason and upload correction | Validation test |
| Bedrock timeout, 429 or 5xx | Retry within three total attempts; then `Failed` | Pending/retrying with attempt count; terminal safe error | Fault injection and exhaustion test |
| Worker crash or lost heartbeat | Expire lease after two minutes; fence stale worker; next attempt if budget remains | Retrying or terminal failure | Crash-after-effect test |
| Database unavailable before enqueue commit | Return safe request failure; no job ID | Retry submission when service recovers | Transaction rollback test |
| Database unavailable during attempt | Stop effects; recover from persisted lease when DB returns | Last known state, marked stale | Recovery test |
| Unauthorized status/result/retry | Deny by persisted school or assigned Ministry event scope | No data disclosed | Direct API 403/404 tests |

SageMaker remains a gated extension; it is not a baseline job dependency. Raw exception traces, payloads, answer keys and personal data must not appear in user status or central logs.
