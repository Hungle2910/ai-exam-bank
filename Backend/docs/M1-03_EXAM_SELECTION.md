# M1-03 — Approved question selection and gap reports

Refs #11. This change is stacked on M1-02 / PR #82, which is stacked on M4 / PR #81. Neither dependency has been merged into dev. Independent review and integration CI are required before closing the task.

## Owner decisions, 2026-11-07

- A Teacher may select approved questions from their department within their school, including another author's approved questions. Other departments/schools remain excluded.
- Exams provides a cookie-authenticated bridge to the existing Jobs contracts. M3 cookie authentication, M5 Bearer endpoints, worker leases/retry rules and existing job types are unchanged.
- M1-04/M1-05 wait for M2–M4's adapter and AWS configuration. No Bedrock calls, AI-generated fixtures presented as real output, or AWS deployment are part of this change.

## Request and execution

`POST /api/v1/exams/generate` requires authentication, current CSRF token and `Idempotency-Key` (1–128 characters). Body:

```json
{
  "blueprintId": "<persisted blueprint UUID>",
  "blueprintVersion": "<Base64 rowversion from the blueprint API>",
  "seed": 42
}
```

Returns 202 with an exam ID and `/api/v1/exams/{id}/selection` location. Ownership/school/department are loaded from the persisted blueprint and current assignments, not accepted from the client. Stale/archived matrices and reused keys with different requests return 409. A same-key identical retry returns the same exam ID. Database deadlocks return 503 so clients can retry the same key.

The database stores the request and pinned matrix before dispatch. ExamOutboxDispatcher sends requests without a delivery marker to IJobPublisher using the exam ID as its stable idempotency key. A restart can repeat enqueue without creating another job. It also reconciles a missing marker when a worker has already committed the result. ApprovedExamSelectionHandler is an M1-owned job type; it resolves a fresh scoped context and rechecks authorization and the current blueprint version before selecting.

Selection consumes IApprovedQuestionReader only. It checks school, department, subject, topic and difficulty; excludes previously selected question identities; pins exact revisions and revalidates their approval. Candidate pages are bounded to the reader's 500-item limit. SHA-256 ordering by seed and question ID gives repeatable selection for an unchanged ordered candidate pool. This is bounded candidate selection, not a uniform random sample of the entire bank. At most 1000 questions are selected under the Blueprint input limits. Per-question revalidation remains a performance measurement target for M1-07.

Serializable SQL transactions cover the approved reads and result write. ExamItems store pinned revision/content/points; ExamGapReports store each exact deficit. Successful replay of the same job returns the existing result. An incomplete result is PARTIAL; a fully populated selection is NEEDS_REVIEW, never a finalized/published exam. No endpoint edits the stored result. M1-06 must implement finalization and its own revalidation later.

`GET /api/v1/exams/{id}/selection` rechecks current Teacher scope before reading or calling IJobTracker. It exposes selected revision references and deficits, not answer keys. Items/gaps use split queries to avoid a multiplicative join. While a worker commits, a poll may transiently show QUEUED with result rows; clients continue polling until a terminal result. The UI shows actual server/job states and no simulated progress.

## Verification

- Release solution tests: 195 passed, including six new selection tests and SQL Server persistence tests.
- SQL integration: empty-bank deficits, idempotent requests/replays, request-key conflict, wrong actor/school denial, approved-only reads, another author's same-department questions, pinned revisions and exact points. Approved fixtures are constructed only in the isolated test database; this does not prove the unfinished M3 approval API.
- Frontend: 17 tests passed; GeneratePage tests cover CSRF/idempotency/version headers, real deficits and stale-blueprint rejection. TypeScript and scoped ESLint passed.
- HTTP smoke: tests/exam-selection-smoke.ps1 provisions through application APIs, then exercises cookie -> persisted request -> outbox -> M5 worker -> persisted gap result. It also checks same-key retry, changed-key payload conflict, anonymous/other-author denial and archived blueprint rejection.
- Headless Edge: real login, create/save blueprint, select via generation screen, poll worker and display persisted missing count. Local screenshots and private trace are outside the repository at E:/codex-blueprint-browser-tools/evidence. Trace data must not be published with credentials/session data.
- Migration ExamSelection creates ExamGenerations, ExamItems and ExamGapReports after BlueprintArchive, tested on an isolated SQL Server database stored on E: because C: has no free space.

## Remaining gates and limitations

- Human review, dependency integration and CI on dev remain pending; task is not marked Done.
- End-to-end approval -> selection depends on M3-03; AWS/AI, finalization, recovery fault injection and production-scale performance are not claimed tested here.
- M5's existing FileJobStore remains the queue store. SQL request durability and idempotent dispatch bridge that storage boundary; this does not migrate Jobs storage or change its reliability contract.
- The test environment and generated samples are local only. No production credentials or data were used.
