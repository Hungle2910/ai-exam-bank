# Module ownership

This directory holds the application modules of the .NET modular monolith. Each module has a buildable project with a small contract or value object. This is the integration skeleton; authentication, approval, CRUD, import, AI drafting and durable jobs are not implemented by these types. A module owns its business rules and persistence boundary; the API host wires modules together when the first use case lands.

| Module | Primary owner | Review partner | Boundary |
|---|---|---|---|
| Identity and Review | @Lancelot-sys25 | @Hungle2910 | Identity, permissions, human decisions and audit |
| Questions | @flwndyy | @Lancelot-sys25 | Question revisions and taxonomy |
| Knowledge | @flwndyy | @Hungle2910 | Source/citation lifecycle and RAG contracts |
| Exams | @Hungle2910 | @flwndyy | Blueprints, approved-only selection and final snapshots |
| Import | @HuuPhuoc-NH | @flwndyy | Validation, preview and idempotent import requests |
| Jobs | @TranGiaBao2005 | @HuuPhuoc-NH | Durable state, attempts, leases, retry and telemetry |

Module names and paths are defined here so incoming code can land in one predictable place. The team must review changes to a shared contract with its consumers before implementation. Do not put business rules in the API host or copy another module's data writer. See [architecture](../../docs/ARCHITECTURE.md) and [team ownership](../../docs/TEAM.md).

Current code is deliberately small: `Identity/Contracts` carries actor identity, `Questions/Contracts` carries revision identity, `Review/Contracts` carries a proposed decision request, `Exams/Domain` validates blueprint slots, and `Import`, `Knowledge`, `Jobs` carry stable identifiers/citations. These are starting contracts for owner review, not released API schemas. The approved-question read interface must include authorization/scope and belongs in the first Questions/Exams integration PR. Add `Application`, `Infrastructure` and `Endpoints` only alongside real implementation and tests. Modules must not reference `Api`, `Worker`, persistence providers or AWS SDKs; `tests/Architecture.Tests` checks this compile-time boundary.
