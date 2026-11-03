# AI Exam Bank frontend

This is the Next.js frontend in `Frontend/`. The ASP.NET Core backend, its .NET solution, tests, and documentation are in `Backend/`.

To run the backend in Visual Studio and the frontend in VS Code:

1. Open `Backend/AiExamBank.slnx` in Visual Studio. Select the `AiExamBank.Api` launch profile and start it. This profile serves HTTP at `http://localhost:65526`.
2. Copy `.env.example` to `.env.local` in `Frontend/`. The local file points Next.js to that HTTP URL and is ignored by Git.
3. Open `Frontend/` in VS Code, then install dependencies and start the UI:

```powershell
npm ci
npm run dev
```

Open `http://localhost:3000`. If you instead run the backend at `http://localhost:5000`, set `AIEXAMBANK_BACKEND_URL=http://localhost:5000` in `.env.local` and restart Next.js. In a separate terminal opened in `Frontend/`, run:

```powershell
cd ..\Backend\Api
dotnet run --no-launch-profile --urls http://localhost:5000
```

`next.config.ts` forwards `/auth`, `/api` and `/health` requests to `AIEXAMBANK_BACKEND_URL`. The backend needs its SQL Server connection configuration and applied migrations. The frontend may display mock data for endpoints that have not been implemented yet; that is not proof that the corresponding backend workflow works.

Run `npm run test:unit` and `npm run build` to check the frontend. From `Backend/`, run `dotnet build AiExamBank.slnx` and `dotnet test AiExamBank.slnx` to check the backend.
