# Source map: Backend and Frontend

The source code is physically separated into [Backend](../README.md) and [Frontend](../../Frontend/README.md). The .NET solution lives in `Backend/`; the Next.js app runs separately from `Frontend/`.

See the [exam workflow contract handoff](M1-01_EXAM_CONTRACTS.md) for the current module interfaces and the exact PRs that introduce the remaining exam routes.

| Area | Actual path | Contents |
|---|---|---|
| Backend | `Backend/Api/` | ASP.NET Core host, authentication, admin endpoints, HTTP composition. |
| Backend | `Backend/Modules/` | Owned business modules and public contracts. |
| Backend | `Backend/Persistence/` | SQL Server EF Core context, entities, migrations. |
| Backend | `Backend/Worker/` | Runnable background Jobs worker. |
| Frontend | `Frontend/` | Next.js app, pages, components, styles and frontend dependencies. |

Open `Backend/AiExamBank.slnx` in Visual Studio. Run .NET commands from `Backend/` and frontend commands from `Frontend/`, where its own `package.json` lives. Building the .NET solution does not build the frontend. See [the frontend README](../../Frontend/README.md) for its commands.
