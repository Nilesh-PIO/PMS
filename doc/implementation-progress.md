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
| F-1 | Solution scaffolding, app shell, health check, error contract | **Built & Verified** | `f-1-scaffolding` / `feature/f-1-scaffolding` | 2026-09-01 | Verified by verification-pms 2026-09-01 — all 5 ACs met on independently re-run evidence; 70 tests re-run, 0 failed, 0 skipped. **Two carried items, neither an F-1 code defect:** Playwright browser harness unprovable on this host (must be closed before F-14); branch was merged to `main` before this gate ran (process violation). Next: `code-review-pms`. |
| F-2 | Login, session policy, idle screen lock | Awaiting verification | `f-2-auth-session` / `feature/f-2-auth-session` | 2026-09-01 | Ready for verification-pms. 211 automated tests pass (108 .NET + 103 Vitest), 0 skipped — re-run from a clean build in a second pass, plus a live smoke against a throwaway DB. Built against the plan's C-44/REC-11 assumption. **Carries a deliberate, user-directed deviation: the seed credential `doctor` / `SeedDoctor#2026!` is committed in plain text in `appsettings.json` under `SeedDoctorUser`** — see the log entries; this is an instruction, not an oversight. Password rotation after first sign-in is recommended, not enforced (F-21 is `Blocked`). E2E browser launch still blocked by the host. |
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

---

### 2026-09-01 — F-1 independent verification (verification-pms)

**Verdict: PASS. Status → `Built & Verified`**, with two items carried forward and named
below. Nothing here was taken from the builder's report; every result below is output I
produced myself in this pass, inside the worktree.

**Worktree confirmed real and current.** `git worktree list` shows
`C:\Users\NileshMalviya\source\repos\f-1-scaffolding` on `feature/f-1-scaffolding` at
`0af1b06`, working tree clean before and after my runs. The worktree's
`doc/planning-pms-verification.md` is identical to `main`'s, so this was verified against
the current committed plan.

**What I re-ran, and the actual output:**

| Command (run by verification-pms) | Result |
|---|---|
| `dotnet build PMS.sln` after deleting all `bin/`+`obj/` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `dotnet test PMS.sln` | `PMS.Application.Tests` **Failed: 0, Passed: 13, Skipped: 0**; `PMS.Api.IntegrationTests` **Failed: 0, Passed: 18, Skipped: 0** |
| `npm test` (`vitest run`) in `PMS/frontend`, run twice | **4 files, 39 passed, 0 failed, 0 skipped** both times — no flake observed |
| `npm run build` (`tsc -b && vite build`) | Succeeded; emitted `index.html` + `assets/` into `PMS/backend/src/PMS.Api/wwwroot` after I deleted that folder first |
| `npx playwright test --project=chromium` against a live instance | **2 passed, 4 failed (`browserType.launch: spawn EPERM`), 1 skipped (`test.fixme`)** |

**Total re-run and passing: 70 automated tests (31 .NET + 39 Vitest), 0 skipped, 0 failed.**

**Green-checkmark checks.** Test *counts* read, not just exit codes — no empty test project,
no `Skipped` in either .NET suite, no `[Ignore]`/`.skip`. No `NoWarn`, no
`TreatWarningsAsErrors` toggle, no `Directory.Build.props`, `.editorconfig` or ruleset
anywhere in the repo, so the 0-warning build is genuine and not suppressed. Test names in
both .NET projects are substantive (e.g.
`HealthDb_never_discloses_the_connection_string_or_server_name`,
`Unmatched_api_route_body_is_never_empty`), not placeholders. Vitest run twice with
identical results.

**Acceptance criteria — checked against code and live output, not the builder's report:**

1. *Build succeeds, four projects + three test projects* — **met in substance.**
   `dotnet sln list` shows `PMS.Api`, `PMS.Application`, `PMS.Domain`, `PMS.Infrastructure`
   plus `PMS.Api.IntegrationTests` and `PMS.Application.Tests`, all building clean.
   The third suite, `PMS.E2E`, is Playwright/TypeScript **because plan §3 line 92 itself
   annotates it that way**, so it cannot be an MSBuild project. AC-1's "three test projects"
   wording contradicts the plan's own §3. **Recorded as a plan gap, not a build failure** —
   I am not resolving it unilaterally; `planning-pms` should reword AC-1.
2. *`InitialCreate` migration; `database update` creates `PMSDb`* — **met.** I queried the
   database directly: `sqlcmd -S "(localdb)\MSSQLLocalDB" -d PMSDb` returns tables
   `__EFMigrationsHistory` and `AppUsers`, with `20260825170916_InitialCreate` /
   ProductVersion `10.0.11` in the history table. `dotnet ef migrations
   has-pending-model-changes` → **"No changes have been made to the model since the last
   migration"**, so the committed migration matches the model.
