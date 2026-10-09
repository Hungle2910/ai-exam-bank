# ADR 0002 — .NET module and project boundaries

**Status:** Proposed, pending M1–M5 peer review. **Owner:** M1 / Le Doan Gia Hung. **Date:** 2026-10-09. Related: #1, ADR 0001 and [solution design](../DOTNET_SOLUTION_DESIGN.md).

## Context and requirement

Five owners must build exam, question, review, import, knowledge and job features concurrently over ten weeks. PRs #59 and #63 placed the .NET API host and seven contract/value-object module projects on `dev`; no complete business workflow, database or Worker exists yet. The MVP is one organization, one relational database and AWS deployment. Cross-module state changes include approval + audit and approved-only exam snapshots. The design must let each owner deliver a vertical slice without creating a distributed transaction or making M1 the sole implementer.

## Options considered

| Option | Benefit | Cost / risk |
|---|---|---|
| One undifferentiated API project | Few projects and quick start | Business ownership, dependencies and migrations collide in `Program.cs` and shared files |
| Layered `Domain/Application/Infrastructure` projects for the whole product | Compile-time layer boundaries | Feature owners edit each layer, cross-module interfaces become central bottleneck |
| One project per owned module, plus thin API/Worker hosts and one migration stream | Ownership and vertical slices stay clear; modest project count | Folder-level domain/adapter boundaries need review and targeted tests |
| Many projects per module or microservices | Stronger physical boundaries/independent deploy | Too much scaffolding, CI/deploy and transaction overhead for this MVP |

## Proposed decision

1. Extend ADR 0001's modular monolith: `Api` and future `Worker` are composition roots. Seven owned module projects now contain small initial contracts/value objects; add use-case folders and implementation only with executable behavior and tests. Give a module `Domain`, `Application`, `Contracts`, `Infrastructure` and `Endpoints` code only as needed. Explicit DI/route registration, no runtime assembly scanning.
2. Modules expose small provider-owned public contracts. Consumers do not read another module's tables or DbContext. M1 and both owners break dependency cycles before merge. Split an infrastructure project only when SDK/EF dependencies or tests demonstrate a boundary problem.
3. Propose one relational DbContext/migration stream in `Persistence` after the DB engine/hosting ADR is reviewed. Module owners own mapping/query files; cross-module schema/transaction changes need all affected owners. Approval, revision state and audit commit atomically; finalization pins approved revisions in a durable snapshot.
4. Keep async work in the future Worker under M5's job lifecycle. Business owners implement handlers behind its contract. The API accepts/returns job state rather than running long Bedrock/import requests inline.
5. Each feature PR adds relevant tests, API examples, configuration and CI checks. No empty module projects, fake readiness or unreviewed AWS resources.

## Security, reliability and cost implications

- M3 enforces server-side authentication, role/resource scope and negative tests at the API/use-case boundary. Cross-module contracts must carry actor/scope where needed; IAM alone cannot authorize a teacher's action.
- A shared database makes critical transitions atomic and reduces operations cost, but schema coordination is required. Migrations must be reproducible and backward compatible with deployment/rollback strategy.
- Separate API and Worker processes can restart independently. Durable jobs/leases prevent lost or duplicate effects; two processes do not imply microservices or separate databases.
- No paid AWS services are provisioned by this ADR. IAM, private network, DB hosting, egress and budget remain M2/M3 ADR decisions.

## Validation evidence and consequences

The current solution builds one API host and seven module projects, runs small domain tests and checks current module references, plus liveness/HTTP error smoke tests. It does **not** validate real persistence, authorization or cross-module workflows yet. The first Questions/Review PRs must prove public contract usage, transaction behavior and permission failures. The first Jobs PR must prove restart/idempotency. Reviewers should reject direct cross-module table access or domain imports of ASP.NET/EF/AWS SDK types.

This choice minimizes initial project and deployment complexity. Its accepted cost is that some boundaries are maintained by ownership, review and tests rather than a separate assembly for every layer. Revisit if cyclic references repeatedly appear, domain rules become coupled to provider SDKs, modules require independent scaling, or the single migration stream blocks delivery.
