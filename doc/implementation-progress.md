# Implementation Progress — Patient Management Application

Running record of what has been built against `doc/planning-pms-verification.md`.
Feature IDs and readiness tags are that plan's; this file never re-derives them.

**Status values:** `Not Started` · `In progress` · `Awaiting verification` · `Built & Verified`
(verification-pms only) · `Reviewed` (code-review-pms only) · `Needs rework` · `Blocked`.

**Repo layout note (applies to every feature):** at the user's explicit direction the
application lives under a top-level **`PMS/`** folder — `PMS/backend/` and `PMS/frontend/` —
rather than the plan §3 layout of repo-root `backend/` and `frontend/`. Only the root path
differs; project names, the four-project backend split, DTO/entity separation, the
folder-per-feature React structure and the test project layout all follow the plan.

| Feature ID | Feature | Status | Worktree / branch | Last updated | Notes |
|---|---|---|---|---|---|
| F-1 | Solution scaffolding, app shell, health check, error contract | Awaiting verification | `f-1-scaffolding` / `feature/f-1-scaffolding` | 2026-08-25 | Ready for verification-pms. 70 automated tests pass. E2E browser launch blocked by host (see log). |
| F-2 | Login, session policy, idle screen lock | Not Started | — | — | Buildable once F-1 is `Built & Verified`. Needs decision C-44/REC-11 — build against the plan's stated assumption. |
| F-3 | ClinicProfile + first-run setup gate | Not Started | — | — | Depends on F-1, F-2. Needs decision Q-4 (assumption stated). |
| F-4 | Doctor-configured settings | Not Started | — | — | Depends on F-3. Needs decision Q-9, Q-10 (assumptions stated). |
| F-5 | Patient registration & profile | Not Started | — | — | Depends on F-1, F-2, F-4. Needs decision Q-7, Q-16, Q-9. |
| F-6 | Duplicate detection + `merged_into` pointer | Not Started | — | — | Depends on F-5. Needs decision Q-13. |
| F-7 | Patient search, recent patients, picker | Not Started | — | — | Depends on F-5. Needs decision C-22 (no `Q-` exists). |
| F-8 | Patient edit + deactivate (no hard delete) | Not Started | — | — | Depends on F-5, F-17. Needs decision Q-6. |
| F-9 | Appointments | Not Started | — | — | Depends on F-5, F-7. Needs decision Q-5, Q-14. |
| F-10 | Visit lifecycle | Not Started | — | — | Depends on F-9. Needs decision Q-3. |
| F-11 | Vitals capture | Not Started | — | — | Depends on F-10, F-4. Needs decision Q-2, Q-10. |
| F-12 | Complaints & diagnosis capture | Not Started | — | — | Depends on F-10. Needs decision Q-8. |
| F-13 | Medications | Not Started | — | — | Depends on F-10. Needs decision C-31/E-22 (no `Q-` exists). |
| F-14 | Prescription generation, print, reprint | Not Started | — | — | Depends on F-3, F-11, F-12, F-13. Needs decision Q-4, Q-8. |
| F-15 | Visit amendments (append-only) | Not Started | — | — | Depends on F-14. Needs decision Q-3. |
| F-16 | Patient history + date filter | Not Started | — | — | Depends on F-10, F-14, F-15. Ready once upstream lands. |
| F-17 | Audit trail (six event types) | Not Started | — | — | Depends on F-1. Needs decision REC-9/C-48 (no `Q-` exists). |
| F-18 | Export CSV / PDF | Not Started | — | — | Depends on F-14, F-16, F-17. Needs decision Q-11. |
| F-19 | Keyboard-first input + performance instrumentation | Not Started | — | — | Depends on F-10..F-13. Needs decision Q-15. |
| F-20 | Backup, restore rehearsal, encryption at rest | **Blocked** | — | — | Blocked on Q-1 + Q-12 (deployment model, RPO). Off the build path but a hard go-live gate. |
| F-21 | Credential recovery, lockout policy | **Blocked** | — | — | Blocked on C-44; brainstorm §12 carries no question for it. Off the build path but a hard go-live gate. |

---

## Log

### 2026-08-25 — F-1 solution scaffolding, app shell, health check, error contract

**Status: `Awaiting verification`.** Handed to verification-pms. Branch left for review, not merged.

