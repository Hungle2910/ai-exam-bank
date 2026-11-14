# M1-05 — Durable draft mappings (storage stage)

Refs #21; stacked on PR #89. This PR implements draft persistence and an owner-scoped preview. Live generation waits for the M2–M4 adapter and configuration. Submission and decision actions depend on M3 review.

## Storage and contract

`ExamDraftStore.SaveAsync` is a trusted worker boundary, not a client endpoint. A future authorized handler must obtain the actual job identity and its exam/actor scope from M5, load server-side gaps and call the M1-04 orchestrator. Never accept job IDs or validated outputs directly from an untrusted client. Resolve the store and `IQuestionWriter` in a fresh scope sharing the same `AiExamBankDbContext`; do not carry unrelated pending edits into the transaction.

The store rechecks exam ownership, school and current Teacher department assignment. New writes additionally require a PARTIAL exam and a current, unarchived blueprint version. It validates each supplied slot against stored deficits, question taxonomy/count/difficulty and trusted citation references before writes. Slots can be persisted in subsets; each submitted slot is atomic. Only REVIEW_REQUIRED outputs with exactly the missing count, or FAILED outputs with no drafts and a bounded safe code, are accepted.

Three new tables belong to Exams:

- ExamDraftBatches: exam/job identity, SHA-256 fingerprint of slot-ordered output, timestamp.
- ExamDraftSlots: per-job slot status, correlation ID and safe error code.
- ExamDraftLinks: slot/ordinal to question ID + pinned revision + job + citation. Its primary key is unique across jobs for the same exam slot/ordinal.

The transaction uses SERIALIZABLE isolation. An identical replay returns existing links; changed output for the same exam/job fails with a concurrency exception. A new job cannot fill a slot that already has draft links. FAILED slots have no links, so a subsequent job may retry them. A deadlock/unique race must be retried through the future M5 handler using a fresh scope and the same persisted validated output, not by blindly regenerating different content under the same job identity. Concurrent-race behavior has not been load-tested.

All questions are created through M4 `IQuestionWriter` with `QuestionProvenance.AiDraft()`. The writer joins the transaction. Any failure rolls back the batch, links and questions together, and clears tracked state. A question remains DRAFT; slot REVIEW_REQUIRED describes orchestration output, not submission into M3's approval queue. This change does not add questions to approved exam items, remove deficits or promote the exam from PARTIAL.

The existing selection read includes `drafts` with slot, question ID and revision references for the owner with current Teacher access. The generation page groups them by gap and opens a provisional view through the existing M4 question detail API. A changed revision is reported instead of displaying its contents as the original. The preview omits answer keys and leaves the approved totals untouched.

## Validation

Migration `ExamDraftMappings` was applied to the isolated local SQL database `AiExamBankM1Selection`. Release solution tests passed: 207, including 14 persistence tests with SQL enabled. The extended selection integration test exercises real M4 question writes, same-job replay, changed-output rejection, cross-job duplicate prevention, wrong actor/school, invalid count, FAILED slot retry, injected second-write failure and complete rollback. It asserts AI provenance/DRAFT status and unchanged exam items/PARTIAL status. Fixtures do not call AWS or prove the review workflow.

Run tests after migrating an isolated database, setting `BLUEPRINT_TEST_CONNECTION` to its SQL connection string:

```powershell
dotnet test Backend/AiExamBank.slnx --configuration Release --verbosity quiet
```

## Remaining acceptance criteria

Remaining acceptance criteria include the live M4 adapter and M2 region/model/KB/IAM configuration, M5 job binding and recovery, M3 submission and decision, UI navigation into review, and live AWS evidence. Aggregate status can advance when the review contract is connected. A generated draft becomes eligible for selection only after independent human approval of its revision.
