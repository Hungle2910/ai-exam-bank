# AI Exam Bank frontend

This is the Next.js frontend in `Frontend/`. The ASP.NET Core backend, its .NET solution, tests, and documentation are in `Backend/`.

From this directory, install dependencies and start the UI:

```powershell
npm ci
npm run dev
```

Open `http://localhost:3000`. To use the live API, run the backend in another terminal from `Backend/Api/` at `http://localhost:5000`:

```powershell
dotnet run --no-launch-profile --urls http://localhost:5000
```

`next.config.ts` forwards `/auth`, `/api` and `/health` requests to that backend address. The backend needs its SQL Server connection configuration and applied migrations. The frontend may display mock data for endpoints that have not been implemented yet; that is not proof that the corresponding backend workflow works.

Run `npm run build` to check the frontend. From `Backend/`, run `dotnet build AiExamBank.slnx` and `dotnet test AiExamBank.slnx` to check the backend.
