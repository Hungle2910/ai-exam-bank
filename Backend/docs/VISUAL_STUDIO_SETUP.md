# Open the .NET solution in Visual Studio

## Prerequisites

- [Visual Studio 2026 version 18.9 or newer](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs) with the **ASP.NET and web development** workload. Install the SDK version pinned by `global.json` (currently `10.0.400`); this SDK's supported Visual Studio minimum is 18.9.
- Git and, for repository checks, Python 3.12. No AWS credentials or database are required to build and test the solution; running the API requires local SQL Server configuration.

## Open and run

1. Clone the repository and check out `dev` after this scaffold PR is merged. In Visual Studio choose **File → Open → Project/Solution**, then select `Backend/AiExamBank.slnx`.
2. Allow NuGet restore to finish. Build the solution in **Release**. Backend projects are under `Backend/`; the Next.js application is under `Frontend/`. `Directory.Build.props` and `Directory.Packages.props` apply shared .NET compile settings and pinned test dependency versions.
3. Configure the local SQL Server connection, set **AiExamBank.Api** as the startup project and run it. Visit `/health/live` on the shown local URL. The endpoint checks process liveness only.
4. Open **Test Explorer** and run all tests. The current MSTest suites check revision IDs, blueprint slot validation and module dependency boundaries.

Equivalent terminal commands from the `Backend/` directory:

```bash
dotnet restore AiExamBank.slnx
dotnet build AiExamBank.slnx --configuration Release --no-restore --warnaserror
dotnet test AiExamBank.slnx --configuration Release --no-build --no-restore
dotnet run --project Api/AiExamBank.Api.csproj --urls http://127.0.0.1:5000
```

The Worker is still planned. See [Worker](../Worker/README.md), [Persistence](../Persistence/README.md), and the [frontend](../../Frontend/README.md). The frontend has its own Node.js build and is not built by the .NET solution. Do not use the liveness probe as a deployment-readiness signal.

## Project ownership and adding a feature

| Project area | Owner | First work to add |
|---|---|---|
| `Modules/Identity`, `Modules/Review` | M3 / Nguyen Hoang Phuc | Auth, permissions, decision/audit transaction |
| `Modules/Questions`, `Modules/Knowledge` | M4 / Nguyen Thien Phuc | Revision CRUD, taxonomy, citations/RAG boundaries |
| `Modules/Exams` | M1 / Le Doan Gia Hung | Blueprint selection and final immutable snapshot |
| `Modules/Import` | M2 / Nguyen Huynh Huu Phuoc | Preview/validation/commit with idempotency |
| `Modules/Jobs`, future `Worker` | M5 / Tran Gia Bao | Durable job/attempt state, lease/retry and recovery |

Branch from `dev`, implement a small end-to-end slice in the owning module, add its meaningful tests, update configuration/docs, then open a PR to `dev`. Use public module contracts for cross-module reads and review changes to shared contracts with affected owners. Promotion from tested `dev` to `main` remains a separate reviewed PR.
