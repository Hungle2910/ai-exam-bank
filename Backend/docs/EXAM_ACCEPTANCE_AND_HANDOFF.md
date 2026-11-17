# Exam acceptance and handoff

Refs #36, #41 and #46. This records local evidence and the remaining release checks for the school exam workflow. Ministry acceptance follows the distinct actor and release rules in [ADR 0004](adr/0004-multi-school-ministry-mvp.md) and the [permission matrix](IDENTITY_AND_PERMISSIONS.md).

## Extension boundary

Keep the core demo independent of Lex and SageMaker. If the MVP gate opens those extensions, Lex output may prefill the editor's subject, topic, difficulty, counts and score for teacher confirmation. The confirmed payload goes through the existing authenticated Blueprint save route, catalog lookup, matrix validation and concurrency checks. Invalid taxonomy, low confidence, or provider failure leaves the manual editor available. A difficulty assessment may show advisory metadata with its revision and evidence. The teacher or reviewer still decides the actual difficulty. Approval and release stay in their own authorized workflows. Review feature flags and payloads with M4/M5 when their adapters are available.

## Acceptance evidence

| Requirement | Verified locally | Remaining staging evidence |
|---|---|---|
| Blueprint save, edit and archive | Cookie and CSRF HTTP tests, cross-owner denial, stale update conflict and archive history. #86 | Teacher browser journey on the integrated release candidate. |
| Select approved revisions and report gaps | Real API and separate Worker with isolated SQL Server, exact deficit, same-key replay, changed payload conflict and cross-owner denial. Approved-only and distinct IDs covered by the SQL selection fixture and profile. #88, #92 | Select questions approved through M3, consecutive requests with the same candidate set and concurrent workload. |
| Missing slot to draft and review | Contract tests for count, schema, citation and timeouts. SQL rollback, replay and scoped draft links. UI checks pinned revision before displaying a provisional draft. #89, #90 | Live retrieval and Bedrock generation with scoped IAM, then submit, reject and retry through M3/M5. Confirm a rejected draft cannot satisfy an approved slot. |
| Final snapshot and release | Local snapshot builder checks missing items, counts, score, duplicates, scope and revoked approvals. #91 | Persist the selected revision atomically, race two selections/finalize calls, edit source content and confirm snapshot stays unchanged. Verify role-based answer keys and release access. |
| Extensions disabled | The local Blueprint and selection flow uses the application API, SQL and Jobs. | Repeat the full core workflow with extensions disabled when review and finalization are connected. |
| Ministry event | The actor and release requirements are documented. | Two independently assigned actors, denial of self-confirmation and unassigned access, and no content disclosure before release. |

## Demo and release rehearsal

1. Record the candidate commit, applied migration, environment and available provider configuration. Provision isolated actors and demo data through the supported administration flow. Use separate Teacher, DepartmentHead and administrator accounts in the appropriate school and department.
2. Save a Blueprint. Run a selection with enough approved questions and another with a known shortage. Record the exam and job references, expected counts and score, and retry the shortage request with the same key.
3. Generate a draft for the shortage. Open its provisional view and check citation and exact revision. Submit through the review flow. Exercise both reject and approve, then check that only the approved revision can fill the slot.
4. Submit a fixed school exam revision, have the assigned DepartmentHead select it, finalize and display the authorized preview. Change the source question and verify the final snapshot is unchanged. Repeat the release/access checks for a Ministry event using two assigned actors.
5. Link redacted evidence to #41 and the release candidate in #46. Record CI and owner acceptance for security, corpus, deployment and recovery. Tag the reviewed candidate after staging passes on the same commit.

The demo depends on M3's approval and audit, M4's source and generation integration, and M2/M5 staging and recovery. The local evidence supports the completed portions above. The acceptance run, recording and release tag remain open until those integrations are available.