- Got an isolated worktree from `worktree-pms` at
  `C:\Users\NileshMalviya\source\repos\f-1-scaffolding`, branch `feature/f-1-scaffolding`,
  cut from `main` at `67cc2a6`. Confirmed distinct from the main working tree before writing
  anything. Also confirmed the worktree's `doc/planning-pms-verification.md` is byte-identical
  (modulo CRLF) to the main tree's, so this was built against the current committed plan.

**Deviation from the plan, flagged per the "flag deviations explicitly" rule:**

- **Folder root.** Plan §3 places the solution at repo-root `backend/` and `frontend/`. At the
  user's explicit direction everything is under **`PMS/`** instead: `PMS/backend/` and
  `PMS/frontend/`. Every other convention in §2/§3 is unchanged — the four-project split
  (`PMS.Domain`, `PMS.Application`, `PMS.Infrastructure`, `PMS.Api`), Controller → Service →
  abstraction layering, DTOs separate from EF entities, folder-per-feature React structure,
  and the three test projects under `backend/tests/`. `.gitignore` was rewritten for the new
  root. Acceptance criteria 1, 2 and 5 name `backend/...` and `frontend/...` paths; they were
  evaluated against the corresponding `PMS/backend/...` and `PMS/frontend/...` paths.

**Built (backend):**

- `PMS/backend/PMS.sln` — classic `.sln` format (the .NET 10 `dotnet new sln` default is
  `.slnx`; the plan's AC names `PMS.sln`, so the classic format was chosen deliberately).
  Four source projects, two .NET test projects, all `net10.0` (the only SDK installed).
- `PMS.Domain/Entities/AppUser.cs` per plan §4.
- `PMS.Application`: `Abstractions/IClock.cs`, `Abstractions/IDatabaseProbe.cs`,
  `Abstractions/IHealthService.cs`; `Services/SystemClock.cs`, `Services/HealthService.cs`;
  `Dtos/Health/HealthResponse.cs`; `Exceptions/` (`DomainRuleException`, `NotFoundException`,
  `ValidationFailedException`); `DependencyInjection.cs`.
- `PMS.Infrastructure`: `Persistence/PmsDbContext.cs` (DbSet `AppUsers` only, per F-1 §2),
  `Persistence/Configurations/AppUserConfiguration.cs`,
  `Persistence/EfCoreDatabaseProbe.cs`, `DependencyInjection.cs`,
  `Migrations/20260825170916_InitialCreate.cs`.
- `PMS.Api`: `Program.cs` (composition root, HSTS, static files, SPA fallback that never
  captures `/api/*`), `Controllers/HealthController.cs`,
  `Middleware/ProblemDetailsMiddleware.cs`, `Middleware/RequestTimingMiddleware.cs`.
  Controllers depend on `IHealthService`, never on `PmsDbContext`.

**Built (frontend):** `PMS/frontend` — Vite 6 + React 18 + TypeScript, TanStack Query v5,
React Router v6. `src/main.tsx`, `App.tsx`, `routes.tsx`; `shared/api/httpClient.ts`,
`shared/api/problemDetails.ts` (typed `ProblemDetailsError`), `shared/api/queryClient.ts`
(`retry: 1`, `refetchOnWindowFocus: false`); `shared/components/AppLayout.tsx`,
`EmptyState.tsx`, `ErrorBoundary.tsx`, `PlaceholderPage.tsx`;
`shared/types/problemDetails.ts`. All nine plan-named routes registered as placeholders.

**Data integrity check (F-1 §5).** No user data path yet. The contract this feature owes
later features is that no failure is ever swallowed: the server maps every throw to RFC-7807,
an unmatched `/api/*` returns `problem+json` rather than the SPA shell, and `httpClient`
converts even a rejected `fetch` into a typed `ProblemDetailsError` with an explicit "your
work has not been saved" message. That is the E-47 guard, and it is tested at both ends.

**Test results — run in the worktree, real output:**

- `dotnet build PMS/backend/PMS.sln` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet test PMS/backend/PMS.sln` →
  - `PMS.Application.Tests` — **Passed! Failed: 0, Passed: 13**
  - `PMS.Api.IntegrationTests` — **Passed! Failed: 0, Passed: 18**
- `npm test` in `PMS/frontend` → **4 test files, 39 passed, 0 failed**.
- `npm run build` in `PMS/frontend` → succeeded, emitting to
  `PMS/backend/src/PMS.Api/wwwroot`.
- **Total: 70 automated tests passing.**
- Live smoke against `dotnet run`: `GET /api/health` → 200 `{"status":"Healthy",...}`;
  `GET /api/health/db` → 200 with LocalDB configured and **503**
  `{"detail":"Database connection is not configured."}` with it removed; `GET /` → 200
  `text/html` serving the SPA; `GET /patients/123` → 200 `text/html`; `GET /api/nope` → 404
  `application/problem+json`.

**Acceptance criteria — walked line by line:**

1. *Build succeeds with four projects and three test projects* — **met, with one caveat.**
   Four source projects and two .NET test projects build clean. The third test project,
   `PMS.E2E`, is Playwright/TypeScript by the plan's own §3 annotation and therefore cannot
   be an MSBuild project inside `PMS.sln`; it exists at `backend/tests/PMS.E2E/` with its own
   `package.json`, `playwright.config.ts` and `tsconfig.json`, and typechecks clean. Raised
   as a plan wording issue, not resolved unilaterally.
2. *`InitialCreate` migration; `database update` creates `PMSDb` visible in SSMS* — **met.**
   `dotnet ef migrations add InitialCreate -p src/PMS.Infrastructure -s src/PMS.Api -o Migrations`
   produced the migration; `dotnet ef database update` applied it. `sqlcmd -S "(localdb)\MSSQLLocalDB" -d PMSDb`
   lists `__EFMigrationsHistory` and `AppUsers`.
3. *`/api/health/db` 200 live / 503 with the connection string removed* — **met**, verified
   both live (curl, above) and by integration tests.
4. *No connection string, password or key in any committed file; user-secrets supplies it
   locally* — **met.** `appsettings.json` and `appsettings.Development.json` contain no
   connection-string value (only a comment naming how to supply one).
   `dotnet user-secrets list` returns the local value. A grep of every staged file finds
   connection-string text in exactly two non-production places: `PMS/README.md` documenting
   the user-secrets command, and integration-test fixtures using LocalDB with integrated
   security. Neither carries a credential.
5. *`npm run build` emits to the API's `wwwroot`, and browsing the API root serves the SPA* —
   **met** (adjusted for the `PMS/` root), verified live.

**Assumptions and judgement calls recorded inline in the code:**

- `HealthResponse` fields (`status`, `component`, `checkedUtc`, `detail`) — the plan names the
  DTO but not its shape. Marked `// ASSUMPTION:` in
  `PMS.Application/Dtos/Health/HealthResponse.cs`.
- `AppUser` is created by `InitialCreate` rather than by F-2's `AddAppUser`. F-1 §2 says the
  context has "no entity sets beyond `AppUser`", so the table has to exist for that DbSet to
  compile; F-2's `AddAppUser` will therefore be an alter rather than a create. Flagged for
  the plan owner.
- A missing connection string is a reportable 503 state, not a startup exception —
  `/api/health/db` exists precisely to surface it, and a crash would leave nothing able to.
- `PMS.Application/Exceptions/` is a folder the plan's §3 tree does not list (it lists
  `Abstractions/`, `Services/`, `Dtos/`, `Validation/`). The error contract this feature owns
  needs somewhere to declare its exception types.