3. *`/api/health/db` 200 live, 503 with the connection string removed* — **met, verified
   live by me both ways.** With user-secrets supplying the string:
   `200 {"status":"Healthy","component":"database",...}`. With `ConnectionStrings__Pms=""`
   overriding it on a second instance:
   `503 {"status":"Unhealthy","component":"database","detail":"Database connection is not
   configured."}`, while `/api/health` still returned 200 — liveness and readiness are
   correctly separated. Also covered by
   `HealthEndpointTests.HealthDb_returns_503_with_the_connection_string_removed`.
4. *No connection string, password or key in any committed file* — **met.** `git grep` for
   `Password=|Server=|Data Source=|User Id=|api key|secret|PRIVATE KEY` over **tracked**
   files returns no credential. `appsettings.json` carries only a `_comment` naming how to
   supply the value; `appsettings.Development.json` has no connection section at all;
   `UserSecretsId` is present in `PMS.Api.csproj` and `dotnet user-secrets list` returns the
   local value. The only `Server=` literals in code are LocalDB with
   `Trusted_Connection=True` (integrated security, no password) in test fixtures, plus
   `PMS/README.md` documenting the command. **`git ls-files` on
   `PMS/backend/src/PMS.Api/wwwroot` returns nothing** — the previously predicted
   `.gitignore` gap around the built bundle is closed, so the secret-scan surface is not
   enlarged by committed build output.
5. *`npm run build` emits to the API's `wwwroot`; browsing the API root serves the SPA* —
   **met, verified live.** `GET /` → `200 text/html` serving the SPA shell;
   `GET /patients/123` → `200 text/html` (server-side SPA fallback);
   `GET /api/nope` → `404 application/problem+json`, never the SPA shell.

**Data-integrity and architecture spot-check — mechanism present, not just mentioned:**

- **E-47 guard is real at both ends.** Server: `ProblemDetailsMiddleware` maps every throw
  to RFC-7807, clears the response, never returns an empty body, and deliberately withholds
  exception text/SQL from the 500 body while emitting a correlation id. `Program.cs`
  registers a `/api/{**slug}` fallback ahead of `MapFallbackToFile` so an unmatched API path
  cannot return `index.html`. Client: `httpClient.request` converts even a rejected `fetch`
  into a typed `ProblemDetailsError` carrying "nothing has been saved", rethrows `AbortError`
  unchanged, and throws rather than returning a half-parsed value on a non-JSON 2xx — it
  **never resolves on failure**, so a caller cannot mistake a failure for a success.
  Directly asserted by the `request - transport failure (E-47)` Vitest block and by
  `ErrorContractTests.Unmatched_api_route_body_is_never_empty`.
- **Layering as specified.** `HealthController` depends on `IHealthService` only and never
  on `PmsDbContext`; `HealthService` (Application) depends on the `IDatabaseProbe`
  abstraction, implemented by `EfCoreDatabaseProbe` in Infrastructure. `PMS.Domain.csproj`
  has zero package references. `HealthResponse` is a DTO in `PMS.Application/Dtos/`, distinct
  from the `AppUser` EF entity — no entity crosses the wire.
- **Frontend structure per plan.** Shared fetch wrapper, query client, layout, empty state,
  error boundary and `problemDetails` types all under `frontend/src/shared/`; `queryClient`
  defaults are `retry: 1` / `refetchOnWindowFocus: false` as §F-1 requires, asserted by test.
  All **nine** plan-named routes are registered (`/login`, `/setup`, `/`, `/patients`,
  `/patients/:id`, `/visits/:id`, `/settings/clinic`, `/export`, `/audit`) plus a catch-all
  that shows a not-found page rather than a blank screen — asserted by `App.test.tsx`.
- **Folder root deviation accepted as directed** — `PMS/backend/` and `PMS/frontend/` per
  explicit user instruction; every other §2/§3 convention verified above at the new root.

