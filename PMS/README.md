# PMS — Patient Management Application

Application code for the single-physician clinic system described in `BRD/Doc_BRD.md` and
planned in `doc/planning-pms-verification.md`.

```
PMS/
├─ backend/            ASP.NET Core Web API solution (PMS.sln)
│  ├─ src/
│  │  ├─ PMS.Domain/           entities and enums, zero framework dependencies
│  │  ├─ PMS.Application/      services, DTOs, abstractions, validators
│  │  ├─ PMS.Infrastructure/   EF Core PmsDbContext, configurations, migrations
│  │  └─ PMS.Api/              controllers, middleware, composition root, serves the SPA
│  └─ tests/
│     ├─ PMS.Application.Tests/     xUnit + NSubstitute + FluentAssertions
│     ├─ PMS.Api.IntegrationTests/  xUnit + WebApplicationFactory + SQL Server LocalDB
│     └─ PMS.E2E/                   Playwright specs (TypeScript, not an MSBuild project)
└─ frontend/           React 18 + TypeScript, built with Vite
```

> **Folder-root note.** The plan (`doc/planning-pms-verification.md` §3) places these at the
> repository root as `backend/` and `frontend/`. They live under `PMS/` here at the user's
> explicit direction. Project names, layering, DTO/entity separation, the folder-per-feature
> React structure and the test project layout are unchanged from the plan.

## Prerequisites

- .NET 10 SDK
- Node.js 20.19+ or 22.12+
- SQL Server (LocalDB is sufficient for development; SSMS to inspect it)
- `dotnet-ef` (`dotnet tool install --global dotnet-ef`)

## First-time setup

The connection string is **never committed**. Supply it locally with user-secrets:

```bash
cd PMS/backend/src/PMS.Api
dotnet user-secrets set "ConnectionStrings:Pms" "Server=(localdb)\MSSQLLocalDB;Database=PMSDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

In a deployed environment use the `ConnectionStrings__Pms` environment variable instead.
Until one of the two is present, `GET /api/health/db` answers `503` with
`"Database connection is not configured."` — by design, so the state is visible rather than
a startup crash.

Then create the schema:

```bash
cd PMS/backend
dotnet ef database update -p src/PMS.Infrastructure -s src/PMS.Api
```

## Everyday commands

| Task | Command |
|---|---|
| Build the backend | `dotnet build PMS/backend/PMS.sln` |
| Backend tests (unit + integration) | `dotnet test PMS/backend/PMS.sln` |
| Frontend install | `cd PMS/frontend && npm install` |
| Frontend unit tests | `cd PMS/frontend && npm test` |
| Frontend production build | `cd PMS/frontend && npm run build` |
| Run the API (serves the built SPA) | `cd PMS/backend/src/PMS.Api && dotnet run --launch-profile https` |
| Frontend dev server (proxies `/api`) | `cd PMS/frontend && npm run dev` |

The `https` launch profile must be used explicitly: the frontend dev proxy (`vite.config.ts`) targets
`https://localhost:7191`, but plain `dotnet run` selects the first profile in `launchSettings.json`
(`http`, port 5054 only). Running without `--launch-profile https` leaves nothing listening on 7191,
so the Vite proxy fails with a bare `500 Internal Server Error` on every `/api/*` call, including login.
| E2E | `cd PMS/backend/tests/PMS.E2E && npm install && npm run install-browsers && npm test` |

`npm run build` emits into `PMS/backend/src/PMS.Api/wwwroot`, so the API and the SPA are
same-origin. That is a requirement of the cookie-based auth decision (plan §2), not a
packaging convenience: a `SameSite=Strict` cookie cannot be used cross-origin.

## Adding a migration

Every schema change is a **named** migration, named by the feature that introduces it:

```bash
cd PMS/backend
dotnet ef migrations add <Name> -p src/PMS.Infrastructure -s src/PMS.Api -o Migrations
```
