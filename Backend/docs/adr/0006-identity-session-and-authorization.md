# ADR 0006 — Identity session and resource authorization

**Owner:** M3 / Minh Phúc. **Related:** M3-01, M3-02, [permission matrix](../IDENTITY_AND_PERMISSIONS.md), [ADR 0003](0003-data-identity-runtime-baseline.md).

## Status

Proposed for M1/M3 and security review. This record documents the path already implemented for school administration; it does not mark the broader architecture or Ministry exam workflow accepted.

## Context/requirement

The MVP has four business roles, multiple schools and a separate Ministry exam scope. An actor can have several assignments. A role name alone cannot authorize access to a question, blueprint, job or review decision: the server must verify the current assignment against the persisted resource and prevent self-review. Browser login also needs expiration, logout and CSRF protection without custom cryptography.

## Options considered

| Option | Trade-off |
|---|---|
| ASP.NET Core Identity password store and same-origin cookie | Fits the existing .NET host and browser UI; requires antiforgery, cookie settings and session lifecycle. |
| External OIDC/Cognito | Useful if school federation becomes a requirement; adds provider setup, claims mapping and operational dependencies that have not been agreed for MVP. |
| Custom bearer/JWT login | Adds token issuance, revocation, rotation and storage risks without a demonstrated MVP need. |

## Decision

Use ASP.NET Core Identity for password hashing and a server-managed application cookie for human browser sessions. The API currently uses `HttpOnly`, `SameSite=Lax`, a Secure cookie outside Development, 30-minute default sliding expiry (configurable through `Auth:SessionLifetimeSeconds`), antiforgery token header `X-CSRF-TOKEN` on mutations, and a per-IP login rate limit. These are implementation facts, not a claim that every deployment has passed security review. `GET /auth/me` returns current database role assignments; handlers must not trust a role or scope supplied by a client or cached in the cookie.

Authorization has two stages: authenticate the actor, then load the target resource and current assignments from the database before evaluating action, owner, school, department, submission state and reviewer identity. Collection routes must filter by the same scope before returning records. `SchoolAdmin` and `MinistryAdmin` do not inherit access to exam content. Ministry exam content stays denied until an explicit event-assignment model and tests exist. AI, background workers and operational IAM identities have no human approval authority. The existing Job engine bearer scheme is separate from the human Identity cookie and must not be interpreted as a human role grant.

The current implementation covers login/logout, `/auth/me`, CSRF and school administration. `ResourceAccessPolicy` is implemented and tested for school Question/Blueprint/Job decisions, but Question/Blueprint resource endpoints have not yet been wired to persisted resource loads. Review and Ministry event decisions are future integrations; see [M3-01 contract/evidence](../M3-01_SECURITY_CONTRACTS.md).

## Security, reliability and cost implications

- IDOR: use actor identity and persisted resource scope; a URL/body `schoolId`, `departmentId` or owner ID is never proof of authority. Return denial without leaking cross-school content.
- Privilege escalation and stale permissions: read current role assignments from the DB for every protected decision; revoke and audit atomically. Disallow self-grant and elevated grants through school admin routes.
- CSRF and XSS: require antiforgery on cookie-authenticated mutations; keep credentials out of browser storage, escape untrusted UI content, avoid unsafe HTML rendering and apply a reviewed Content Security Policy before deployment.
- Review integrity: recheck reviewer, immutable revision and expected version inside the write transaction; audit and state transition commit together. A conflicting version returns `409`.
- Session and secrets: protect HTTPS and data-protection keys across replicas, use the environment secret channel for bootstrap credentials, and test expiration/logout. The cookie approach needs no paid identity provider by itself; deployment cost remains with M2.

## Validation evidence

`Backend/tests/Modules/AiExamBank.Modules.Identity.Tests/` covers role shape, school-role assignment and the school resource policy. `Backend/docs/M3-02_identity_schema_handoff.md` records the implemented SQL Server schema and school admin behavior. The endpoint and threat-model gap list in [M3-01 contract/evidence](../M3-01_SECURITY_CONTRACTS.md) identifies tests that require M4/M1/M5 resource APIs and Review persistence. No live AWS or production session claim is made here.

## Consequences and related issues/owner

M3 owns the human auth/resource-policy contract and must review future endpoint integrations with M1/M4/M5. M1 must reconcile ADR 0003's old PostgreSQL proposal with the team's SQL Server selection and existing EF implementation. M2 owns runtime/CI IAM and secret delivery; its policy drafts require account-specific validation. Mark this ADR Accepted only after the responsible reviewers agree to the session, identity and resource-scope boundaries.