**Carried item 1 — the Playwright harness is unproven, and that is recorded, not waved
through.** I reproduced the builder's claim exactly rather than accepting it: of 7 chromium
specs, **2 passed** (the two that use Playwright's API `request` context and need no
browser), **1 skipped** (`test.fixme`, the `/login` redirect, correctly deferred because
`RequireAuth` is F-2's), and **4 failed with `browserType.launch: spawn EPERM`** on both the
first run and the automatic CI retry. This is a host process-spawn denial, not a defect in
F-1's code, and no change `implementation-pms` could make would fix it — which is why this is
not routed back as rework.

I did not let "written but unrunnable" count as passing. I checked whether each unrunnable
spec's behaviour is proven elsewhere by a suite that does run, and it is:
`the SPA loads and mounts` → `SpaHostingTests.Root_serves_the_spa_when_the_bundle_is_built`
plus my live `GET /`; `renders its main navigation` → `App.test.tsx` layout-chrome test;
`deep client route survives a hard refresh` →
`SpaHostingTests.A_deep_client_route_serves_the_spa_shell_not_an_api_error` plus my live
`GET /patients/123`; `no auth token in browser storage` → the httpClient storage test, and
F-1 contains no auth code at all. **The only genuinely unproven axis is real-browser
rendering, and F-1 contains no browser-divergent surface.**

**This is a carried risk with a deadline, not a closed item.** The harness must be proven
before **F-14** (printed prescription across Chrome/Edge/WebKit is a stated BRD compatibility
requirement, C-47 — the one place browsers actually diverge), and the `test.fixme` must be
removed by **F-2**. If the host cannot run browsers by then, that is an environment decision
for the product owner, not something to discover at F-14.

**Carried item 2 — the gate was bypassed: F-1 was already merged to `main` before this
verification ran.** `main` is at `7a28cd2` "Merge pull request #1 from
Nilesh-PIO/feature/f-1-scaffolding"; `git merge-base --is-ancestor 0af1b06 main` confirms the
F-1 commit is on `main`, and `origin/feature/f-1-scaffolding` exists. This **contradicts the
2026-08-25 entry above, which states "Not merged, not pushed"** — a reminder that a builder's
report is a claim to check. Per the pipeline, only `finishing-pms` merges, and only after
`Reviewed`; F-1 was neither `Built & Verified` nor `Reviewed` at merge time. The code itself
passes verification, so nothing needs reverting on quality grounds, but the sequencing
violation is recorded here rather than absorbed silently, and `code-review-pms` should note
that it is reviewing code already on `main`.

**Confirmed for the reviewer, not decided here:**

- **React Router v6 advisories are real** — I reproduced them independently:
  `npm ls` shows `react-router-dom@6.30.6` / `react-router@6.30.6`, and `npm audit` reports
  **2 moderate** severity issues (GHSA-wrjc-x8rr-h8h6 open redirect via backslash in `<Link>`
  and `useNavigate`; GHSA-337j-9hxr-rhxg constructor injection via `deserializeErrors()` in
  SSR hydration), affecting `6.0.0 - 7.17.0`, fix in `7.18.3` (breaking). Plan §2 explicitly
  specifies React Router v6, so building v6 was **correct plan adherence, not a defect** —
  the builder was right to flag rather than silently upgrade. **This is a plan gap requiring
  an owner decision:** amend §2 to v7.18+, or accept the risk on the record. Neither advisory
  is reachable in F-1's shape (no SSR; no redirect target from untrusted input), but the
  routing surface grows from F-2 onward, so it should be settled before then.
- `AppUser` is created by `InitialCreate` rather than F-2's `AddAppUser`, so `AddAppUser`
  will be an alter, not a create. This follows from plan F-1 §2's own wording; flagged for
  the plan owner.
- `PMS.Application/Exceptions/` is not in the plan's §3 tree. Reasonable for the error
  contract F-1 owns; noted for `code-review-pms`.

**Gate status.** F-1 is `Built & Verified` — it works, on evidence I produced. It is **not**
finished: `code-review-pms` reviews it for quality, consistency and security before
`Reviewed`. Per the dependency map, **F-2 is now buildable**; F-17 and F-20's dependency on
F-1 is likewise satisfied. No application code, test code or migration was modified in this
pass — `git status` in the worktree is clean at `0af1b06`.

---

### 2026-09-01 — F-2 login, session policy, idle screen lock

**Status: `Awaiting verification`.** Handed to verification-pms. Branch left for review, not
merged, not pushed.

- Got an isolated worktree from `worktree-pms` at
  `C:\Users\NileshMalviya\source\repos\f-2-auth-session`, branch `feature/f-2-auth-session`,
  cut from `main` at `7a28cd2` (the F-1 merge commit). Confirmed distinct from the main
  working tree before writing anything. Also confirmed `doc/planning-pms-verification.md` and
  `doc/brainstorm-pms-verification.md` are byte-identical to `HEAD` in the main tree
  (`git diff --stat` empty), so this was built against the current committed plan.
- Built against the plan's stated assumption for **C-44 / REC-11**, since brainstorm §12
  carries no `Q-` for it: **5-minute idle lock, 12-hour absolute session expiry, sliding
  renewal, 12-character minimum password, no forced rotation.** Those five numbers live in
  exactly two files — `PMS.Application/Services/SessionPolicy.cs` and
  `frontend/src/shared/config/sessionPolicy.ts` — and are pinned by a test, so a different
  answer from the physician is a two-file change, not a hunt.
- **Lockout is deliberately not implemented.** `AppUser.FailedAttempts` / `LockoutEndUtc`
  exist (plan §4) but nothing writes them, because lockout and credential recovery are **F-21,
  which is `Blocked` on C-44**. With one user and no recovery path, shipping a lockout could
  lock the clinic out of its own patient records permanently. `AuthServiceTests` pins the
  absence so it cannot be "fixed" by accident.

---

#### DEVIATION 1 — a real login credential is committed in plain text (user-directed)

**This is an explicit user instruction, given after the user was warned that it departs from
both the plan and normal secret handling. It is not an oversight, and `verification-pms` /
`code-review-pms` should not treat it as one.**

- **What the plan says.** F-2 §2: the initial credential is read "from configuration
  (user-secrets / environment variable)". §2 *Environments*: "No connection string, password
  pepper or signing key is ever committed."
- **What was built instead.** The real seed user name and password are written into the
  tracked file **`PMS/backend/src/PMS.Api/appsettings.json`**, under a `SeedDoctorUser`
  section:

  ```
  SeedDoctorUser:UserName = doctor
  SeedDoctorUser:Password = SeedDoctor#2026!
  ```

  These are the credentials to actually sign in with, recorded here in plain text because the
  user needs them. The section is named `SeedDoctorUser` — deliberately distinct from
  `ConnectionStrings` in the same file — so the one credential that *is* committed here is
  never mistaken for the connection string, which remains user-secrets-only and uncommitted.
- **Recommendation, recorded not built: the physician should change this password after the
  first sign-in.** F-2 does not build a forced-rotation flow; that is out of scope for the
  feature as planned, and password change / recovery is F-21, which is `Blocked` on C-44.
  Until F-21 is unblocked, rotating means updating the `AppUsers` row directly.
- **Consequence, stated plainly: once this branch is committed, a working login credential for
  this application is in git history permanently.** Rotating the password later does not
  remove it from history, and anyone with read access to the repository can sign in. This is
  the reason the plan wanted user-secrets.
- **Where it is flagged in the code**, in the same style F-1 used for its `PMS/` folder-root
  deviation: a `DEVIATION` comment block in `appsettings.json` itself, and a matching
  `<remarks>` block at the read site,
  `PMS/backend/src/PMS.Api/Startup/InitialUserSeedExtensions.cs`.
- **The route back is a config change, not a code change.** Configuration precedence is
  untouched, so `SeedDoctorUser__UserName` / `SeedDoctorUser__Password` as environment
  variables (or user-secrets) still override the committed values.
- **Everything else about the seeder is as specified.** It hashes with PBKDF2-HMAC-SHA256
  before insert, never persists or logs the plaintext, refuses a password under 12 characters,
  and **refuses to run twice** — proven live below.

#### DEVIATION 2 — `AddAppUser` is an empty migration

Confirmed rather than assumed, as instructed: `dotnet ef migrations
has-pending-model-changes` reports **"No changes have been made to the model since the last
migration"**. F-1's `InitialCreate` already created `AppUsers` with every column in plan §4
plus the unique index on `UserName` — F-1's own tracker entry flagged that `AddAppUser` would
therefore be an alter, not a create. The migration the plan names was still generated and kept,
with an explanatory `<remarks>` block, so the schema history contains it and records that F-2
examined the table and found it already correct. `dotnet ef database update` applied it;
`__EFMigrationsHistory` now lists both `20260825170916_InitialCreate` and
`20260901093334_AddAppUser`.

---

**Built (backend):**

- `PMS.Application`: `Abstractions/IAuthService.cs` (+ `AuthenticationResult`),
  `Abstractions/IPasswordHasher.cs`, `Abstractions/IAppUserRepository.cs`,
  `Abstractions/IInitialUserSeeder.cs` (+ `InitialUserSeedOutcome`/`InitialUserSeedResult`);
  `Dtos/Auth/LoginRequest.cs`, `Dtos/Auth/SessionResponse.cs`;
  `Services/AuthService.cs`, `Services/InitialUserSeeder.cs`, `Services/SessionPolicy.cs`;
  both new services registered in `DependencyInjection.cs`.
- `PMS.Infrastructure`: `Security/Pbkdf2PasswordHasher.cs` (PBKDF2-HMAC-SHA256, 210,000
  iterations, 128-bit random salt, 256-bit subkey, cost stored inside the hash,
  `CryptographicOperations.FixedTimeEquals`), `Persistence/Repositories/AppUserRepository.cs`,
  `Migrations/20260901093334_AddAppUser.cs`. The hasher is registered unconditionally; the
  repository only alongside the DbContext, matching F-1's no-connection-string branch.
- `PMS.Api`: `Controllers/AuthController.cs` (the four routes exactly as the plan's table
  specifies), `Auth/AuthenticationSetup.cs`, `Auth/PmsAuthDefaults.cs`,
  `Startup/InitialUserSeedExtensions.cs`; `Program.cs` gains `AddPmsAuthentication()`,
  `UseAuthentication()`, and one `await app.SeedInitialUserAsync()` after `Build()`.
  `AuthController` depends on `IAuthService`, never on `PmsDbContext`.

**Three backend decisions worth naming:**

1. **Default-deny authorization.** `AuthorizationOptions.FallbackPolicy` requires an
   authenticated user, so a controller added by F-5 or F-10 is protected by omission rather
   than exposed by it. The anonymous allow-list is exactly `api/health`, `api/health/db`,
   `api/auth/login`, `api/auth/reauth` and the `api/{**slug}` catch-all, and
   `AuthorizationPolicyTests` fails if that set ever changes.
2. **Sliding renewal *within* a hard 12-hour cap.** The cookie handler's `SlidingExpiration`
   renews indefinitely on its own, so the absolute expiry is stamped into a claim at sign-in
   and enforced in `OnValidatePrincipal`. `SessionExpiryTests` drives a fake `IClock` through
   twelve hours of continuous activity and proves the session still dies.
3. **401s are RFC-7807, never a 302.** `OnRedirectToLogin`/`OnRedirectToAccessDenied` write a
   `problem+json` body instead of redirecting, or `httpClient.ts` would JSON-parse an HTML
   login page and report a nonsense error (E-47, F-1's error contract).

**Built (frontend):**

- `features/auth/`: `LoginPage.tsx` (route `/login`), `authApi.ts` (`login`/`logout`/
  `getSession`/`reauth`), `useSession.ts` (`useSession` at `staleTime: 60_000`, plus
  `useLogin`/`useReauth`/`useLogout`), `types/session.ts`.
- `shared/`: `components/ScreenLock.tsx`, `components/RequireAuth.tsx`,
  `hooks/useIdleTimer.ts`, `config/sessionPolicy.ts`, and the E-65 form convention in
  `components/forms/TextField.tsx` + `components/forms/PatientDataForm.tsx`.
- `routes.tsx` now mounts `LoginPage` at `/login` and wraps the layout branch in
  `RequireAuth`; `AppLayout.tsx` gains the sign-out control and wraps itself in `ScreenLock`.

**Data integrity check (F-2 §5) — the mechanism, not a mention.** The lock is a *sibling* of
the application tree, never a conditional render of it. `children` is never unmounted,
re-keyed or replaced, so component state and uncontrolled input values survive lock and
unlock; the overlay re-authenticates through `POST /api/auth/reauth` with no navigation, which
is why the consultation beneath is still there afterwards (E-41). `useIdleTimer` only ever
*reports* idleness — it never signs anyone out — which is what keeps a 5-minute absence from
costing a draft (E-62). The `ScreenLock` test asserts this the strongest way available: it
captures the actual DOM node before the lock and asserts object identity with the node after
the unlock.

**Test results — run in the worktree after deleting every `bin/` and `obj/`, real output:**

- `dotnet build PMS.sln` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet test PMS.sln` →
  - `PMS.Application.Tests` — **Failed: 0, Passed: 48, Skipped: 0**
  - `PMS.Api.IntegrationTests` — **Failed: 0, Passed: 60, Skipped: 0**
- `npm test` in `PMS/frontend` → **9 test files, 103 passed, 0 failed, 0 skipped**.
- `npm run build` → succeeded, emitting to `PMS/backend/src/PMS.Api/wwwroot`.
- **Total: 211 automated tests passing (up from F-1's 70), 0 skipped.**

New test files, at the plan's named targets plus four the plan implies:
`PMS.Application.Tests/Services/AuthServiceTests.cs`,
`PMS.Application.Tests/Services/InitialUserSeederTests.cs`,
`PMS.Api.IntegrationTests/Endpoints/AuthEndpointTests.cs`,
`PMS.Api.IntegrationTests/Endpoints/SessionExpiryTests.cs`,
`PMS.Api.IntegrationTests/Security/Pbkdf2PasswordHasherTests.cs`,
`PMS.Api.IntegrationTests/Registration/AuthorizationPolicyTests.cs`,
`frontend/src/features/auth/LoginPage.test.tsx`,
`frontend/src/shared/hooks/useIdleTimer.test.ts`,
`frontend/src/shared/components/ScreenLock.test.tsx`,
`frontend/src/shared/components/RequireAuth.test.tsx`,
`frontend/src/shared/components/forms/forms.test.tsx`.
`frontend/src/App.test.tsx` was updated (not weakened): its routes now sit behind
`RequireAuth`, so it seeds a session, and it gains new assertions that a signed-out visitor is
redirected from every protected path.

**Live smoke against a running instance** (`ASPNETCORE_ENVIRONMENT=Development dotnet run`,
LocalDB `PMSDb`, connection string from user-secrets):

| Check | Result |
|---|---|
| First start | log: `Initial login seeding: Created the initial login 'doctor'.` |
| `SELECT ... FROM AppUsers` | one row, `doctor`, `PasswordHash` starts `PBKDF2-SHA256$210000$`, length 90 |
| **Second start (restart)** | log: `Initial login seeding skipped (SkippedAlreadySeeded)`; **still exactly 1 row** |
| `POST /api/auth/login` (`doctor` / `SeedDoctor#2026!`) | **200**, `{"userName":"doctor","expiresUtc":"2026-09-01T22:20:40+00:00","setupComplete":false}` — 12 h after the 10:20 sign-in |
| `Set-Cookie` | `pms.session=...; path=/; secure; samesite=strict; httponly` — and **no `Expires`/`Max-Age`**, so it dies with the browser |
| `GET /api/auth/session` with / without the cookie | **200** / **401** |
| Wrong password vs. unknown user | **401** both, byte-identical bodies apart from `traceId` |
| `POST /api/auth/login` with empty fields | **400** `problem+json` with per-field `errors` |
| `POST /api/auth/reauth` **with no cookie** | **200** + a fresh cookie — the E-41 path works from an expired session |
| `POST /api/auth/logout` | **204** |
| F-1 regressions | `health=200 health-db=200 unmatched-api=404 root=200 deep-route=200 login=200` — all unchanged |

**Acceptance criteria — walked line by line:**

1. *Login sets `HttpOnly`/`Secure`/`SameSite=Strict`; no token in web storage* — **met.**
   The live `Set-Cookie` above carries all three; `AuthorizationPolicyTests` asserts the
   configured options and `AuthEndpointTests` asserts the emitted header. No token can be in
   web storage because none exists: `SessionResponse` has exactly three fields and no token
   among them, asserted by test. `LoginPage.test.tsx` and `ScreenLock.test.tsx` assert
   `localStorage.length === 0` and `sessionStorage.length === 0` after a full sign-in. The
   plan asks for the storage assertion in the E2E spec; it is written there too
   (`auth.spec.ts`, plus `document.cookie` must not contain `pms.session`) but **that spec
   could not be executed — see the environment limitation below.**
2. *Any `/api/*` route other than `health` and `auth/login` is 401 without a cookie* — **met**,
   and by default-deny rather than per-route opt-in. Verified live (`/api/auth/session` → 401)
   and by test. Two notes for the reviewer: `auth/reauth` is also anonymous **by design** (it
   is the endpoint used when the cookie has already expired — that is the point of E-41), and
   an unmatched `/api/*` path stays a **404**, not a 401, preserving F-1's committed error
   contract. Both are in the asserted allow-list, so neither can widen silently.
3. *After 5 minutes idle the overlay covers all PHI; the underlying route is still mounted* —
   **met.** `useIdleTimer.test.ts` pins the 5-minute threshold and proves activity after the
   lock does **not** lift it (a passer-by nudging the mouse). `ScreenLock.test.tsx` asserts the
   overlay is present, the content beneath is `aria-hidden` and blurred, **and still in the
   document with its typed value**.
4. *Re-authenticating from the overlay restores the exact view; a draft retains every typed
   character* — **met**, and asserted at DOM-node identity: the textarea element captured
   before the lock is the *same object* after the unlock, still holding
   `BP 130/85, review in two weeks`, with component state (a click counter) also intact.
   A separate test asserts the client calls `reauth` and **never** `login`, because going
   through the login page is exactly what would unmount the consultation.
5. *Patient-data inputs render with `autocomplete="off"`* — **met** as a shared convention
   rather than a habit: `TextField` defaults `autoComplete` to `off` and `PatientDataForm`
   sets it on the `<form>`, so every form F-5 onward adds inherits it; `forms.test.tsx` asserts
   both, and that an explicit opt-in stays visible at the call site. The login form and the
   unlock form set it too.

**Assumptions and judgement calls recorded inline in the code:**

- `SessionResponse.setupComplete` is `false` until F-3. Marked `// ASSUMPTION:` in
  `AuthService.cs`. It is not a placeholder — no clinic profile has been captured, so `false`
  is literally correct; F-3 replaces the constant with a read of `ClinicProfile.IsSetupComplete`
  and the wire shape does not change.
- **ASP.NET Core Identity was not used**, only the cookie *handler* that plan §2 actually
  names. Full Identity brings its own user/role schema, which would collide with the plan's own
  `AppUser` entity in §4. Reasoned in `Pbkdf2PasswordHasher.cs`. (Note for the plan owner:
  `modules/08-authentication-authorization.md` describes the adopted mechanism as "cookie-based
  ASP.NET Core Identity", which reads as more than §2 specifies.)
- **FluentValidation was not introduced.** Plan §3 lists a `Validation/` folder, but F-1 never
  added the package, and F-2's only request DTO has two required fields. Validation throws
  F-1's existing `ValidationFailedException`, which the existing middleware already maps to a
  400 with field-keyed `errors` — one error shape, no new dependency. Flagged rather than
  decided.
- **Timing equalisation on an unknown user name**: the login path hashes against a throwaway
  hash when no user matches, so "no such user" is not measurably faster than "wrong password".
  Asserted by test.
- Two folders the plan's §3 tree does not list: `PMS.Infrastructure/Security/` (alongside the
  listed `Printing/` and `Export/`) and `frontend/src/shared/config/`. Same class as F-1's
  `PMS.Application/Exceptions/`; noted for `code-review-pms`.
- **With no connection string configured, `POST /api/auth/login` returns a 500** (the user
  store is not registered), while `GET /api/health/db` returns the diagnosable 503. That
  follows F-1's decision that a missing connection string is a reportable state rather than a
  startup crash. Named here rather than left to be discovered.

**Known environment limitation — E2E written, not proven (unchanged from F-1's carried item):**

`PMS.E2E/specs/auth.spec.ts` is written at the plan's named target, covers the golden path and
the severe edge case E-41 (idle past the lock with a draft open, re-authenticate, assert the
text is still on screen), and typechecks clean (`tsc --noEmit` exit 0). **F-1's `test.fixme`
for the unauthenticated `/login` redirect has been removed and is now a real assertion**, as
`verification-pms` required of F-2. Two `app-shell.spec.ts` specs that reach the app shell now
sign in first, since everything under `/` is behind the guard.

Running `npx playwright test --project=chromium` against a live instance gave **4 passed, 14
failed**, every failure `browserType.launch: spawn EPERM` — the same host process-spawn denial
F-1 recorded, not a defect in this code. The 4 that pass are the API-request specs, which need
no browser. **The browser-dependent assertions are therefore written but unproven, and are not
claimed as passing.** Each one's behaviour is proven by a suite that does run: the cookie
attributes by `AuthEndpointTests` and the live `curl` above; the web-storage assertions by
`LoginPage.test.tsx` / `ScreenLock.test.tsx`; the redirect by `App.test.tsx` and
`RequireAuth.test.tsx`; the E-41 lock/unlock by `ScreenLock.test.tsx` at DOM-node identity.
The genuinely unproven axis remains real-browser rendering. **The deadline recorded against
F-1 stands: the harness must work before F-14.**

**Flagged for review — not decided here:**

- **The committed credential (Deviation 1).** It is a user instruction and was carried out as
  given, but it is a live security exposure the moment this branch is committed, and it should
  be a conscious acceptance by the owner rather than something that slips through review.
- **React Router v6 advisories are unchanged** (GHSA-wrjc-x8rr-h8h6, GHSA-337j-9hxr-rhxg; fix
  in 7.18+). F-1's verification asked for this to be settled "before the routing surface grows
  from F-2 onward". F-2 grew it — `RequireAuth` now issues a `<Navigate>` — so the question is
  now live rather than theoretical. The redirect target is not attacker-controlled (it is
  `location.pathname` of the route the user already reached), so the open-redirect advisory is
  still not reachable, but plan §2 should either be amended to v7.18+ or the risk accepted on
  the record. Built as v6 per the plan, not silently upgraded.
- **F-21 (lockout + credential recovery) is still `Blocked` on C-44 and is now the only thing
  standing between this feature and a real go-live risk:** the clinic can now sign in, and has
  no way to recover if the password is lost. The plan already calls this a go-live gate; F-2
  landing makes it concrete.

Committed on `feature/f-2-auth-session`. Not merged, not pushed, worktree not removed.

---

### 2026-09-01 — F-2 second implementation pass: seed-section rename and independent re-run

**Status unchanged: `Awaiting verification`.** This pass changed one thing and re-proved
everything. Nothing below is carried over from the entry above — every result is output
produced in this pass, inside the worktree at
`C:\Users\NileshMalviya\source\repos\f-2-auth-session`.

**What changed in this pass — the seed config section is now `SeedDoctorUser`.**

The user's instruction named the section `SeedDoctorUser` and asked for it to be "clearly
labeled so it doesn't get confused with the real connection-string secret-handling
convention". It was built as `SeedUser`, which is close but not the name given, and
`SeedDoctorUser` is also the more self-describing of the two for a physician opening
`appsettings.json` to find their own initial login. Renamed in four places, plus a
recommendation comment:

- `PMS/backend/src/PMS.Api/appsettings.json` — section key and its `DEVIATION` comment block,
  now also carrying an explicit **"change this password after the first sign-in"**
  recommendation and a sentence stating why this section is named separately from
  `ConnectionStrings` in the same file.
- `PMS/backend/src/PMS.Api/Startup/InitialUserSeedExtensions.cs` — `SectionName` constant and
  the `<remarks>` deviation block, which now also records the rotation recommendation and that
  F-2 deliberately does **not** build a forced-rotation flow (out of scope; that is F-21,
  `Blocked` on C-44).
- `PMS/backend/tests/PMS.E2E/specs/helpers/credentials.ts` — the pointer comment.
- This document.

The environment-variable escape hatch moves with it: `SeedDoctorUser__UserName` /
`SeedDoctorUser__Password` still override the committed values, so the route back to the
plan's user-secrets design remains a config change and not a code change.

**Full build and test suite, re-run in this pass — real output:**

| Command (run in the worktree) | Result |
|---|---|
| `dotnet build PMS/backend/PMS.sln` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `dotnet test PMS/backend/PMS.sln` | `PMS.Application.Tests` **Failed: 0, Passed: 48, Skipped: 0**; `PMS.Api.IntegrationTests` **Failed: 0, Passed: 60, Skipped: 0** |
| `npm test` (`vitest run`) in `PMS/frontend` | **9 files, 103 passed, 0 failed, 0 skipped** |
| `npm run build` in `PMS/frontend` | Succeeded — 93 modules, emitted `index.html` + `assets/` into `PMS/backend/src/PMS.Api/wwwroot` |
| `tsc --noEmit -p tsconfig.json` on `PMS.E2E` | **exit 0**, no diagnostics |

**Total re-run and passing: 211 automated tests (108 .NET + 103 Vitest), 0 skipped, 0 failed.**
Counts read, not exit codes — this host is known to print `Passed!` on a zero-test project.

*Environment note, not a code defect:* `PMS.E2E/node_modules/typescript` on this host is a
partial install missing its `bin/` folder, so `npm run typecheck` inside `PMS.E2E` fails with
`MODULE_NOT_FOUND` before TypeScript ever runs, and `npm install` reports "up to date" without
repairing it. The specs were typechecked with the frontend's own TypeScript instead
(`node ../../../frontend/node_modules/typescript/bin/tsc --noEmit -p tsconfig.json`, exit 0).

**Live smoke test of the seeding path, against a throwaway database — the user's added
requirement, proven rather than asserted.**

The rename touches the seed read path, and the existing dev database already had a `doctor`
row, which would have made the seeder skip and proved nothing. So a fresh database was used:

1. Created `PMSDb_F2Smoke`; `dotnet ef database update` applied **both**
   `20260825170916_InitialCreate` and `20260901093334_AddAppUser`; `SELECT COUNT(*) FROM
   AppUsers` → **0**.
2. Started the API against it. Log: `Initial login seeding: Created the initial login
   'doctor'.` — so `SeedDoctorUser` is read correctly after the rename.
3. `GET /api/auth/session` with no cookie → **401** with an `application/problem+json` body
   (`"Your session has ended."`), **not** a 302 to HTML.
4. `POST /api/auth/login` with a wrong password → **401**.
5. `POST /api/auth/login` with `doctor` / `SeedDoctor#2026!` → **200**
   `{"userName":"doctor","expiresUtc":"2026-09-02T02:29:57Z","setupComplete":false}` — 12 hours
   ahead of sign-in, as the policy states. The `Set-Cookie` header was
   `pms.session=...; path=/; secure; samesite=strict; httponly`, with **no `expires` and no
   `max-age`** — a session cookie, not a persistent one (E-62). No token in the response body.
6. `GET /api/auth/session` with the cookie → **200**. `GET /api/health` and `/api/health/db`
   without one → **200** each, so the anonymous allow-list is exactly what the plan names.
   `GET /api/nope` without a cookie → **404 problem+json**, not 401 and not the SPA shell.
7. `POST /api/auth/reauth` **with no cookie at all** → **200** and a fresh `Set-Cookie`. This
   is the E-41 mechanism working: the physician can prove identity on a dead session without
   the client navigating anywhere, so nothing beneath the lock is unmounted.
8. `POST /api/auth/logout` with the cookie → **204**, with the cookie cleared
   (`expires=Thu, 01 Jan 1970`, still `secure; samesite=strict; httponly`).
9. Database after the flow: **exactly 1 row**; `PasswordHash` begins
   `PBKDF2-SHA256$210000$mvZ1OdzjFVQ...` (90 chars, per-credential salt, cost in the hash);
   `SELECT COUNT(*) FROM AppUsers WHERE PasswordHash LIKE '%SeedDoctor%'` → **0**, so the
   plaintext is nowhere in the row. `grep` for the plaintext in the whole application log →
   **0 occurrences**. `LastLoginUtc` was updated by the sign-in.
10. **Restarted the API against the same database**: `Initial login seeding skipped
    (SkippedAlreadySeeded): A login already exists; the seed credential was ignored and nothing
    was changed.` Row count still **1**. The seeder cannot overwrite a password the physician
    has since changed.
11. Stopped the instance and dropped `PMSDb_F2Smoke`. `sys.databases` shows only `PMSDb`
    remaining, so the dev database was not disturbed by any of the above.

**The credential deviation, restated because it is the one thing that should not be skimmed.**
`doctor` / `SeedDoctor#2026!` is now in a tracked file and will be in git history permanently
once this branch is merged; rotating the password later does not remove it from history. That
is the user's explicit instruction for this single-user local clinic app's bootstrap
credential, and it is carried out as given — but it is a real exposure, not a formality, and it
should be an owner's conscious acceptance at review rather than something that passes
unremarked. **Recommendation on the record: change the password after the first sign-in.** F-2
does not build a forced-rotation flow — that is out of scope for the feature as planned, and
password change/recovery is F-21, `Blocked` on C-44.

Committed on `feature/f-2-auth-session`. Not merged, not pushed, worktree not removed.
Next: `verification-pms`.
