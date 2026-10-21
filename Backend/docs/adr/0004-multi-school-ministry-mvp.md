# ADR 0004 — Multi-school and Ministry exam scope in MVP

**Status:** Accepted for product scope on 2026-10-09 by M1 / Le Doan Gia Hung (project owner). Implementation contracts remain to be reviewed by M1–M5.

## Context

The initial ten-week, 50-package plan assumed one school and a generic Teacher/Reviewer/Admin model. The project owner has now selected multiple schools and Ministry-level exams for the MVP. The existing .NET solution is a scaffold; no identity, persistence, exam workflow or AWS deployment implements this decision yet.

## Decision

1. Model `School`, `Department`, scoped role assignments and `ExamEvent` explicitly. School content is isolated by resource scope, including reads, downloads, search and background jobs. A `MinistryAdmin` does not gain implicit access to school exam content.
2. Use the four business roles `Teacher`, `DepartmentHead`, `SchoolAdmin` and `MinistryAdmin` as specified in [the permission matrix](../IDENTITY_AND_PERMISSIONS.md). School exams are selected by an assigned DepartmentHead; Ministry exams require an assigned author and a different assigned confirmer.
3. Preserve the approved-only question invariant. Accepting an AI suggestion creates draft content; every question revision in a selected/finalized exam needs independent human approval. Exam selection does not approve question revisions.
4. Keep the modular monolith and one relational transaction boundary for approval, audit and exam selection. Concurrent selection of different revisions for one event has one winner; the loser receives a conflict. Immutable revisions and audit history remain available.
5. The 50 packages / 926 hours in the original plan are a historical baseline, **not** a validated estimate for the expanded MVP. Re-estimate identity, authorization, tenant isolation, exam workflows, data migration, AWS cost, security and end-to-end testing on the Project board before claiming a delivery date.

## Consequences and validation

M1/M3/M4 must agree on actor context, school/department assignment, question-review and exam-selection contracts before migrations or endpoints. M2/M5 must scope import, source retrieval, jobs, logs and alerts to the owning school or assigned Ministry event. Tests must cover cross-school reads/writes, privilege escalation, self-review, stale revisions, concurrent selection, rollback and unreleased Ministry content. Existing deployment and estimate documents must use this scope without claiming these controls are already implemented.

The choice of identity provider, DB engine, hosting and frontend in [ADR 0003](0003-data-identity-runtime-baseline.md) remains proposed. Lex, SageMaker, VPN, student test-taking, proctoring and billing remain gated or outside MVP.
