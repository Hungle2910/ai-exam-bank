# AI Exam Bank backend

This folder contains the .NET 10 backend:

| Folder | Purpose |
|---|---|
| `Api/` | HTTP host, authentication, and endpoints. |
| `Modules/` | Business modules and their contracts; see [module owners](Modules/README.md). |
| `Persistence/` | EF Core SQL Server context, entities, and migrations. |
| `Worker/` | Runnable Jobs worker; use the same `Jobs__StorePath` as the API when running it separately. |

From this `Backend/` directory, use `dotnet restore AiExamBank.slnx`, `dotnet build AiExamBank.slnx`, and `dotnet test AiExamBank.slnx`. For the API, configure the SQL Server connection locally, then run `dotnet run --project Api/AiExamBank.Api.csproj`. See the [M3 feature notes](docs/M3-02_feature_notes.md) for migration and initial admin setup.

The Next.js application is in [Frontend](../Frontend/README.md) and has its own Node.js build.

After pulling the latest `dev` code, the API also hosts the M5 Jobs endpoints and the M2 infrastructure health preview. The Jobs endpoints use their own bearer-token authentication scheme; the M3 identity/admin endpoints continue to use the Identity cookie. See [M5 Jobs](docs/M5_02/M5_02_JOB_ENGINE_DESIGN.md) and [M2 local preparation](docs/M2-02_LOCAL_PREPARATION.md) for the separate setup steps.

The Jobs module currently stores jobs in a file. Its `Persistence/Migrations/001_create_jobs_and_job_attempts.sql` is a PostgreSQL draft from M5; do not run it against the team's SQL Server database. A SQL Server persistence migration and Identity-based resource authorization for Jobs need separate integration.
