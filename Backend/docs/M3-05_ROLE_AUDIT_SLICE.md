# M3-05 role audit read slice

`RoleAssignmentAudits` already records a grant/revoke in the same `SaveChangesAsync` call as the role change. This slice exposes those existing records through `GET /api/v1/audit/role-assignments` and connects the audit page to it. The page no longer substitutes sample events when the API fails; it shows an error and no rows. It does not claim review decisions, question edits, generation or exam finalization are audited.

The endpoint requires a current authenticated user and reads assignments from the database on each request. A SchoolAdmin can see Teacher and DepartmentHead role changes in their own school. A MinistryAdmin can see SchoolAdmin role changes across schools. Other roles receive `403`; no audit write/update/delete endpoint exists. Scope is applied in the database query before filters and pagination. The response contains display names, role/action, school and UTC time, but no password, token or secret.

Optional query parameters are `actorUserId`, `targetUserId`, `action` (`ROLE_GRANTED` or `ROLE_REVOKED`), `fromUtc`, `toUtc`, `page` (1–100000), and `pageSize` (1–100, default 50). Invalid bounds/action return `400`. Results use newest-first order with ID tie-breaker. The UI currently reads the latest 100 rows and filters that visible page locally; it does not present those rows as the complete audit history.

## Verification and remaining work

- Backend API builds locally with zero warnings and errors. Frontend TypeScript check and AuditPage ESLint pass. A direct HTTP smoke against isolated SQL Server database `AiExamBankM305_20261117` passed anonymous `401`, Teacher `403`, invalid action `400`, SchoolAdmin own-school view without other-school data, and MinistryAdmin SchoolAdmin-only view. The reproducible script is `Backend/tools/m305_role_audit_smoke.ps1`.
- Still required before full M3-05 acceptance: DepartmentHead rows, pagination stability, revocation visibility on the next request, audit transaction failure, and unified event coverage for review/question/import/generation/exam.
- The unified `GET /audit` for review, question, import, generation and exam events remains blocked on M3-03/M4/M1 integration. Its schema must keep one audit event and business mutation in the same transaction. Do not route those events through this role-only endpoint or mark M3-05 complete from this slice.
