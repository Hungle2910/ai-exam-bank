# M3-03 — Question review flow

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
After M4-02 is merged into `dev`: map persisted status to the agreed state,
implement the transaction and routes, add integration tests with two concurrent
reviewers and 403/409 responses, then build the pending/detail UI baseline. AWS
and live database evidence remain pending.
