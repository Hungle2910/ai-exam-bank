# Application implementation boundary

The initial .NET 10 API host lives in `Api/`. From the repository root, run `dotnet run --project src/Api/AiExamBank.Api.csproj`; `GET /health/live` returns a liveness response. This proves only that the process is running. It does not prove database, worker, AWS or business readiness.

New business code belongs in `Modules/<Module>/` and must follow the ownership table in [Modules/README.md](Modules/README.md). Add Worker/Web projects with their first executable behavior and reviewed ADRs. Do not add placeholder production logic merely to make the tree appear complete. See [architecture](../docs/ARCHITECTURE.md).
