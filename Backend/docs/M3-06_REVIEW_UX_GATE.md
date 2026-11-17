# M3-06 review UX and security gate (partial)

The `dev` review API already checks the current DepartmentHead assignment, school and department, self-review, latest revision, and expected version before accepting a decision. It writes the revision state and decision history in one `SaveChangesAsync`. M3-03 direct API smoke covers Teacher `403`, successful reviewer approval and stale decision `409` on isolated SQL Server.

This slice makes the browser require explicit approval confirmation for the selected revision. It validates the pending API response before displaying any decision control, blocks a mutation without a CSRF token, reloads after `401`/`403`/`409`, and removes confirmation after those failures. The server remains the authority for permissions and version checks.

## Remaining acceptance checks

- Exercise anonymous, Teacher, reviewer, revoked reviewer, self-review, cross-school and cross-department direct calls against the combined M1/M3/M4 flow.
- Verify that AI workers cannot approve or finalize, and that answer-key visibility follows the final exam permission policy.
- Check the audit event chain and citation evidence once M4-05 and the M1-06 finalization path are on `dev`.
- Run the same matrix on staging with M2 runtime IAM and configuration. This slice does not claim the full M3-06 security gate is complete.
