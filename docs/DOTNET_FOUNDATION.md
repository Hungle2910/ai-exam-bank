# .NET foundation status and next gates

**Assessment date:** 09 Oct 2026. **Owner:** M1 / Le Doan Gia Hung. This page describes the scaffold in the current PR; it becomes the shared `dev` baseline only after review and merge. [Open it in Visual Studio](VISUAL_STUDIO_SETUP.md) and read the [solution design](DOTNET_SOLUTION_DESIGN.md) for the target architecture.

| Area | Implemented evidence | Remaining limit |
|---|---|---|
| Toolchain | `global.json` pins SDK `10.0.400`; common props enable net10.0, nullable types, deterministic build and warnings as errors; MSTest version is pinned centrally | Production runtime and deployment are not selected or tested |
| Solution | `AiExamBank.slnx` opens/builds the API, seven owned module projects and three test projects | Only small contracts/value objects and two domain validation examples exist; no product workflow is wired through the API |
| HTTP | `/health/live` and JSON Problem Details smoke tests | No authentication, resource endpoints or dependency readiness |
| Boundaries | Module ownership, explicit project references and architecture test reject host/provider SDK dependencies in modules | Data ownership, cross-module writes and transaction contracts need feature review |
| CI | Release build, nine MSTest cases and HTTP smoke checks | No DB/auth/worker/AWS integration or end-to-end tests |

This is a **Visual Studio-ready technical scaffold**, not a complete exam-bank application. In particular, `Persistence/README.md` and `Worker/README.md` record missing decisions; neither contains a production project. The planned frontend is also absent.

## Next delivery gates

| Gate | Owner | Completion evidence |
|---|---|---|
| Review contracts | M1 with M2–M5 | Confirm identifiers, revision rules, role/scope, API errors and module references before other PRs depend on them |
| Choose persistence/auth | M1/M2/M3/M4 | Accepted ADRs for DB provider, schema/migration owner and auth flow; secrets/config strategy |
| First stored workflow | M4 Questions + M3 Review; M1 integrates | API endpoint and persistence roundtrip; permission failures; revision and audit commit atomically; meaningful positive/negative/concurrency tests |
| Exam/import/AI flow | M1/M2/M4 | Approved-only selection, validated import, cited draft and immutable final snapshot with API/integration tests |
| Durable background path | M5 with M2/M1 | Worker project with real handler, persisted job/attempt state, lease/retry/idempotency, restart test and failure telemetry |
| Release | M2 with all owners | Frontend E2E, private AWS deployment, migration/restore/rollback and exact-commit staging evidence |

Add an `Application`, `Infrastructure` or `Endpoints` folder only with working code and tests. Add one database adapter and migration stream after the DB decision; modules never reference that adapter. API and future Worker are composition roots. Do not add fake readiness probes, placeholder endpoints or AWS resources just to make the tree appear complete.

## Verify the current scaffold

```bash
dotnet restore AiExamBank.slnx
dotnet build AiExamBank.slnx --configuration Release --no-restore --warnaserror
dotnet test AiExamBank.slnx --configuration Release --no-build --no-restore
python tests/smoke/test_api_health.py
```

These commands prove the current build, small domain rules and HTTP baseline. The remaining gates need their own runnable tests and deployment evidence before the team labels the product complete.