**Known environment limitation — E2E not run:**

`PMS.E2E/specs/app-shell.spec.ts` is written at the plan's named target and typechecks. Of
its 7 specs: 2 (the pure API-request ones) pass against a live instance; 1 is deliberately
`test.fixme` because it asserts F-2's unauthenticated redirect, which does not exist yet; the
remaining 4 could not run — Playwright fails with `browserType.launch: spawn EPERM` on this
host, both inside and outside the tool sandbox. WebKit's browser dependencies
(`javascriptcore.dll`, `webcore.dll`, `webkit2.dll`) are also absent. **The E2E suite is
therefore written but unproven, and is not claimed as passing.** This is the same class of
environment gotcha already recorded in `CLAUDE.md`.

**Flagged for review — not decided here:**

- **React Router v6 carries two moderate advisories** (GHSA-wrjc-x8rr-h8h6 open redirect,
  GHSA-337j-9hxr-rhxg SSR hydration constructor injection). Every v6 release is affected;
  the fix is v7.18+. Plan §2 explicitly specifies **React Router v6**, so v6 was built as
  planned rather than silently upgraded. Neither advisory is reachable in this app's Phase-1
  shape (no SSR; no redirect target taken from untrusted input), but the plan should either
  be amended to v7.18+ before the routing surface grows or the risk accepted on the record.
- Acceptance criterion 1's "three test projects" wording versus `PMS.E2E` being a Node
  project (above).

Committed on `feature/f-1-scaffolding`. Not merged, not pushed.
