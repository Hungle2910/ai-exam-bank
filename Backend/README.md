# AI Exam Bank backend

This folder contains the .NET 10 backend:

| Folder | Purpose |
|---|---|
| `Api/` | HTTP host, authentication, and endpoints. |
| `Modules/` | Business modules and their contracts; see [module owners](Modules/README.md). |
| `Persistence/` | EF Core SQL Server context, entities, and migrations. |
| `Worker/` | Planned background worker area; no runnable worker yet. |

From this `Backend/` directory, use `dotnet restore AiExamBank.slnx`, `dotnet build AiExamBank.slnx`, and `dotnet test AiExamBank.slnx`. For the API, configure the SQL Server connection locally, then run `dotnet run --project Api/AiExamBank.Api.csproj`. See the [M3 feature notes](docs/M3-02_feature_notes.md) for migration and initial admin setup.

The Next.js application is in [Frontend](../Frontend/README.md) and has its own Node.js build.
