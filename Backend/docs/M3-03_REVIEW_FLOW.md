# M3-03 — Question review flow

## Implementation update (2026-11-16)

M4-02 is now on `dev`. This branch maps the Review module's `ReviewRequired` state to
the persisted `PendingReview` state, exposes submit/pending/decision routes, and stores
one durable `ReviewDecision` for each decided revision. The browser supplies neither
the reviewer identity nor school/department scope; the API loads them from the session
and current database assignments.

Each new draft starts with `ReviewVersion = 1`. Submit changes it to 2; approve or reject
changes it to 3. This long token belongs to the revision and is distinct from M4's
byte-array `Question.RowVersion`. EF checks the original `ReviewVersion` in the SQL
update. Decision status and audit row are saved in one transaction, so a stale second
reviewer receives 409 without creating another decision. A unique database index on
`(QuestionId, Revision)` adds a second guard. Submit also updates the question row so
its existing `RowVersion` detects a concurrent edit.

The pending API pages and filters by the reviewer's assigned departments and excludes
the question owner and revision author. The question list has a submit button, and the
review page uses the real pending and decision routes. The earlier sections below are
the design record; their statements that M4-02 was unmerged are historical.

Local verification on 2026-11-16: migration applied to an isolated LocalDB database,
then `Backend/tools/m303_smoke.ps1` exercised authenticated draft creation, submit
(200), reviewer queue (one item), teacher decision (403), approve (200), stale retry
(409), and final `APPROVED` detail. Two DepartmentHead sessions raced on a second
revision: one returned 200 and one 409. The isolated database was dropped afterward.
Backend build passed; Review domain tests 6/6, Persistence tests 13/13, frontend review
and question tests 6/6, and TypeScript check passed. Remaining team-level evidence:
approved-only exam selection through M1-03 and an integrated browser walkthrough.

## Current implementation and dependency

`ReviewWorkflow` in the Review module now checks the transition, current role scope,
question owner, revision author, revision identity and expected version without depending on an HTTP
request. Its input **must** be a snapshot loaded from the persisted question revision,
not scope or status supplied by the browser. `ReviewDecisionApiRequest` validates the
decision payload. No submit/pending/decision route or database transaction is wired yet:
the M4-02 `QuestionRevision` entity, migration and writer are still on its unmerged
feature branch. These examples are the proposed M3/M4 integration contract.

| Current state | Action | New state | Required checks |
|---|---|---|---|
| `Draft` | Submit | `ReviewRequired` | Teacher owns the question in the assigned school/department; revision and version match. |
| `ReviewRequired` | Approve | `Approved` | DepartmentHead in the same school/department, neither question owner nor revision author; revision and version match. |
| `ReviewRequired` | Reject | `Rejected` | Same checks as Approve; non-blank reason required. |

The `ReviewRequired` code value corresponds to `PENDING_REVIEW` in the original
planning table. This mapping needs agreement with M4 before migration or API release.
An approved revision is immutable; later edits create a new `Draft` revision.
`Draft` and `Rejected` revisions cannot be selected for an exam.

## Proposed HTTP contract

The authenticated cookie supplies the actor ID; the body must not contain a
`ReviewerId`, `SchoolId`, `DepartmentId`, author ID or state. All write routes require
the existing `X-CSRF-TOKEN` header. Route names and version token representation are
finalized with M4 when its persistence contract lands on `dev`.

```http
POST /api/v1/questions/{questionId}/submit-review
Content-Type: application/json
X-CSRF-TOKEN: <token>

{"revision":1,"expectedVersion":7}
```

```http
GET /api/v1/reviews/pending?schoolId={schoolId}&departmentId={departmentId}&page=1&pageSize=20
```

The server derives the allowed scope from current role assignments. Filters only
narrow it; they never grant access to another school or department. The response
contains revision ID, subject/topic, author, version and submitted time. Answer
content is returned only to an authorized reviewer.

```http
POST /api/v1/reviews/{questionId}/revisions/{revision}/decision
Content-Type: application/json
X-CSRF-TOKEN: <token>

{"decision":"Reject","reason":"Incorrect answer","expectedVersion":8}
```

An Approve request uses `"decision":"Approve"` and may omit `reason`. A successful
transition returns the revision reference, new state and new version. The server
returns `400` for invalid decision/reason/version, `401` for no session, `403` for
wrong role/scope or self-review, `404` for a missing revision, and `409` for a stale
version or a revision already submitted/decided. The API must not expose answer
content in an error response.

## Persistence and concurrency gate

The M4 revision's status update, one `ReviewDecision` audit row and its expected
version check must commit in **one database transaction**. The update must have a
database-enforced compare-and-swap condition; a prior in-memory check alone does
not prevent two reviewers from approving the same revision. If the conditional
update affects zero rows, return `409` and do not insert a decision. The pending
query must page and filter server-side by persisted resource scope.

The M4 draft currently uses a `Question.RowVersion` byte array while this M3
contract uses a `long ExpectedVersion`; M3 and M4 must choose one token format
before wiring the route. Do not silently cast the rowversion to a number.

## Evidence and remaining work

Six `ReviewWorkflowTests` pass for valid transitions, owner/scope/self-review,
missing reject reason, stale revision/version and a second sequential decision.
These are pure domain tests, not proof of database concurrency or live API behavior.
The Review page now has a pending/detail UI baseline: it requests the proposed
pending endpoint, sends a decision with revision and expected version, and reloads
on a 409. Three frontend tests cover API unavailability, decision payload and stale
response. It does not display mock review decisions or claim a server-side transition.
The pending response shape and version token must be reconciled with M4 before the
UI can be used against real data.

After M4-02 is merged into `dev`: map persisted status to the agreed state,
implement the transaction and routes, add integration tests with two concurrent
reviewers and 403/409 responses, then verify the UI with the real endpoint. AWS
and live database evidence remain pending.
