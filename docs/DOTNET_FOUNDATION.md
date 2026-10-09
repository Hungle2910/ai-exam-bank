# .NET foundation status and next gates

**Assessment date:** 09 Oct 2026. **Owner:** M1 / Le Doan Gia Hung. This page describes the proposed foundation in PR #59; until that PR is reviewed and merged into `dev`, it is not part of the shared integration branch.

## What works in the foundation PR

| Area | Evidence | Limit |
|---|---|---|
| Toolchain | `global.json` pins .NET SDK 10.0.400; `Directory.Build.props` applies net10.0, nullable reference types, implicit usings, deterministic builds and warnings as errors | Deployment runtime is not selected or tested |
| Solution | `AiExamBank.slnx` builds the HTTP API host | No business module or Worker project exists |
| HTTP baseline | `GET /health/live` returns HTTP 200; JSON clients receive RFC Problem Details with a trace ID for unknown routes and unhandled errors | Liveness does not check DB, AWS, jobs or product readiness; business error codes are still pending |
| CI | Application CI restores/builds Release and runs HTTP smoke checks for liveness and 404 Problem Details | No product, auth, database or AWS integration tests; this check is not yet required by branch protection |
| Team boundaries | `src/Modules/README.md` and CODEOWNERS name module owners and reviewers | Boundaries are documented, not yet enforced through implemented interfaces |

The API host is a **working starting point**, not a complete .NET application or production-ready AWS deployment. Do not add empty projects, fake readiness probes or placeholder endpoints merely to make the directory tree look complete.

## Build the next vertical slices in dependency order

| Gate | Owner / coordination | Minimum completion evidence |
|---|---|---|
| 1. Review foundation | M1; peer review by M2/M3 | PR #59 green, independent approval, review threads resolved, merged to `dev`; require Application CI after its first successful run on `dev` |
| 2. Record shared contracts | M1 with M2–M5 | Reviewed ADRs for database, API version/error schema, identity flow, module dependencies and local setup; one agreed migration owner per schema change |
| 3. First business path | M4 Questions + M3 Identity/Review; M1 consumes their contracts | Executable module code, migrations, authorization, revision/audit invariants, API examples and meaningful positive/negative/concurrency tests |
| 4. Durable background path | M5 Jobs with M2 deployment and M1 generation | Worker host added with its first real handler, persisted job/attempt state, lease/retry/idempotency behavior, restart test and failure telemetry |
| 5. Integration and release | M1/M2 with all module owners | Frontend end-to-end flow, exact-commit CI and AWS deployment evidence, private config/IAM, migration and restore procedure, health that reflects real dependencies, rollback proof |

Each gate is tracked through the relevant issues in the [implementation plan](IMPLEMENTATION_PLAN.md) and [Project board](https://github.com/users/Hungle2910/projects/4). An owner should add a project or NuGet package only when its first behavior needs it; then pin the dependency, add reproducible restore/lockfile handling where applicable, and extend CI to test that behavior.

## Recommended .NET layout as modules arrive

```text
AiExamBank.slnx
Directory.Build.props
src/
  Api/                 HTTP composition root; no business rules
  Modules/
    <OwnedModule>/     domain, application and infrastructure code owned by one team member
  Worker/              add with the first durable job handler (M5)
tests/
  smoke/               HTTP host checks already present
  <OwnedModule>.Tests/ add with meaningful module behavior
```

Keep module domain rules independent of ASP.NET and AWS SDK DTOs. API/Worker compose modules and adapters; cross-module writes go through an agreed application contract. The module owner provides tests for its own invariants and failures. M1 reviews integration contracts, while M2/M3 review deployment and security boundaries. This keeps one deployable monolith without forcing every feature into `Program.cs`.

Run the current baseline from the repository root:

```bash
dotnet build AiExamBank.slnx --configuration Release
python tests/smoke/test_api_health.py
```

These commands verify the API baseline only. The remaining gates require their own runnable tests and deployment evidence before anyone labels the system complete.
