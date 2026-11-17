# M3-04: AI safety and IAM review

Status: strict model-output gate prepared on `dev` with focused tests. M1/M4 AI adapters and live Bedrock/S3/IAM validation remain dependencies; this document does not claim end-to-end M3-04 acceptance.

## Controls added

- `AiDraftSafetyGate` accepts a narrow JSON content shape (`stem`, four `options`, `correctOption`, optional `explanation`). It rejects unexpected or duplicate fields, including model-supplied `status`, `reviewerId`, and `citations`. The accepted object always has `Draft` state. Its result carries policy version `m3-ai-draft-v1`, action `REVIEW_REQUIRED` or `BLOCK`, and a safe validation reason on block. The question writer must still validate taxonomy, scope, revision immutability, and its own MCQ invariants before persistence. No persistent policy decision field exists yet.
- M3-03 review API and persistence are on `dev` through PR #95. Its decision path derives the reviewer from the authenticated session and checks the current assignment, revision and expected version. AI workers must have no human session or approval endpoint access. The model-output gate in this slice is deliberately separate from M4/M1 AI generation until their adapter contract is available.

## Local IAM and infrastructure review

| Area | Repository evidence | Result / remaining action |
| --- | --- | --- |
| S3 public access | `infrastructure/modules/private-bucket/main.tf` sets all four public-access-block flags and denies insecure transport. | Static configuration is present. Verify effective bucket/account policies and live public-access block after deployment. |
| S3 endpoint | `infrastructure/environments/staging/storage.tf` limits endpoint actions/resources to the named data bucket but uses `Principal = "*"`. | Endpoint policy is not identity authorization. Verify runtime role and bucket policy together; do not use endpoint policy as the only corpus access control. |
| Runtime IAM | `infrastructure/security/render_policies.py` scopes app S3 access to imports and releases and denies IAM mutation, state access, and cross-secret reads. | No corpus prefix or Bedrock retrieval/model permission is present. M4/M2 need exact KB/model/S3 resources and actions before a least-privilege AI policy can be reviewed. Do not add a broad `bedrock:*` grant. |
| Secrets | Runtime templates use exact SSM parameter and KMS key context per app/DB role. | Local template only; effective permissions, key policy, boundary, and live allow/deny remain unverified. |
| Network | `infrastructure/environments/staging/network.tf` has separate entry/app/DB groups. | Review live rules and egress path for Bedrock with M2 before retrieval proof. |

## Integration contract for M4/M1

1. Treat retrieved text and model output as untrusted data. Provider and citation metadata come from the trusted retrieval adapter, never from generated JSON.
2. Call `AiDraftSafetyGate.Parse` before passing model content to the question writer. Only the writer creates a revision, with `Origin=AI_DRAFT` and initial `DRAFT`; no AI DTO may set `APPROVED` or reviewer identity.
3. Before exposing a citation or excerpt, resolve its source from persisted metadata and recheck the current actor's school/department/event scope. A source URI or model-provided source ID does not grant access.
4. Render question text, options, explanations and source excerpts as text. If rich content is later required, use an explicit sanitizer and tests for that renderer.
5. Record policy version/action and reasons for blocked or review-required content when Guardrails is integrated. Advanced Guardrails configuration remains SHOULD and does not replace the app checks above.

## Remaining verification

- Direct API tests for AI-created draft, fake approval fields, rejected/approved transition, stale version and cross-scope citation access once the M1/M4 AI writer and Knowledge APIs exist.
- Live IAM simulation and allow/deny checks for exact S3 prefix, KB, model and secrets; capture redacted policy diff and evidence.
- Direct API proof that an AI worker cannot call the human review decision path, and that failed/unavailable review API cannot mark a revision approved.

M4-04 retrieval and citations, M4-05 generation, and the M1-04 adapter are not on `dev`. These gaps prevent final M3-04 acceptance today.

This PR verifies the isolated parser and its rejection of model-supplied approval, reviewer and citation authority. It does not verify the UI, live AI path or AWS resources. Record the final .NET test result in the PR evidence after CI runs.
