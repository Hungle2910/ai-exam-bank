# Module ownership

This directory holds the application modules of the .NET modular monolith. A module owns its business rules and persistence boundary; the API host wires modules together. Create a module when its first executable behavior is implemented, not as an empty placeholder.

| Module | Primary owner | Review partner | Boundary |
|---|---|---|---|
| Identity and Review | @Lancelot-sys25 | @Hungle2910 | Identity, permissions, human decisions and audit |
| Questions | @flwndyy | @Lancelot-sys25 | Question revisions and taxonomy |
| Knowledge | @flwndyy | @Hungle2910 | Source/citation lifecycle and RAG contracts |
| Exams | @Hungle2910 | @flwndyy | Blueprints, approved-only selection and final snapshots |
| Import | @HuuPhuoc-NH | @flwndyy | Validation, preview and idempotent import requests |
| Jobs | @TranGiaBao2005 | @HuuPhuoc-NH | Durable state, attempts, leases, retry and telemetry |

Module names and paths are defined here so incoming code can land in one predictable place. The team must review changes to a shared contract with its consumers before implementation. Do not put business rules in the API host or copy another module's data writer. See [architecture](../../docs/ARCHITECTURE.md) and [team ownership](../../docs/TEAM.md).
