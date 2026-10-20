# M3-02 Identity schema handoff for M1 review

**Status:** Implemented on the M3 branch against local SQL Server `AiExamBankDev`. UI was deferred by the user. These are M3's implementation decisions; they do not assert team-wide approval of ADR 0003.

## Current shape

| Table/column | Purpose and constraint |
|---|---|
| `Schools(Id, Name)` | Stable school ID used by users and role scopes. |
| `Departments(Id, SchoolId, Name)` | Department belongs to one school. `(Id, SchoolId)` is a referenced alternate key. |
| `AspNetUsers.SchoolId` | Nullable home school; null supports Ministry-only accounts. School staff must have a school before school-scoped roles can be granted. |
| `RoleAssignments` | Existing role shape check; new school FK, `(DepartmentId, SchoolId)` FK, and unique `(UserId, Role, SchoolId, DepartmentId)` index. |
| `RoleAssignmentAudits` | Actor, target, grant/revoke action, role, scope and UTC timestamp; written with the role change in one `SaveChangesAsync` transaction. |

The M3 admin API allows `SchoolAdmin` to grant/revoke only `Teacher` or `DepartmentHead` for another user in the same school. `MinistryAdmin` can grant/revoke `SchoolAdmin` for a user already associated with the target school. The API rejects self-changes, `MinistryAdmin` grants, invalid department/school pairs, and duplicate assignments. Authorization reads current DB assignments for every request, so revoking a role takes effect even if the cookie remains valid.

The admin API also creates schools (`MinistryAdmin`), departments (MinistryAdmin or own-school SchoolAdmin), and new accounts (SchoolAdmin may create scoped Teacher/DepartmentHead; MinistryAdmin may create SchoolAdmin). `GET /api/v1/admin/schools/{schoolId}/users` lists own-school accounts for SchoolAdmin and SchoolAdmin accounts for MinistryAdmin; `GET /api/v1/admin/schools/{schoolId}/role-assignments` follows the same visibility rule. Both endpoints return the server's current assignments, never roles from a cookie claim. POST/DELETE requests require an antiforgery token.

The development-only `LocalDemoUser:BootstrapMinistryAdmin=true` option gives the configured `demo.user` an initial MinistryAdmin assignment after verifying its configured password. Its default is false. Use it only with an isolated development database: disabling the option later does **not** revoke a role already inserted. For production, a separate one-time `--bootstrap-ministry-admin` command creates a new account and assignment atomically, only while no MinistryAdmin exists. There is no public API for creating MinistryAdmin accounts.

Authentication uses ASP.NET Core Identity password hashing, an HttpOnly cookie, antiforgery protection on mutations, and login rate limiting. The cookie lifetime defaults to 1800 seconds; `Auth:SessionLifetimeSeconds` permits 1–86400 seconds to test expiry. Set a short lifetime only for a separate test run.

`ResourceAccessPolicy` and the DB-backed `ResourceAuthorizationService` now implement school-resource decisions for Question, Blueprint and Job, with unit tests for ownership, school/department scope, self-review and current-role revocation. The modules currently have contracts/domain types but no resource API or persistence records to load before calling this service. Endpoint guards and direct-API IDOR tests still require those actual resources; the current admin IDOR checks do not cover them. Ministry exam content remains denied until event assignments have a persisted contract.

## M3 implementation decisions and integration notes

1. MVP uses one nullable `AspNetUsers.SchoolId` per account. School staff have one home school; cross-school membership would require a later schema change.
2. `Schools` and `Departments` are created by the Identity migration because role scope has foreign keys to them. Future modules should reference these stable IDs and add changes to the same migration stream.
3. Role changes use the dedicated `RoleAssignmentAudits` table, written in the same database transaction. Other workflow audit is outside this slice.
4. An operator provisions the first production `MinistryAdmin` through the one-time CLI command documented in [feature notes](M3-02_feature_notes.md). Schools, departments and staff are then provisioned through the admin API.

The migration is `SchoolScopeAndRoleAudit`. It was applied to `AiExamBankDev` locally. The repository's ADR 0003 still describes PostgreSQL as a proposal and needs a separate team update to reflect the SQL Server implementation before a team-wide architecture claim is made.
