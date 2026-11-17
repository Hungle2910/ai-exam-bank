# M3 weeks 8–10: review and security handoff

This guide prepares the remaining M3 checks. It is not a release sign-off. On 2026-11-17, the review API and its revision history are on `dev` through PR #95. M3 role-scope evidence is in PR #105; AI content gate, role audit reader, review confirmation and regression checks are in PRs #106–#109 awaiting review. M1's approved selection/finalization work and M4's AI retrieval/generation are not yet on `dev`; no release candidate or tag is available.

## Week 8: core and optional extension boundary

- Keep the ordinary review queue usable when optional Lex or inference endpoints are disabled. Any future advisory score/explanation is display data only; it cannot set approval state or reviewer identity.
- If Lex/ML is enabled, bind its session to the authenticated server-side actor, validate slots before calling the blueprint API, deny cross-user session use, and require the same school/department access as the normal flow. Exercise the disabled path too.
- For AI drafts, require the M3-04 parser plus M4's trusted citation lookup and M1's writer. The browser must label missing/unverified evidence; it must not treat a model-provided URI as permission to open a source.

No Lex/ML extension endpoint or M4 citation adapter is on `dev` today, so these are integration gates, not test results.

## Week 9: acceptance record to fill on the release candidate

Record the exact commit/tag, environment URL, database migration version, test time, separate test actors, and redacted request/response IDs. Run these direct API cases after all dependent PRs merge:

| Case | Expected result | Evidence available now |
| --- | --- | --- |
| Anonymous review/audit read or decision | `401` | Role audit: PR #107 local SQL smoke; review: existing auth middleware. |
| Teacher or self-reviewer posts decision | `403` | M3-03 smoke extended in PR #109. |
| Reviewer with revoked role, same cookie | Queue and decision `403` | M3-03 smoke extended in PR #109. |
| Cross-school/department revision or citation | `403` or concealed `404` | Review domain tests; citation adapter pending. |
| Current reviewer with valid CSRF/version | One transition and one durable decision/audit event | M3-03 review decision tested; unified audit pending. |
| Stale or concurrent decision | `409`, no second decision/audit | M3-03 SQL smoke in PR #95 and PR #109. |
| AI/worker submits approval identity/state | Rejected; no approved revision | Isolated parser in PR #106; writer integration pending. |
| Finalize with draft/rejected item or missing slot | Denied; no final snapshot | M1-06 integration pending. |
| Source outside current actor scope | Denied without excerpt disclosure | M4 citation API pending. |

Also recheck session expiry, CSRF, job scope, exam ownership, answer-key access, runtime IAM, S3 and Bedrock policy, secret redaction, and production dependency advisories. A failed critical case blocks security acceptance; record the finding, owner, fix PR and retest result. Use real separate accounts, not one shared administrator.

## Week 10: demo and transfer sequence

1. Show Teacher, DepartmentHead and Admin as separate accounts. Teacher submits a revision and gets `403` on a direct approval request.
2. Reviewer opens the correct revision, inspects trusted source evidence, rejects with a reason or confirms approval. Repeat a stale decision to show `409`.
3. Query audit by actor/revision, show the recorded decision and trace that exact approved revision into the final exam snapshot. Check one event per successful mutation and none for a denied decision.
4. Hand over the user/role administration guide, API routes, migration and release versions, sanitized policy evidence, known limitations and open issue owners. Omit passwords, cookies, bearer tokens, keys and private source text from screenshots.

The final trace requires M3-05's unified audit, M4's citation evidence, M1's final snapshot, staging IAM from M2, and a release tag. Re-run this sequence on the tagged build before marking M3-09 or M3-10 complete.
