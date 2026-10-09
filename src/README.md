# Application implementation boundary

The initial .NET 10 API host lives in `Api/`. From the repository root, run `dotnet run --project src/Api/AiExamBank.Api.csproj`; `GET /health/live` returns a liveness response. JSON clients receive RFC Problem Details for unknown routes and unhandled failures, including a trace ID. `Directory.Build.props` applies shared compiler settings to every .NET project. Liveness proves only that the process is running. It does not prove database, worker, AWS or business readiness.

New business code belongs in `Modules/<Module>/` and must follow the ownership table in [Modules/README.md](Modules/README.md). Add Worker/Web projects with their first executable behavior and reviewed ADRs. Do not add placeholder production logic merely to make the tree appear complete. See [the .NET solution design](../docs/DOTNET_SOLUTION_DESIGN.md), [foundation checklist](../docs/DOTNET_FOUNDATION.md) and [architecture](../docs/ARCHITECTURE.md).
