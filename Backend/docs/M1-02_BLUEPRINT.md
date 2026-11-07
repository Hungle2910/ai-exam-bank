# M1-02 Blueprint implementation and verification

Implementation on `codex/m1-02-blueprint`, based on M4 PR #81 at `ae809da`.
Do not merge before the M4 dependency and independent human review. Refs #6.

## Implemented

- Authenticated GET list/detail, POST and PUT under `/api/v1/blueprints`.
- School and author derived from the authenticated database user; department comes from the writable M4 catalog subject.
- Only the author with a current Teacher assignment in the resource department can read/update a saved blueprint. Inaccessible IDs return 404.
- Subject is fixed after creation in this initial implementation. Ministry event assignments and submission/review are separate unfinished workflows.
- Server validates topic membership, difficulty, duplicate topic/difficulty cells, counts, decimal precision and declared totals.
- SQL Server rowversion, supplied as Base64 `version` on PUT, prevents stale writes. Replacing slots and updating the parent use one SaveChanges transaction.
- UI uses AuthContext.apiFetch for session handling and explicitly attaches the current csrfToken to mutations. It displays validation errors and retains unsaved edits on 409/network failures. Reload after conflict asks before discarding edits.
- Migration adds only Blueprints/BlueprintSlots plus their keys/indexes; it follows M4 QuestionBank. No production-owned blueprint is seeded automatically.
- User selected archival on 2026-11-07. `POST /api/v1/blueprints/{id}/archive` accepts `{ "version": "<Base64 rowversion>" }`, applies the same author/school/department checks and CSRF validation, and records ArchivedAtUtc. No blueprint or slot is deleted. Default lists omit archived entries; `?archived=true` lists only archived entries. Detail remains readable to the authorized author. Archived entries are read-only; old edits cannot overwrite archival because it changes rowversion. This preserves the archived matrix, not a revision log of every edit before archival.

## Input limits

Name 1–200 characters, duration 1–1440 minutes, 1–1000 total questions, 1–200 cells, positive points up to 10000 with four decimal places. Each cell has topicId, EASY/MEDIUM/HARD, questionCount and pointsPerQuestion. TotalQuestions and totalPoints must match cells exactly.

## Verified locally on 2026-11-07

- API build: no warnings/errors.
- Release solution tests: 188 passed, including the SQL Server integration test below.
- Frontend unit tests: 15 passed, including five Blueprint tests for conflict, server errors and load failure.
- TypeScript check passed. Full frontend lint has existing errors outside Blueprint; targeted Blueprint lint was checked after the async-fetch effect annotation.
- Migration applied successfully from an empty isolated SQL Server LocalDB database `AiExamBankM1Blueprint`.
- SQL integration test verifies decimal roundtrip, topic rejection, another user/school isolation, loss of access after role revocation, two contexts sharing an old rowversion, and rollback of the losing write.

To run the database test, set BLUEPRINT_TEST_CONNECTION to an isolated migrated SQL Server database and run the Persistence.Tests project. The test creates uniquely identified fixtures and retains them for inspection. Without that environment variable the database test is inconclusive; unit tests do not prove database integration.

## Still required before completion

- Archival is now in scope; deletion/restoration and a full edit revision log are not implemented.
- HTTP smoke passed using the application bootstrap/provisioning APIs on isolated LocalDB AiExamBankM1BlueprintHttp2: create/read/update, totals 400, stale version 409, anonymous 401, SchoolAdmin create 403, other author read/archive 404, absent CSRF 400, archive/read-only 409, active/archive list separation. Run tests/blueprint-smoke.ps1 against an isolated test API with administrator credentials supplied through environment variables.
- Headless Edge browser journey passed: login, create, save, archive, reopen read-only. Local screenshots and Playwright trace are retained outside the repo at E:/codex-blueprint-browser-tools/evidence; traces are private test artifacts, not public uploads.
- The HTTP script creates isolated school/department/subject mapping and Teacher-owned example matrices using the application APIs; browser validation uses that catalog. It never provisions production fixtures.
- Review endpoint/application layering, shared migration diff, UI behavior and negative permission cases before PR.
- Repository checker: 496 tracked files, zero findings; repository tests: 20 passed. Migration drift check passed after BlueprintArchive. Independent human approval and dependency merge remain required.
- Subsequent M1 tasks remain pending; this work does not implement approved question selection, Bedrock orchestration or finalization.

No AWS deployment or task completion is claimed.

## Follow-on decision

On 2026-11-07 the owner confirmed M1-03 may select approved questions from the Teacher’s department within the same school. Cross-school/cross-department selection remains forbidden. This decision is for the Exams consumer; it does not change M3/M4 endpoints.
