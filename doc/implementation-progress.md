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
| F-2 | Login, session policy, idle screen lock | Awaiting verification | `f-2-auth-session` / `feature/f-2-auth-session`; test-fixture fix on `fix-session-expiry-clock` / `fix/session-expiry-clock` | 2026-09-03 | Ready for verification-pms. **The "211 tests pass" claim below was true when written but stopped reproducing on 2026-09-01 20:00 UTC** — `SessionExpiryTests` put only the app's `IClock` under test control and left the cookie handler judging ticket expiry by the real system clock, so 3 of its 4 tests failed permanently from that instant and the 4th passed for the wrong reason. **Root-caused and fixed on `fix/session-expiry-clock` (test fixture only; F-2 application code unchanged); backend now 170 passed / 0 failed / 0 skipped.** Original claim, for the record: 211 automated tests pass (108 .NET + 103 Vitest), 0 skipped — re-run from a clean build in a second pass, plus a live smoke against a throwaway DB. Built against the plan's C-44/REC-11 assumption. **Carries a deliberate, user-directed deviation: the seed credential `doctor` / `SeedDoctor#2026!` is committed in plain text in `appsettings.json` under `SeedDoctorUser`** — see the log entries; this is an instruction, not an oversight. Password rotation after first sign-in is recommended, not enforced (F-21 is `Blocked`). E2E browser launch still blocked by the host. |
| F-3 | ClinicProfile + first-run setup gate | Awaiting verification | `f-3-clinic-profile` / `feature/f-3-clinic-profile` | 2026-09-03 | Ready for verification-pms. **The "315 tests pass" claim below stopped reproducing on 2026-09-01 20:00 UTC through no fault of F-3** — it inherited F-2's `SessionExpiryTests` fake-clock defect (see the F-2 row). F-3's own code and tests are not implicated. **Fixed on `fix/session-expiry-clock`; backend now 170 passed / 0 failed / 0 skipped.** Original claim, for the record: 315 automated tests pass (169 .NET + 146 Vitest), 0 skipped, re-run from a clean build, plus a live smoke against a throwaway database. Built against the plan's **Q-4** assumption (PNG signature ≤ 200 KB, footer ≤ 500 chars, nothing prints until `IsSetupComplete`). **Dependency note: F-2 is still tracker-status `Awaiting verification` but was merged to `main` at `2eb69d4` before its gate ran — the same out-of-band-merge pattern F-1 recorded.** F-3 was built on that merged code because it is on `main` and demonstrably working, **not** because F-2 is `Built & Verified`; only verification-pms sets that. **One real defect was found by the live smoke and fixed** (oversize signature upload returned 400 instead of the specified 413 — the integration test had passed for a reason that did not hold under real Kestrel; see the log). E2E browser launch still blocked by the host. |
| F-4 | Doctor-configured settings | **Built & Verified** | `f-4-clinic-settings` / `feature/f-4-clinic-settings` | 2026-09-07 | Verified by verification-pms 2026-09-07 — all 5 ACs met on independently re-run evidence; **417 tests re-run by me (243 .NET + 174 Vitest), 0 failed, 0 skipped**, plus my own live smoke against a throwaway DB under Kestrel. AC-1/AC-2 (gender dropdown, retired option) were proven end-to-end through F-5's patient records, since they are unobservable inside F-4 alone. **One carried item, not an F-4 defect:** AC-4's confirm-and-save half needs the consultation page (F-11) and cannot exist yet — F-4's side (evaluator returns a list, never throws, never blocks) is verified. Next: `code-review-pms`. Built against the plan's **Q-9** assumption (gender seeded Female/Male/Other/Not stated, "Not stated" never removable — E-23) and **Q-10 / Q-2** (temperature unit from `ClinicProfile`, BP in mmHg, thresholds blank by default, warnings soft — E-12, E-24). **417 automated tests pass (243 .NET + 174 Vitest), 0 failed, 0 skipped**, re-run from a clean build, plus a live smoke against a throwaway database under real Kestrel. **Dependency note: F-3 is still tracker-status `Awaiting verification` but is on `main` at `990ec19`; F-4 was built on it because it is on `main` and demonstrably working (its migration applies and its endpoints answer live), not because F-3 is `Built & Verified` — only verification-pms sets that.** Carries one commit that is **not** F-4 work: the `fix/session-expiry-clock` test-fixture fix, cherry-picked file-only because that branch is still unmerged and `main` alone fails 3 tests. E2E `settings.spec.ts` written and typechecked; browser launch still blocked by the host. |
| F-5 | Patient registration & profile | **Built & Verified** | `f-5-patient-registration` / `feature/f-5-patient-registration` | 2026-09-07 | Verified by verification-pms 2026-09-07 — **525 tests re-run by me (315 .NET + 210 Vitest), 0 failed, 0 skipped**, twice for each suite (no flake), plus my own live smoke against a throwaway DB under Kestrel. **Verified against the F-4 branch baseline, not `main`** — this branch contains F-4's commits, so **F-4 must merge before or with F-5**; merging F-5 alone would carry F-4 in without F-4 clearing its own gate. Builder's two flagged items both checked and both stand up: (1) the plan F-5 §2 check constraint really is a tautology, the shipped `CK_Patient_AgeShape` really enforces the E-9 rule — I proved it with raw SQL inserts that bypass the service; (2) the phone-equivalence gap is correctly F-6's, not F-5's. **One plan-vs-build discrepancy needing an owner ruling, not a rework instruction:** AC-2's "the API rejects a bare age with 400" is not what was built (a bare approx age is completed with today's date and accepted). See the log entry. Builder's full log entry for F-5 lives on the branch's copy of this file and lands on merge. Next: `code-review-pms`. |
| F-6 | Duplicate detection + `merged_into` pointer | Awaiting verification | `f-6-duplicate-detection` / `feature/f-6-duplicate-detection` — **and already on `main`** at `4e1c33c` via PR #6 | 2026-09-07 | **Status corrected from `Not Started` on 2026-09-07 during the F-7 merge: `Not Started` was factually wrong.** F-6's code is on the default branch (`PatientDuplicateService`, `NameSimilarity`, migration `AddPatientDuplicateIndexes`, the `duplicate-check` and `mark-merged` routes, `DuplicateWarningDialog`, and six test files) and builds and passes in the F-7 worktree. **No F-6 builder log entry ever reached this file** — see the 2026-09-07 F-6 tracking-gap entry in the log; the gap is recorded, not filled in, because this role did not build F-6 and will not invent a report for it. F-6 reached `main` without `Built & Verified` and without `Reviewed`, the same out-of-band pattern as F-1/F-2/F-4/F-5. **`verification-pms` still owes F-6 a pass, and will have to work from the code and the plan rather than from a builder's report.** Built against Q-13's assumption per the plan (0.85 name-similarity threshold, warn-never-block). Its `NameSimilarity` is now the single similarity implementation shared with F-7 — see the F-7 merge entry. |
| F-7 | Patient search, recent patients, picker | Awaiting verification | `f-7-patient-search` / `feature/f-7-patient-search` | 2026-09-07 | Ready for verification-pms. Built against the plan's **C-22** assumption (no `Q-` exists) — substring on `NormalizedName`, digits match on `NormalizedPhone` incl. last-4, min length 2, 300 ms debounce, ranked exact → prefix → substring → phone, inactive and merged excluded by default behind one toggle. **631 automated tests pass (384 .NET + 247 Vitest), 0 failed, 0 skipped**, both suites re-run twice from a clean build, plus a live smoke against a throwaway database under real Kestrel. **Measured, not assumed: search p95 = 185 ms over 5,000 seeded patients against the plan's 2,000 ms budget** (AC-4). **Cut from `feature/f-5-patient-registration`, not `main`** — F-5 is `Built & Verified` but lives only on that branch, which carries F-4's commits beneath it, so **F-4 and F-5 must merge before or together with F-7**. **Three items flagged for the plan owner, none silently decided:** (1) the C-22 fuzzy-fallback threshold is **0.7**, deliberately looser than F-6's 0.85 — reasoning and a pinning test now in the shared `NameSimilarity`; (2) **"recent patients" is undefined in the BRD** and ships as most-recently-*registered*, because plan §2 forbids new entities and `Visit` is F-10; (3) `lastVisitDate` ships in the DTO but is **always null until F-10**, with `registeredOn` carrying E-28's date axis meanwhile. **F-7 adds no migration** — F-5 already created the `NormalizedName`/`NormalizedPhone` indexes plan §2 attributes to F-6; confirmed live via `sys.indexes` and `has-pending-model-changes`. **Two merge conflicts with the concurrent F-6 branch were expected by design, not by accident:** `shared/components/PatientPickerRow.tsx` and `PatientNameSimilarity` — the plan says both features render candidates identically and share one similarity function. **Both are now resolved: `main` (F-4 + F-5 + F-6) was merged into this branch on 2026-09-07, all six conflicts resolved by reading both sides, and F-6 and F-7 now share one `PatientPickerRow` and one `NameSimilarity` with two call-site thresholds (0.85 duplicate / 0.7 search) — see the F-7 merge entry at the end of the log for the full resolution and the re-run test output (745 tests: 471 .NET + 274 Vitest, 0 failed, 0 skipped, clean build). The F-4/F-5 merge-order constraint is discharged — those features are on `main`.** Branch pushed to `origin`; **not merged into `main`**. E2E `patient-search.spec.ts` written and typechecked; browser launch still blocked by the host. |
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

> **Correction, 2026-09-03 — this result no longer reproduces and stopped being true on
> 2026-09-01 at 20:00 UTC.** Three of the four `SessionExpiryTests` began failing on any run
> after that instant: the fixture pinned a fake `IClock` to a hardcoded `2026-09-01T08:00Z`
> but left the cookie handler validating ticket expiry against the real system clock, so every
> ticket went permanently stale 12 hours later. The fourth test kept reporting green **for the
> wrong reason** (its expected 401 was arriving from real-clock staleness, not from the
> simulated advance past the absolute cap). The `dotnet test` half of this total was therefore
> `Failed: 3, Passed: 105` from 2026-09-01 20:00 UTC onward, not `Passed: 108`. Root-caused and
> fixed on branch `fix/session-expiry-clock` — see the 2026-09-03 log entry at the end of this
> file. **This was a defect in F-2's test fixture, not in F-2's application code**, which is
> unchanged by the fix.

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

> **Correction, 2026-09-03 — the `dotnet test` row above is no longer reproducible.** It was
> genuine when run, but it was date-dependent without anyone realising: `SessionExpiryTests`
> substituted the application's `IClock` and not the cookie handler's `TimeProvider`, so from
> 2026-09-01 20:00 UTC (the fixture's hardcoded start instant plus the 12-hour
> `ExpireTimeSpan`) the real clock expired every ticket regardless of the simulated time.
> `PMS.Api.IntegrationTests` reads **Failed: 3, Passed: 57** on any run after that instant, and
> one further test passes vacuously. Fixed on `fix/session-expiry-clock`; see the 2026-09-03
> entry at the end of this file. F-2's application code is untouched by that fix — the defect
> was entirely in the test fixture, so F-2's *behaviour* is as this entry describes it.

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

---

### 2026-09-01 — F-3 ClinicProfile + first-run setup gate

**Status: `Awaiting verification`.** Handed to verification-pms. Branch left for review, not
merged, not pushed, worktree not removed.

- Reused the existing isolated worktree at
  `C:\Users\NileshMalviya\source\repos\f-3-clinic-profile`, branch `feature/f-3-clinic-profile`,
  cut from `main` at `8f0da5d`. Confirmed distinct from the main working tree (`git worktree list`)
  and clean before writing anything. Also confirmed `doc/planning-pms-verification.md`,
  `doc/brainstorm-pms-verification.md` and `doc/implementation-progress.md` are byte-identical to
  the main tree's (modulo CRLF), so this was built against the current committed plan.

**Dependency flag, stated rather than absorbed — F-2 was merged out of band.**
The plan makes F-3 depend on F-1 and F-2. F-1 is `Built & Verified`. **F-2 is still tracker-status
`Awaiting verification`, yet its code is already on `main` at `2eb69d4`** ("Merge pull request #2
from Nilesh-PIO/feature/f-2-auth-session") — the same sequencing violation `verification-pms`
recorded against F-1. F-3 was built on top of that merged code because it is on `main` and
demonstrably working (its auth paths are exercised live in the smoke below). **This is not a claim
that F-2's dependency is satisfied in the pipeline's sense** — only `verification-pms` can set
`Built & Verified`, and it has not. If F-2 later comes back `Needs rework`, F-3's session-gate
integration is the surface that would be affected.

**Readiness — Q-4, built against the plan's stated assumption.**
Plan F-3 point 1 says the entity and the gate are settled by REC-4 and that what remains open is
the *content*: **Q-4 — the clinic header/footer text, the registration number, and who supplies the
signature image.** That is a business/clinical-identity question for the physician, so nothing here
invents an answer; what is built is the *shape* the plan specifies, with the clinic supplying the
content through the UI:

- signature stored as an uploaded **PNG at most 200 KB** in `ClinicProfile.SignatureImage`;
- prescription footer free text **at most 500 characters**;
- **no prescription can be printed until `IsSetupComplete` is true** (E-1);
- **with no signature image the footer renders a ruled signature area, never a broken-image
  placeholder** — implemented in the form preview now, and inherited by F-14's PDF.

No header, footer or registration value is hardcoded anywhere; every one of them is data the
physician enters.

**Built (backend):**

- `PMS.Domain`: `Entities/ClinicProfile.cs` (singleton, `SingletonId = 1`),
  `Enums/TemperatureUnit.cs`.
- `PMS.Application`: `Abstractions/IClinicProfileService.cs`,
  `Abstractions/IClinicProfileRepository.cs`; `Dtos/Clinic/ClinicProfileResponse.cs`,
  `Dtos/Clinic/UpsertClinicProfileRequest.cs`; `Services/ClinicProfileService.cs`;
  `Exceptions/PayloadTooLargeException.cs`; registered in `DependencyInjection.cs`.
- `PMS.Infrastructure`: `Persistence/Configurations/ClinicProfileConfiguration.cs`,
  `Persistence/Repositories/ClinicProfileRepository.cs`, `PmsDbContext.ClinicProfile` DbSet,
  `Migrations/20260901163456_AddClinicProfile.cs`, registration in `DependencyInjection.cs`.
- `PMS.Api`: `Controllers/ClinicProfileController.cs` (the four routes exactly as the plan's table
  specifies), `Filters/RequiresSetupCompleteAttribute.cs`, `Filters/MaxUploadBytesAttribute.cs`;
  `Middleware/ProblemDetailsMiddleware.cs` gains 413 mapping. `ClinicProfileController` depends on
  `IClinicProfileService`, never on `PmsDbContext`.
- **F-2's placeholder is gone.** `AuthService` had `SetupCompleteUntilF3 = false` hardcoded;
  it now takes `IClinicProfileService` and reports the real answer. `DescribeSessionAsync`
  **re-reads** it on every call rather than stamping it into the cookie — the physician completes
  setup *during* a session, and a cached claim would keep bouncing them back to `/setup` until they
  signed out and in again. Pinned by a test.

**Three backend decisions worth naming:**

1. **`IsSetupComplete` is derived, not trusted.** The column is still persisted (F-14 and any
   reporting query need to read it), but `ClinicProfileService` recomputes it from the stored
   values on every read and write. This is not defensive theatre in this stack: **SQL Server via
   SSMS is the project's stated database tool**, so a hand-run `UPDATE` that blanks `DoctorName`
   while leaving `IsSetupComplete = 1` is an ordinary Tuesday, and it would silently disarm the
   E-1 print gate. Proven live below — the column said `1`, the API said `false`.
2. **The singleton is enforced by the database, not just the service.**
   `CK_ClinicProfile_SingletonRow` (`[Id] = 1`) plus a non-identity key. "Which clinic name goes on
   the prescription?" must not have two possible answers, and the SSMS path is how a second row
   would otherwise appear. Proven live: a direct `INSERT` of `Id = 2` fails with Msg 547.
3. **The signature is validated by its bytes, not its content type.** A renamed `.jpg` passes any
   content-type check and then fails inside the PDF renderer at print time — with a patient
   waiting. Checking the eight-byte PNG magic moves that failure to the settings screen, where it
   costs nothing.

**Built (frontend):**

- `features/clinic/`: `ClinicProfileForm.tsx` (the one form both screens render, per plan F-3
  point 4), `ClinicProfilePage.tsx` (route `/settings/clinic`), `clinicApi.ts`
  (`getProfile`/`saveProfile`/`uploadSignature`/`deleteSignature`), `useClinicProfile.ts`,
  `types/clinicProfile.ts`.
- `features/setup/FirstRunSetupPage.tsx` (route `/setup`).
- `shared/components/RequireSetup.tsx` (the router-level gate),
  `shared/components/forms/TextAreaField.tsx`, `shared/format/temperature.ts`.
- `routes.tsx` mounts the two real pages and wraps the layout branch in
  `RequireAuth` -> `RequireSetup` -> `AppLayout`. `/setup` sits behind `RequireAuth` but
  deliberately **outside** `RequireSetup`, or the gate would redirect to itself forever.

**Data integrity check (F-3 point 5) — the mechanism, not a mention.** The risk F-3 removes is
**E-1**: a prescription printed with no clinic identity, which is not a weak document but an
unusable one a pharmacy will refuse. It is closed at three levels, so no single omission reopens
it: the **client** never lets the physician reach a consultation while `setupComplete` is false;
the **server** exposes `[RequiresSetupComplete]` + `EnsureSetupCompleteAsync`, which throws
`DomainRuleException("setup-incomplete")` producing a 409 through F-1's existing error contract;
and the **answer itself** is derived from the stored values rather than read from a flag anyone
could set. Deleting the signature deliberately does *not* un-arm the gate — a physician who signs
by hand has a complete setup, not a broken one.

**Test results — run in the worktree after deleting every `bin/` and `obj/`, real output:**

| Command (run in the worktree) | Result |
|---|---|
| `dotnet build PMS/backend/PMS.sln` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `dotnet test PMS/backend/PMS.sln` | `PMS.Application.Tests` **Failed: 0, Passed: 83, Skipped: 0**; `PMS.Api.IntegrationTests` **Failed: 0, Passed: 86, Skipped: 0** |
| `npm test` (`vitest run`) in `PMS/frontend` | **13 files, 146 passed, 0 failed, 0 skipped** |
| `npm run build` in `PMS/frontend` | Succeeded — 102 modules, emitted into `PMS/backend/src/PMS.Api/wwwroot` after deleting it first |
| `dotnet ef migrations has-pending-model-changes` | **"No changes have been made to the model since the last migration"** |
| `tsc --noEmit` on `PMS.E2E` | **exit 0**, no diagnostics |

**Total: 315 automated tests passing (169 .NET + 146 Vitest), 0 skipped, 0 failed** — up from
F-2's 211. Counts read, not exit codes; this host is known to print `Passed!` on a zero-test
project.

> **Correction, 2026-09-03 — the `dotnet test` row above is no longer reproducible, and it
> inherited a defect from F-2 rather than introducing one.** F-3 ran on 2026-09-01 while the
> calendar still happened to agree with `SessionExpiryTests`'s hardcoded fake-clock start
> instant, so the suite was genuinely green at the time. After 2026-09-01 20:00 UTC it is not:
> `PMS.Api.IntegrationTests` reads **Failed: 3, Passed: 83**, because that fixture never put the
> cookie handler's expiry clock under test control. Independently re-confirmed on `main` on
> 2026-09-03. Nothing in F-3's own code or tests is implicated, and F-3's application behaviour
> is as this entry describes it. Fixed on `fix/session-expiry-clock` — see the 2026-09-03 entry
> below.

New test files, at the plan's named targets plus four the plan implies:
`PMS.Application.Tests/Services/ClinicProfileServiceTests.cs`,
`PMS.Api.IntegrationTests/Endpoints/ClinicProfileEndpointTests.cs`,
`PMS.Api.IntegrationTests/Endpoints/SetupGateTests.cs`,
`frontend/src/features/clinic/ClinicProfilePage.test.tsx`,
`frontend/src/features/setup/FirstRunSetupPage.test.tsx`,
`frontend/src/shared/components/RequireSetup.test.tsx`,
`frontend/src/shared/format/temperature.test.ts`,
`PMS.E2E/specs/first-run.spec.ts`.
`AuthServiceTests`, `ApplicationRegistrationTests`, `App.test.tsx` and `testUtils.tsx` were updated
(not weakened): `aSession()` now defaults to `setupComplete: true`, because a working clinic is one
whose profile is saved, and the first-run case is asserted explicitly in its own block instead of
being the silent default.

**Acceptance criterion 3 and the F-14 seam — how it is proven without shipping F-14's route.**
AC-3 names `POST /api/prescriptions/...`, which is F-14's endpoint and does not exist. Building a
stub of it here would put a fake route on the live surface that someone later has to find and
remove. What F-3 actually owns is the *gate*, so the gate is exercised end-to-end — real filter,
real service, real database, real ProblemDetails middleware — against a controller that exists
**only in the integration-test assembly**, mounted at the route shape F-14 will use and loaded
through a test-only `AddApplicationPart`. F-14's job is then one line:
`[RequiresSetupComplete]` on the real controller. If it forgets, that is an F-14 test failure, not
a hole this papers over. Flagged for the reviewer rather than decided silently.

---

#### A real defect the live smoke caught that the test suite did not

**Symptom.** An oversize signature upload returned **400**, not the **413** the plan's route table
specifies — and the message told the physician their *form* was malformed when in fact their
*file* was too big. The fix for that is a different file, not a different field, so the wrong
status here is a genuinely misleading one.

**Cause.** MVC runs its value-provider factories during model binding, and
`FormValueProviderFactory` calls `ReadFormAsync` for **any** action whose request has a form
content type. When that read tripped the request-size limit, the factory recorded "Failed to read
the request form" in `ModelState` and `[ApiController]`'s automatic `ModelStateInvalidFilter`
answered 400 — **the action body never ran**, so the `IFormFile`-length check inside it could not
fire.

**Why the suite missed it, which is the part worth reading.** The integration test asserting 413
**passed**. Under `WebApplicationFactory`'s in-memory server the oversize request fails earlier and
happens to produce the right status; under real Kestrel it does not. That is a false green, and it
would have shipped. The fix is deliberately server-independent: `MaxUploadBytesAttribute` is an
`IAsyncResourceFilter` — resource filters run **before** model binding — and it checks
`Content-Length` only, which behaves identically under both servers, so test and deployed behaviour
cannot diverge here again. A test pinning the regression was added
(`A_signature_past_the_transport_limit_is_still_a_413_and_not_a_400`).

**Recorded rather than quietly fixed** because it is evidence about this repo's test setup, not
just about one endpoint: an integration test that only ever runs in-memory can assert a status the
real server does not produce.

---

**Live smoke against a running instance and a throwaway database.** The dev database already had a
clinic profile, which would have made the first-run path untestable, so `PMSDb_F3Smoke` was created
fresh, migrated, exercised, and dropped:

| Check | Result |
|---|---|
| `dotnet ef database update` on the empty DB | applied all three migrations; `sys.check_constraints` lists `CK_ClinicProfile_SingletonRow` as `([Id]=(1))` |
| `POST /api/auth/login` on the fresh clinic | **200** `{"userName":"doctor",...,"setupComplete":false}` |
| `GET /api/clinic-profile` with no profile | **404** `problem+json` — the state the client renders as a blank setup form |
| `GET /api/clinic-profile` with no cookie | **401** |
| `PUT` with blank fields | **400** with all four field errors: `ClinicName`, `DoctorName`, `DoctorRegistrationNo`, `TemperatureUnit` |
| `GET /api/auth/session` after that rejected PUT | still `setupComplete: false` — **a 400 wrote nothing** |
| `PUT` a valid profile | **200**, `isSetupComplete: true` |
| `GET /api/auth/session` immediately after | **`setupComplete: true`** — the gate lifts mid-session, no re-login |
| `GET /api/clinic-profile` | full round-trip, address newlines and footer intact |
| Signature: valid PNG | **200**, `signatureImageDataUrl` starts `data:image/png;base64,` |
| Signature: renamed non-PNG | **400** `{"file":["The signature image must be a PNG file."]}` |
| Signature: 250 KB | **413** `{"detail":"The signature image must be 200 KB or smaller.","limitBytes":237568}` |
| Signature: 700 KB | **413** |
| Database after the rejected uploads | signature still the **67-byte** valid PNG — a rejected upload changes nothing |
| `DELETE /api/clinic-profile/signature` | **200**, signature `null`, `isSetupComplete` still **true** |
| **Direct `INSERT` of a second row (the SSMS path)** | **Msg 547** — `The INSERT statement conflicted with the CHECK constraint "CK_ClinicProfile_SingletonRow"` |
| **`UPDATE ClinicProfile SET DoctorName=''` leaving `IsSetupComplete = 1`** | column still reads **`1`**, but `GET /api/auth/session` reports **`setupComplete: false`** and `GET /api/clinic-profile` reports **`isSetupComplete: false`** — the derivation defends the print gate against an out-of-band edit |
| Restoring the doctor name | gate re-arms, `setupComplete: true` |
| F-1 / F-2 regressions | `health=200 health-db=200 unmatched-api=404 problem+json`, `/`=200 html, `/setup`=200 html, `/settings/clinic`=200 html, `/patients/123`=200 html; unauthenticated `clinic-profile=401 auth-session=401` |
| Teardown | `PMSDb_F3Smoke` dropped; `sys.databases` shows only `PMSDb` — the dev database was not disturbed |

**Acceptance criteria — walked line by line:**

1. *With an empty `ClinicProfile` table, every authenticated route redirects to `/setup`* —
   **met.** `RequireSetup` wraps the whole layout branch, so this is one guard rather than a
   per-route habit. Asserted for **all seven** authenticated paths in `App.test.tsx`
   ("sends a signed-in physician from %s to /setup while the clinic is unconfigured") and again in
   `RequireSetup.test.tsx`. Also asserted that a **signed-out** visitor goes to `/login`, not
   `/setup` — auth is asked first, because `setupComplete` is a property of a session.
2. *Saving clinic name, doctor name, registration number and temperature unit sets
   `IsSetupComplete = true` and lifts the redirect* — **met**, and verified live end to end
   (rows 6-8 of the smoke table): the same session that reported `setupComplete: false` reported
   `true` immediately after the PUT, with no re-login. `useClinicProfile` invalidates the session
   query on every successful write, which is the half that makes the redirect actually lift;
   `FirstRunSetupPage.test.tsx` asserts that invalidation.
3. *`POST /api/prescriptions/...` returns 409 with a `ProblemDetails` naming setup as incomplete
   while `IsSetupComplete` is false (E-1)* — **met, through the F-14 seam described above.**
   `SetupGateTests` drives the real filter and gets **409** with
   `ruleType: "setup-incomplete"` and a detail naming all four missing fields; a further test proves
   the gate opens once the profile is saved, so it is not merely closed; another proves a
   signed-out caller gets **401**, not a 409 that would leak whether this clinic is configured.
4. *An uploaded signature renders in the prescription preview; with no signature, a ruled signature
   area renders instead* — **met for the preview F-3 owns.** The form renders the uploaded image
   when present and a **ruled signature area** (`data-testid="signature-rule"`) when not, so what
   the physician sees is what will print; both branches are asserted. **The prescription PDF itself
   is F-14's**, and the plan agrees — F-3's own point 8 says F-3 *blocks* F-14. Named here so the
   reviewer sees the boundary rather than discovering it: `ClinicProfileResponse` carries the
   signature as a data URL and `null` when absent, which is the contract F-14 consumes.
5. *The chosen temperature unit is displayed alongside every stored temperature in UI and print
   (E-24)* — **met to the extent F-3 can meet it, and honestly bounded.** F-3 stores no
   temperatures — F-11 captures them and F-14 prints them. What F-3 ships is the mechanism that
   makes the criterion hold *by construction* later: the unit is captured with **no default**
   (`TemperatureUnit.Unspecified = 0`, so an unanswered column can never masquerade as an answer),
   it is required for the setup gate, and `shared/format/temperature.ts` is the single function
   F-11/F-14 render through — it **never** returns a bare number, and returns `37 (unit not set)`
   rather than `37` if it is ever handed an unconfigured clinic. Asserted by
   `temperature.test.ts`. **The UI/print half of this criterion cannot be fully closed until F-11
   and F-14 land, and should be re-checked then.**

**Assumptions and judgement calls recorded inline in the code:**

- `TemperatureUnit.Unspecified = 0` — plan F-3 point 2 requires that "TemperatureUnit is chosen" be
  checkable, which needs a representable "not chosen" state. Marked `ASSUMPTION` in
  `TemperatureUnit.cs`.
- `ClinicProfileResponse` field list — the plan names the DTO but not its shape. The signature is
  **inlined as a `data:` URL** rather than exposed through a fifth
  `GET /api/clinic-profile/signature` route, because the plan's route table has exactly four
  entries and adding one would change what F-14 builds against. Bounded by the same 200 KB cap; the
  profile is read only on the two settings screens, never per patient or per visit. Marked
  `ASSUMPTION` in `ClinicProfileResponse.cs`.
- **`AddressLines` and `PrescriptionFooter` do not gate setup** — a home-visit practice may print no
  address, and neither field is what makes a prescription dispensable. The plan's point 2 names
  only four fields; this states the corollary explicitly. Asserted by test.
- **Removing the signature does not un-gate the clinic** — plan point 1 says a missing signature
  prints a ruled area, so signing by hand is a supported way to work. Asserted by test.
- `DELETE`/`POST` on the signature routes return **404** when no profile exists yet. The plan lists
  200/400/413 for those routes and does not name 404; it follows from the same
  `NotFoundException` path as `GET`, and the client never hits it because the form withholds the
  upload control until the profile is saved. Flagged rather than assumed to be intended.
- Two folders the plan's section 3 tree does not list: `PMS.Api/Filters/` and
  `frontend/src/shared/format/`. Same class as F-1's `PMS.Application/Exceptions/` and F-2's
  `frontend/src/shared/config/`; noted for `code-review-pms`.
- `TextAreaField.tsx` was added beside F-2's `TextField.tsx` so the address and footer inherit the
  same `autoComplete="off"` E-65 convention rather than each re-deciding it.

**Known environment limitation — E2E written, not proven (unchanged from F-1 and F-2):**

`PMS.E2E/specs/first-run.spec.ts` is written at the plan's named target, covers E-1 (a fresh
database routes to `/setup`, deep-linking past it fails, the prescription action is refused until
the profile is saved), and typechecks clean. `playwright test --list` enumerates **25 specs in 3
files**. A direct launch probe on this host returns **`browserType.launch: spawn EPERM`** — the same
process-spawn denial recorded against F-1 and F-2, not a defect in this code. **The
browser-dependent assertions are therefore written but unproven and are not claimed as passing.**
Each one's behaviour is proven by a suite that does run: the `/setup` redirect by `App.test.tsx`
and `RequireSetup.test.tsx`; the profile round-trip by `ClinicProfileEndpointTests` and the live
smoke; the 409 by `SetupGateTests`; the ruled signature area by `ClinicProfilePage.test.tsx`.
`helpers/credentials.ts` gained `ensureClinicSetup(page)` and `signIn` now clears the setup gate, so
the existing F-1/F-2 specs still reach the app shell. **The deadline recorded against F-1 stands:
the harness must work before F-14.**

**Flagged for review — not decided here:**

- **F-2's out-of-band merge (top of this entry).** F-3 sits on top of code that has not passed its
  own gate. Worth `verification-pms` knowing before it verifies F-3.
- **The in-memory-versus-Kestrel false green** described above. It is fixed for this endpoint, but
  the general lesson — `WebApplicationFactory` can report a status Kestrel would not — applies to
  every integration test in this repo.
- **AC-5 is only half-closable at F-3** (no temperatures exist yet). It should be re-checked when
  F-11 and F-14 land rather than treated as fully discharged now.
- **AC-3's route is F-14's**, proven through a test-only controller. F-14 must add
  `[RequiresSetupComplete]` to the real `PrescriptionsController`.
- **React Router v6 advisories are unchanged** (GHSA-wrjc-x8rr-h8h6, GHSA-337j-9hxr-rhxg; fix in
  7.18+). F-3 grew the routing surface again — a second `<Navigate>` guard — so the question F-1's
  verification raised and F-2's re-raised is now three features old. Built as v6 per plan section 2,
  not silently upgraded, but section 2 should either be amended to v7.18+ or the risk accepted on
  the record.
- **F-2's committed seed credential is untouched by this feature** and remains a live exposure once
  merged; restated only so it is not forgotten between features.

Committed on `feature/f-3-clinic-profile`. Not merged, not pushed, worktree not removed.
Next: `verification-pms`.

---

### 2026-09-03 — Bug fix (not a Feature ID): F-2's `SessionExpiryTests` validated cookie expiry against the real wall clock

**Scope: one test file. No application code changed, no feature advanced.** This is a defect in
F-2's test fixture, found and independently reproduced on `main` after F-2 and F-3 had both been
merged. F-2 and F-3 keep their existing `Awaiting verification` status; nothing here promotes or
demotes a feature.

- Worktree: `C:\Users\NileshMalviya\source\repos\fix-session-expiry-clock`, branch
  `fix/session-expiry-clock`, cut from `main` at `990ec19` via `worktree-pms`.

**Root cause — two clocks, only one of them substituted.**
`ClockControlledWebAppFactory` replaced the application's `IClock` with a `MutableClock` pinned to
a hardcoded `2026-09-01T08:00:00Z`. That covered the application's half of the session path:
`AuthService` stamps the absolute-expiry claim from `IClock`, `AuthController.SignInAsync` stamps
`AuthenticationProperties.IssuedUtc` from `IClock`, and `EnforceAbsoluteExpiryAsync` compares
against `IClock`. It did **not** cover the cookie authentication handler itself, which validates
the ticket's own `ExpiresUtc` against `CookieAuthenticationOptions.TimeProvider` — left at its
default of `TimeProvider.System`.

So the ticket was *issued* at the simulated instant and *judged* by the real one. From
`2026-09-01T08:00Z` + `ExpireTimeSpan` (12 h) = **2026-09-01 20:00 UTC** onward, every ticket these
tests issued was already expired to the handler before the first assertion ran. The failure is
permanent and date-driven, not flaky and not tied to a particular simulated instant: it fails on
every run from that moment onward.

**Reproduced before fixing** — `dotnet test PMS.sln --filter "FullyQualifiedName~SessionExpiryTests"`
in the fresh worktree: **Failed: 3, Passed: 1, Skipped: 0, Total: 4**.

**The fourth test was passing for the wrong reason — confirmed, not assumed.**
`A_session_is_dead_once_it_passes_twelve_hours_however_active_it_has_been` expects a 401, and a 401
is exactly what real-clock staleness also produces. It was asserting the right status code sourced
from the wrong mechanism, and it never noticed that its 11 intermediate "keep using the session"
requests had all been 401s too. Its `for` loop discarded every intermediate response without
asserting on it, which is what let the vacuous pass hide.

**What changed** (`PMS/backend/tests/PMS.Api.IntegrationTests/Endpoints/SessionExpiryTests.cs`, the
only file touched):

1. `MutableClock` now backs **both** clocks from one instant: it still implements `IClock`, and it
   exposes a `TimeProvider` property whose nested `ClockBackedTimeProvider` overrides `GetUtcNow()`
   to return the same field. `Advance`/`Reset` move both by construction, so the two cannot drift
   apart. Timers and timestamps deliberately stay on the base system implementation — only
   wall-clock reads are simulated, so nothing makes a real timer wait years.
2. `ClockControlledWebAppFactory` adds
   `services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme,
   o => o.TimeProvider = Clock.TimeProvider)`. Scoped to the cookie scheme rather than registered as
   a host-wide `TimeProvider`: data protection, Kestrel and hosted services have no business being
   told it is a different year, and widening it would trade this bug for a subtler one.
3. The fixed start instant is **kept** (reproducibility) but moved to `2020-01-01T08:00:00Z` and
   documented as deliberately long past. This makes the class self-enforcing: if anyone ever
   un-wires the `TimeProvider` again, every ticket is years stale and the suite fails on the *next*
   run rather than on some future date nobody is watching for. The hardcoded date was not the root
   cause and was not simply deleted.
4. New regression guard `The_cookie_handler_judges_expiry_by_the_fixture_clock_not_the_wall_clock`
   asserts against the resolved `CookieAuthenticationOptions` that `TimeProvider` is the fixture's
   instance and that it advances with `Clock.Advance` — checked at the options level precisely
   because a status code cannot distinguish "expired by simulation" from "expired by the calendar".
5. The absolute-cap test now **asserts every one of its 11 hourly requests is 200** before expecting
   the 401, and additionally asserts that sliding renewal actually reissued the `pms.session` cookie
   during those hours. That is what makes the final 401 attributable to the absolute cap rather than
   to the sliding window running out — the renewal pushes the handler's own window out to roughly
   hour 19, so at hour 12:01 only the absolute-expiry claim can be rejecting it. The two re-auth
   tests gained an explicit "alive first" assertion for the same reason.

**Negative control — the fix is proven load-bearing.** With the `TimeProvider` registration
temporarily removed and everything else left in place, the suite goes to **Failed: 5, Passed: 0**.
All five fail, *including* the absolute-cap test that used to pass vacuously. Restoring the one
registration returns it to **Failed: 0, Passed: 5**. The tests now pass because of simulated time
and fail without it, which is the property that was missing.

**Full suite, run in the worktree — real output, counts read rather than exit codes:**

| Command (run in the worktree) | Result |
|---|---|
| `dotnet build PMS/backend/PMS.sln` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `dotnet test PMS/backend/PMS.sln` | `PMS.Application.Tests` **Failed: 0, Passed: 83, Skipped: 0**; `PMS.Api.IntegrationTests` **Failed: 0, Passed: 87, Skipped: 0** |
| `dotnet test --filter "FullyQualifiedName~SessionExpiryTests"` | **Failed: 0, Passed: 5, Skipped: 0, Total: 5** |
| `npm test` (`vitest run`) in `PMS/frontend` | **13 files, 146 passed, 0 failed, 0 skipped** |

**Total: 316 automated tests passing (170 .NET + 146 Vitest), 0 skipped, 0 failed** — backend up 1
from F-3's 169, the new regression guard. No other test regressed. The frontend was re-run only to
confirm the untouched suite is genuinely green today; no frontend file was modified.

**No other test file shares this defect — checked, not assumed.** `ClockControlledWebAppFactory`
and `MutableClock` are referenced only by `SessionExpiryTests.cs`; every other integration test uses
`TestWebAppFactory` or `NoDatabaseWebAppFactory` with the real `IClock` and the real
`TimeProvider`, which are mutually consistent, so no ticket is ever issued and judged by different
clocks. The two nearby time-touching tests were read and are sound:
`AuthEndpointTests` asserts the returned expiry is close to real `UtcNow + 12h` with a 2-minute
tolerance (real clock throughout — correct), and `AuthorizationPolicyTests` reads back
`SlidingExpiration`/`ExpireTimeSpan` from the options object without simulating time at all.
`PMS.Application.Tests` uses its own `FixedClock` against services that have no cookie handler in
play.

**Stale claims corrected above, not deleted:** the F-2 and F-3 table rows and their log entries'
test-result sections now carry dated corrections recording that their `dotnet test` totals were
genuine when run but ceased to reproduce on 2026-09-01 20:00 UTC. The original numbers are left in
place for the record; the log stays append-only.

**Flagged for review — not decided here:**

- **This is the same class of defect as F-3's "in-memory versus Kestrel false green"**: a test that
  reported the right answer for a reason that did not hold. Two instances in three features suggests
  it is worth a standing check during verification — *why* is this test green, not just *is* it.
- **The fixture's start instant is now 2020**, which is intentional (see item 3) but will look wrong
  to a reader who does not read the comment. Called out so it is not "tidied up" back to a
  present-day date, which would restore the latent bug.
- **F-2 and F-3 are both still `Awaiting verification` and both already merged to `main`** — the
  out-of-band-merge pattern already recorded twice. This bug is a concrete example of what that
  sequencing costs: a defect in F-2's test suite reached `main` and was inherited by F-3's baseline
  before either feature's gate had run.

Committed on `fix/session-expiry-clock`. Not merged, not pushed, worktree not removed.

---

### 2026-09-04 — F-4 doctor-configured settings (gender list, vitals reasons, plausibility ranges)

**Status: `Awaiting verification`.** Handed to verification-pms. Branch left for review, not merged,
not pushed, worktree not removed.

- Got an isolated worktree from `worktree-pms` at
  `C:\Users\NileshMalviya\source\repos\f-4-clinic-settings`, branch `feature/f-4-clinic-settings`,
  cut from `main` at `172e946`. Confirmed distinct from the main working tree before writing
  anything, and confirmed `doc/planning-pms-verification.md` in the worktree is identical to the
  committed plan on `main`.

**Built against the plan's stated assumptions, both of which are still open questions:**

- **Q-9 (gender values).** Seeded `Female`, `Male`, `Other`, `Not stated`, doctor-editable, and
  **"Not stated" can never be removed or deactivated** (E-23). The seed is data in the migration,
  not logic: no code branches on any gender value.
- **Q-10 / Q-2 (units, thresholds, reasons).** Temperature unit comes from F-3's `ClinicProfile`
  (E-24); BP is always mmHg; **plausibility thresholds are empty by default**, entered by the
  physician, and a blank threshold fires nothing. Warnings are **soft — confirm and continue,
  never a block** (E-12). Vitals not-recorded reasons seeded `Equipment unavailable`,
  `Patient declined`, `Not clinically indicated`, `Other`, doctor-editable.
- **This feature authors no clinical range.** The only numeric constants it carries are storage and
  list limits, and a unit test asserts by reflection that the service holds nothing else — if
  someone adds `const decimal NormalTemperatureHigh`, that test fails.

---

#### Dependency flag — F-3 is not `Built & Verified`, and F-4 was built on it anyway

Stated explicitly rather than absorbed, and it is the third occurrence of the same pattern.
**F-3 is on `main` at `990ec19` but its tracker status is still `Awaiting verification`**; F-2 and
F-1 are the same story. F-4 depends on F-3 in the plan's dependency map, and the dependency was
treated as satisfied on the evidence that F-3's code is on `main` and demonstrably working — I
confirmed that myself rather than assuming it: `20260901163456_AddClinicProfile` applies to a fresh
database, `PUT /api/clinic-profile` answers 200 live, and F-4's temperature-unit read through
`IClinicProfileService` returns `°C`/`°F` from a real saved profile. **That is not the same as F-3
having passed its gate, and only verification-pms can say it has.**

#### Carried-in commit — the F-2 `SessionExpiryTests` fix, which is not F-4 work

`main` at `172e946` still fails 3 tests on every run: F-2's `SessionExpiryTests` clock defect,
already root-caused and fixed on `fix/session-expiry-clock` (`226e96b`), **which has never been
merged**. Building F-4 on that baseline would have meant reporting a red suite with an unrelated
cause, so the fix was carried in as its own commit (`9e45c81`), ahead of the F-4 commit and clearly
labelled. **Only the test file was taken** — not that commit's `doc/implementation-progress.md`
changes, which are already on `main` — so the F-4 commit stays clean. Reproduced both states here:
`main` alone gives `Failed: 3, Passed: 106`; with the fix, `Failed: 0, Passed: 110`. If
`fix/session-expiry-clock` is merged separately it is identical content and will not conflict.

---

**Built (backend):**

- `PMS.Domain`: `Enums/SettingCategory.cs`, `Enums/VitalMetric.cs`, `Entities/SettingOption.cs`,
  `Entities/VitalRangeSetting.cs` — per plan section 4.
- `PMS.Application`: `Abstractions/IClinicSettingsService.cs`,
  `Abstractions/IClinicSettingsRepository.cs`; `Dtos/Clinic/SettingOptionResponse.cs`,
  `SettingOptionListRequest.cs`, `VitalRangeResponse.cs`, `VitalRangeListRequest.cs`,
  `VitalWarning.cs`; `Services/ClinicSettingsService.cs`, `Services/VitalRangeEvaluator.cs`,
  `Services/VitalMetrics.cs`; registered in `DependencyInjection.cs`.
- `PMS.Infrastructure`: `Persistence/Configurations/SettingOptionConfiguration.cs` (unique index on
  `(Category, Value)`, blank-value check constraint, the `HasData` seed),
  `Persistence/Configurations/VitalRangeSettingConfiguration.cs` (`decimal(6,2)` nullable bounds,
  unique metric index, `CK_VitalRangeSetting_LowNotAboveHigh`),
  `Persistence/Repositories/ClinicSettingsRepository.cs`, two `DbSet`s on `PmsDbContext`, and
  migration **`20260904050020_AddClinicSettings`** — the name the plan specifies.
- `PMS.Api`: `Controllers/ClinicSettingsController.cs` — the four routes exactly as the plan's table
  specifies. Depends on `IClinicSettingsService`, never on `PmsDbContext`. **There is no DELETE
  route, and its absence is the feature.**

**Four backend decisions worth naming:**

1. **Omitting an option retires it; nothing is ever deleted.** `Patient.Gender` is a `string` in
   plan section 4, not a foreign key — deliberately, so a 2026 consultation still reads as it was
   recorded. A deleted row would therefore leave old records showing a value that appears nowhere
   in settings. The PUT turns an omission into `IsActive = false`, and there is no delete path on
   the service or the controller at all.
2. **One repository for two tables.** Unlike F-2/F-3's one-per-aggregate split, because a list edit
   is a mixed batch of inserts, reorders and retirements that must land together — one
   `SaveChangesAsync`, one transaction. A half-applied reorder would leave two options in one slot.
   Asserted by a test that pins `SaveCount == 1`.
3. **A list may not be emptied of active options.** `Gender / Not stated` is protected outright
   (E-23). The vitals reason list is protected as a whole rather than per value, because F-11's
   mandatory-or-reason escape hatch (REC-3, E-18) reads it — emptied, that escape hatch becomes the
   dead end it exists to remove, and a dead end is what makes someone write a BP from memory.
4. **Categories and metrics travel as names, not numbers.** The plan's own routes are
   `?category=Gender` and `/options/{category}`, so a payload answering `1` would be two
   vocabularies for one concept. The columns stay `int` for F-3's stated reason.

**Built (frontend):**

- `features/clinic/ClinicSettingsPage.tsx` (route `/settings/options`),
  `features/clinic/VitalRangesPage.tsx` (route `/settings/vitals-ranges`),
  `features/clinic/useClinicSettings.ts` (`useSettingOptions(category)`, `useVitalRanges()`, plus
  the two save hooks), `features/clinic/types/clinicSettings.ts`; `clinicApi.ts` extended with
  `getOptions` / `saveOptions` / `getVitalRanges` / `saveVitalRanges` as the plan names them.
- `routes.tsx` and `REGISTERED_PATHS` gain both paths; `AppLayout.tsx` gains both nav links; the
  new CSS is appended to `index.css`.

**Data integrity check (F-4 section 5) — the mechanism, not a mention.**
The duplicate risk C-20 names (free-text gender producing `M`/`Male`/`male` in one column) is closed
in three places that do not depend on each other: the service rejects a case-insensitive duplicate
with a field-keyed 400; the unique index on `(Category, Value)` rejects it at the database under the
default case-insensitive collation; and the editor catches it before a request is sent. The orphan
risk is closed by never deleting — proven at the database in the live smoke, where a retired `Other`
is still row id 3 with `IsActive = 0`. A test inserts a duplicate straight through the `DbContext`,
bypassing the service entirely, and asserts the database still refuses it — because SSMS is a stated
tool of this stack and the service is not the only writer.

**Test results — run in the worktree after deleting every `bin/` and `obj/`, real output:**

| Command (run in the worktree) | Result |
|---|---|
| `dotnet build PMS/backend/PMS.sln` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `dotnet test PMS/backend/PMS.sln` | `PMS.Application.Tests` **Failed: 0, Passed: 133, Skipped: 0**; `PMS.Api.IntegrationTests` **Failed: 0, Passed: 110, Skipped: 0** |
| `npx vitest run` in `PMS/frontend` | **15 files, 174 passed, 0 failed, 0 skipped** |
| `npm run build` in `PMS/frontend` | Succeeded — 106 modules, emitted into `PMS/backend/src/PMS.Api/wwwroot` |
| `tsc --noEmit` on `PMS.E2E` | **exit 0**, no diagnostics; `playwright test --list` enumerates **93 tests in 4 files** |
| `dotnet ef migrations has-pending-model-changes` | **"No changes have been made to the model since the last migration"** |

**Total: 417 automated tests passing (243 .NET + 174 Vitest), 0 skipped, 0 failed** — up from 316.
F-4 contributes 50 backend unit, 23 backend integration and 26 frontend unit tests. Counts read,
not exit codes: this host is known to print `Passed!` on a zero-test project.

New test files at the plan's named targets:
`PMS.Application.Tests/Services/ClinicSettingsServiceTests.cs`,
`PMS.Api.IntegrationTests/Endpoints/ClinicSettingsEndpointTests.cs` (which also holds
`ClinicSettingsSeedTests`), `frontend/src/features/clinic/VitalRangesPage.test.tsx`, plus
`frontend/src/features/clinic/ClinicSettingsPage.test.tsx` and
`PMS.Application.Tests/TestDoubles/FakeClinicSettingsRepository.cs` which the plan implies.
`App.test.tsx` and `TestDoubles/StubClinicProfileService.cs` were extended, not weakened.

**Three defects found by these tests during the build, all fixed:**

1. `An_omitted_option_is_retired_rather_than_deleted` used `Single(o => o.Value == "Other")` — and
   `Other` is legitimately a value in **both** seeded lists. The test now filters by category and
   additionally asserts that editing the gender list leaves the vitals reason list untouched.
2. `No_clinical_range_is_compiled_into_the_settings_code` filtered on `FieldInfo.IsLiteral`, which
   **silently skips every `const decimal`** — the compiler emits those as `static readonly`. That is
   precisely the type a clinical threshold would be written as, so the guard had a hole exactly
   where it mattered. Now `IsLiteral || IsInitOnly`.
3. `ClinicSettingsPage.test.tsx` waited on `findByRole('heading')`, which the *loading* branch also
   renders — so every assertion after it raced against the fetch. Now keyed to the labelled
   `region`, which only exists once data has arrived.

**Live smoke against a running instance** (`ASPNETCORE_URLS=http://localhost:5099`, throwaway
LocalDB `PMSDb_F4Smoke`, dropped afterwards; run on its own port so the long-running dev instance
on this host was not touched):

| Check | Result |
|---|---|
| `dotnet ef database update` on a fresh database | applied all four migrations including `20260904050020_AddClinicSettings` |
| `SELECT ... FROM SettingOption` | **8 seeded rows**, gender 1-4 and reasons 1-4, all active |
| `SELECT COUNT(*) FROM VitalRangeSetting` | **0** — nothing is seeded, so nothing can warn (AC 3) |
| `GET options` / `GET vital-ranges` with no cookie | **401** both — default-deny still holds |
| `GET /api/clinic-settings/options?category=Gender` | 200, four options in `DisplayOrder`, `Not stated` flagged `isProtected` |
| `GET ...?category=Bloodtype` | **400** naming the categories that exist — not an empty 200 |
| `PUT` dropping `Not stated` | **400**, and a re-read shows the list **completely unchanged** |
| `PUT` with `Male` + `male` | **400** keyed `Items[1].Value` (C-20) |
| `PUT` reorder + retire `Other` + add `Non-binary` | 200; dropdown view returns the four active ones in the new order |
| `SELECT ... FROM SettingOption` after that | `Other` is **still row id 3**, `IsActive = 0` — retired, not deleted |
| `PUT vital-ranges` upper-only | 200; **`SELECT` shows `WarnLow = NULL`, `WarnHigh = 42.00`** — never `0` |
| `PUT` inverted range / unknown metric | **400** both |
| Clearing a threshold | 200, both bounds back to `null` — silent again |
| E-24 end to end | with the clinic on Celsius the temperature threshold reports `unit: "°C"`; switched to Fahrenheit, `"°F"`; with no profile at all, `null` rather than a guess |
| F-1/F-2/F-3 regressions | `health=200 health-db=200 unmatched-api=404 clinic-profile=404 root=200 deep-route=200` — all unchanged |

**Acceptance criteria — walked line by line:**

1. *The gender dropdown in F-5 renders exactly the active `SettingOption` rows, in `DisplayOrder`* —
   **met to the extent F-4 can meet it, and honestly bounded.** F-5 does not exist, so what F-4
   ships is the mechanism it will consume: `GET /api/clinic-settings/options?category=Gender`
   returns active-only **by default**, ordered by `DisplayOrder`, and `useSettingOptions(category)`
   defaults the same way — a future feature that forgets the parameter gets the safe answer rather
   than a dropdown quietly offering a retired option. Proven live and by test. **Re-check at F-5.**
2. *Deactivating a gender option leaves existing patient records displaying their stored value
   unchanged* — **met by construction, and only partly observable today.** No patient table exists
   yet, so the guarantee is structural: the option row is never deleted (asserted at the service, at
   the API, and in SQL in the smoke above), and `Patient.Gender` is a string per plan section 4 so
   it does not point at this table at all. **The observable half must be re-checked at F-5.**
3. *With all `VitalRangeSetting` rows blank, entering any numeric vital produces no warning* —
   **met.** The table ships **empty**, asserted directly against a pristine migrated database by
   `ClinicSettingsSeedTests` and in the smoke. `EvaluateVitalsAsync` returns nothing for the
   brainstorm's own implausible readings (45 °C, pulse 300, BP 400/0) until a threshold exists.
4. *Setting a threshold then entering a value outside it produces a warning that can be confirmed
   and saved — never a block* — **met on F-4's side; the confirm-and-save half is F-11's and is
   named as such.** The evaluator returns warnings and **cannot** refuse: a test asserts it does not
   throw at any distance outside the range, and the message names the physician's own bound and ends
   by offering to save as entered. There is no consultation page yet to click "confirm" on — that is
   F-11, whose plan entry already owns the E-12 E2E assertion.
5. *No range value is present in source code; all come from the database* — **met, and asserted
   mechanically rather than by inspection.** A reflection test pins the service's public constants
   to exactly four storage/list limits. The migration seeds **zero** vital ranges. `VitalMetric` and
   `VitalMetrics` carry names, labels and units only.

**Assumptions and judgement calls recorded inline in the code:**

- `VitalMetric` has **four** members, splitting BP into systolic and diastolic — the plan names the
  enum but not its members, and E-12's own example (`400/0`) needs two bounds to express. Marked
  `ASSUMPTION` in `VitalMetric.cs`.
- `GET options` takes an **`includeInactive`** flag the plan's route table does not show. Without it
  the editor could never re-activate a retired option; the default is the safe one. Marked
  `ASSUMPTION` in `SettingOptionResponse.cs` and the service interface.
- **Categories and metrics serialize as names.** Marked `ASSUMPTION` in `SettingOptionResponse.cs`.
  Global JSON options were deliberately **not** changed to a string enum converter — that would have
  altered F-3's committed `TemperatureUnit` wire shape.
- `SettingOptionResponse.IsProtected` is sent so the client can render a row without a control the
  server would refuse. The server refuses it regardless.
- **`decimal(6,2)`** for thresholds, with the service rejecting anything that would not survive the
  column rather than letting SQL Server round it silently. Named a **storage** limit in three places
  so it is not mistaken for a clinical bound.
- **A value exactly on a bound does not warn** — "warn above 40" means 40 is acceptable.
- One file the plan's section 3 tree does not list: `features/clinic/useClinicSettings.ts` sits
  beside `useClinicProfile.ts` rather than in a new feature folder. Same class as F-1's
  `PMS.Application/Exceptions/`; noted for `code-review-pms`.

**Known environment limitation — E2E written, not proven (unchanged from F-1, F-2, F-3):**

`PMS.E2E/specs/settings.spec.ts` is written, typechecks clean, and enumerates. **`PMS.E2E` had no
`node_modules` in a fresh worktree**; `npm install` there succeeded (6 packages) and the
partial-TypeScript problem F-2 recorded did not recur, but browser launch is still blocked on this
host. The one API-request spec that needs no browser was observed hitting the **stale long-running
dev instance from 2026-09-02**, which predates F-4 and answers 404 where F-4 answers 401 — a
reminder that an E2E pass or fail on this host says as much about which instance is listening as
about the code. Everything that spec asserts is proven above by the integration suite and by the
live smoke on a dedicated port.

**The plan's E2E line for F-4 cannot be fully written yet, and no `test.fixme` was added.** It asks
for a warning *on the consultation page* — that is **F-11**, which depends on F-10, which depends on
F-9. The settings half is covered here; the confirm-and-save half is named in the spec's header as
belonging to `vitals.spec.ts` when F-11 lands. A skip that looks like coverage is worse than a
stated gap.

**Flagged for review — not decided here:**

- **Changing the clinic's temperature unit does not convert a threshold already entered.** Found in
  the live smoke: a `WarnHigh` of 42 set under Celsius still reads 42 after switching to Fahrenheit,
  now meaning 42 °F. This mirrors F-3's existing behaviour for recorded temperatures and its
  on-screen warning, so the page now carries the matching sentence — but whether thresholds should
  be **converted**, **cleared**, or **left with a warning** on a unit change is an owner decision
  tied to **Q-10**, not a developer one.
- **Q-9 and Q-10 are still open.** Everything above is the plan's stated default. A different answer
  is a settings edit plus, for the gender seed, one migration — not a code hunt.
- **The `Not stated` protection is hardcoded** in `ClinicSettingsService.ProtectedValues`. That is
  the point (the physician must not be able to remove it), but it is the one place where a list
  value appears in code, so it is named here rather than left to be discovered.
- **React Router v6 advisories are unchanged** (GHSA-wrjc-x8rr-h8h6, GHSA-337j-9hxr-rhxg; fix in
  7.18+). F-4 grew the routing surface by two more routes. Four features have now raised this;
  section 2 should either be amended to v7.18+ or the risk accepted on the record.
- **`dotnet-ef` is installed after all** (10.0.11) — `CLAUDE.md` still lists it as a blocker under
  "Known environment gotchas". The migration was created with **`--no-build`**: the plain
  `dotnet ef migrations add` hung for over ten minutes on host build/startup contention and had to
  be killed. Worth correcting in `CLAUDE.md`, and worth knowing for every later migration.
- **F-2's committed seed credential is untouched by this feature** and remains a live exposure;
  restated only so it is not forgotten between features.

Committed on `feature/f-4-clinic-settings`. Not merged, not pushed, worktree not removed.
Next: `verification-pms`.

---

### 2026-09-07 — F-4 independent verification (verification-pms)

**Verdict: PASS. Status → `Built & Verified`**, with two items carried forward and named below.
Nothing here is taken from the builder's report; every number and every response body below is
output I produced myself in this pass, inside the worktree.

**Worktree confirmed real and current.** `git worktree list` shows
`C:\Users\NileshMalviya\source\repos\f-4-clinic-settings` on `feature/f-4-clinic-settings` at
`9844075`, working tree clean before and after my runs. `git diff --stat main -- doc/planning-pms-verification.md doc/brainstorm-pms-verification.md BRD/Doc_BRD.md`
is **empty**, so I verified against the same committed plan, brainstorm and BRD that `main` holds.
The branch's second commit `9e45c81` is the carried-in `fix/session-expiry-clock` fix; I confirmed
by `git show --stat` that it touches **exactly one file**, `SessionExpiryTests.cs` — test fixture
only, no application code, as claimed.

**What I re-ran, and the actual output:**

| Command (run by verification-pms, in the worktree) | Result |
|---|---|
| `dotnet build PMS.sln` after deleting every `bin/` and `obj/` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `dotnet test PMS.sln` | `PMS.Application.Tests` **Failed: 0, Passed: 133, Skipped: 0**; `PMS.Api.IntegrationTests` **Failed: 0, Passed: 110, Skipped: 0** |
| `npm test` (`vitest run`) in `PMS/frontend` | **15 files, 174 passed, 0 failed, 0 skipped** |
| `npm run build` | Succeeded — 106 modules emitted into `PMS/backend/src/PMS.Api/wwwroot` |
| `npx playwright test --project=chromium` | Browser launch denied by the host (see carried item 1) |

**Total re-run and passing: 417 automated tests (243 .NET + 174 Vitest), 0 skipped, 0 failed** —
matching the builder's claim exactly, but on my own run.

**Green-checkmark checks.** Counts read, not exit codes. No zero-test project. `grep` for
`Skip =`, `[Ignore]`, `it.skip`, `test.skip`, `describe.skip`, `it.todo` and `.only(` over the
backend tests and the whole frontend `src/` returns **nothing**. No `NoWarn`,
`TreatWarningsAsErrors` or `WarningsAsErrors` in any `.csproj` or `.props`, so the 0-warning build
is genuine and not suppressed. The three `fixme` hits in the E2E folder are all prose in comments
explaining why a `test.fixme` was *not* used — no live skip anywhere.

**Live smoke, run by me** against a throwaway `PMSDb_F4Verify` (created, migrated with the real
`dotnet ef`, exercised under Kestrel on port 7291, then dropped):

| Check | Result |
|---|---|
| `dotnet ef database update` | all four migrations applied, ending `20260904050020_AddClinicSettings` |
| `SELECT ... FROM SettingOption` | exactly the 8 seeded rows: Gender `Female/Male/Other/Not stated` order 1–4, VitalsNotRecordedReason `Equipment unavailable/Patient declined/Not clinically indicated/Other` order 1–4, all active |
| `SELECT COUNT(*) FROM VitalRangeSetting` | **0** — ships empty, so "no threshold means no warning" is true by construction |
| `sys.check_constraints` | `CK_SettingOption_ValueNotBlank` and `CK_VitalRangeSetting_LowNotAboveHigh` both really in the schema |
| `GET /api/clinic-settings/options?category=Gender` with no cookie | **401** |
| Same, signed in | **200**, 4 active rows in `DisplayOrder`, `"Not stated"` carrying `isProtected: true` |
| `GET .../options?category=Nope` | **400** `problem+json` naming the two real categories — not an empty 200 |
| `PUT .../options/Gender` reordering, adding `Non-binary`, omitting `Other` | **200**; DB shows `Other` **retired (`IsActive=0`), not deleted**, pushed to the end of the order |
| Same PUT resubmitting `"female"` in lower case | updates row id 1 and leaves the **stored spelling `Female` untouched** — C-20 holding |
| `PUT` omitting `Not stated` / deactivating `Not stated` | **400** both (E-23) |
| `DELETE .../options/Gender/1` | **404** — there is no delete route at all |
| `PUT .../vital-ranges` temp high 40, pulse blank | **200**; DB holds `WarnHigh=40.00`, `WarnLow=NULL`, pulse **NULL/NULL — not 0** |
| `PUT` inverted range (low 120, high 40) | **400**, field-keyed on `Items[0].WarnLow` |
| `PUT` unknown metric `Weight` | **400** — dropped on the floor is not an option |
| `PUT` clearing the threshold back to blank | **200**, column returns to `NULL` |

**Acceptance criteria — checked against code and live output, not the builder's report:**

1. *The gender dropdown in F-5 renders exactly the active `SettingOption` rows, in `DisplayOrder`* —
   **met, and proven through F-5** (which is what makes it observable). `useSettingOptions('Gender')`
   defaults `includeInactive = false` and the server orders by `DisplayOrder`; my live `GET` after
   retiring `Male` returned `Female, Other, Not stated` in order and nothing else.
2. *Deactivating a gender option leaves existing patient records displaying their stored value
   unchanged* — **met, proven live end-to-end on the F-5 branch.** I registered a patient with
   gender `Male`, retired `Male` from the list, then re-fetched the patient: `"gender":"Male"`,
   unchanged. A **new** registration with `Male` is then refused with a 400. That is exactly the
   intended asymmetry — history keeps its value, new records cannot use a retired one.
3. *With all `VitalRangeSetting` rows blank, entering any numeric vital produces no warning* —
   **met by construction**, not by a code path: the migration seeds **no** threshold row, and
   `VitalRangeEvaluator` `continue`s on any metric with no row. Asserted by
   `With_nothing_configured_no_value_produces_a_warning` and
   `A_blank_bound_stays_silent_while_the_other_bound_fires`, and I confirmed the empty table live.
4. *Setting a threshold then entering a value outside it produces a warning that can be confirmed
   and saved — never a block* — **met on F-4's side; the confirm-and-save half is not buildable
   yet.** `EvaluateVitalsAsync` returns a `IReadOnlyList<VitalWarning>` and has no path that throws
   or refuses — `Evaluating_never_throws_however_far_outside_the_value_is` pins that, and
   `A_value_exactly_on_the_bound_does_not_warn` pins the boundary. The confirm dialog lives on the
   consultation page, which is **F-11**, which depends on F-10 → F-9, none of which exist. The
   builder stated that boundary in `settings.spec.ts` rather than hiding it behind a `test.fixme`,
   which is the right call given F-1's skipped spec had to be chased down later. **Recorded as
   carried to F-11, not waved through.**
5. *No range value is present in source code; all come from the database* — **met, and enforced.**
   `No_clinical_range_is_compiled_into_the_settings_code` reflects over
   `ClinicSettingsService`'s public static fields and fails if anything appears beyond the four
   named storage/list limits — and it correctly matches on `IsLiteral || IsInitOnly`, because a
   `const decimal` is emitted as `static readonly` and an `IsLiteral`-only filter would have
   skipped exactly the type a clinical threshold would be written as. `VitalMetrics` carries labels
   and units only. The seed contains no threshold.

**Data-integrity and architecture spot-check — mechanism present, not just mentioned:**

- **C-20 is closed three independent ways**, and I exercised all three: the service rejects a
  case-insensitive duplicate before insert; `IX_SettingOption_Category_Value` is a real unique
  index in the shipped schema under the default (case-insensitive) collation; and the editor
  catches it client-side (`catches a case-insensitive duplicate before it is ever sent`).
- **Retire, never delete, is structural rather than careful.** There is no `DELETE` route
  (my live call → 404), no delete method on `IClinicSettingsService`, and no `Remove` on the
  repository. Omission from the PUT sets `IsActive = false`. The whole edit is one
  `SaveChangesAsync`, so a reorder cannot half-apply.
- **Blank ≠ zero survives the entire path** — nullable columns, nullable DTO fields, a frontend
  that converts an empty input to `null` (`submits a blank threshold as null, not zero`) and a
  test that a threshold of **0 is a real threshold** (`accepts a threshold of zero as a real
  threshold`). Confirmed at the column by me.
- **Layering as specified.** `ClinicSettingsController` depends on `IClinicSettingsService` only —
  the only `PmsDbContext` mention anywhere in `PMS.Api/Controllers` is inside a comment.
  `ClinicSettingsService` reads the temperature unit through F-3's `IClinicProfileService` rather
  than the profile table, so one type knows how the profile is stored. Responses are
  `PMS.Application/Dtos/Clinic/` records; no EF entity crosses the wire. Frontend is
  folder-per-feature under `features/clinic/` with the plan's named files and hooks, and both
  plan-named routes (`/settings/options`, `/settings/vitals-ranges`) are registered.
- **API surface matches the plan's route table exactly** — four routes, same verbs, same paths,
  same status codes, cookie auth by F-2's default-deny fallback with no `[AllowAnonymous]`.

**Carried item 1 — the Playwright harness is still unproven, confirmed by me, not accepted from
the report.** I ran the suite myself; every browser-dependent spec fails with
`browserType.launch: spawn EPERM`. This is the host process-spawn denial recorded since F-1, not a
defect in F-4's code, and no change `implementation-pms` could make would fix it — which is why it
is not routed back as rework. **The F-1 deadline stands: the harness must work before F-14.**

**Carried item 2 — F-4's AC-1 and AC-2 are not verifiable inside F-4.** Both describe behaviour in
a patient record, which F-4 does not have. I verified them on the F-5 branch instead (above) rather
than marking them "met by inspection". Noted so the next reader knows where that evidence came from.

**BRD cross-check (the user's explicit ask).** `BRD/Doc_BRD.md` names `Gender` as a captured
patient field (L91) and `Temperature / Blood Pressure / Pulse` as mandatory vitals (L110–113). It
states **no** requirement for a settings screen and **no** clinical range. F-4 is therefore an
enabler for those two BRD lines, and its most important property against the BRD is a negative one:
it authors no clinical number, which is exactly what a BRD silent on ranges requires. Nothing in
F-4 contradicts the BRD. (`modules/01-patient-management.md` was used only as a pointer; the BRD
itself is what I read.)

**Flagged for `code-review-pms` / the plan owner — not decided here:**

- **The branch carries a commit that is not F-4 work** (`9e45c81`, the `fix/session-expiry-clock`
  fixture fix, file-only). Verified test-only and the suite is green with it, but it means F-4's
  branch and `fix/session-expiry-clock` will both want to land the same content.
- **`CLAUDE.md`'s `dotnet-ef` gotcha is stale** — I confirmed `dotnet ef --version` reports
  **10.0.11** on this host and used it to apply migrations.
- **React Router v6 advisories are unchanged and now carried from F-1 through F-4** without a
  decision. Still not reachable in this app's shape; still needs an owner ruling or a plan
  amendment to v7.18+.
- **F-3 is a dependency and is still tracker-status `Awaiting verification`** (though it is on
  `main`). F-4 passing does not advance F-3's status.

**Gate status.** F-4 is `Built & Verified` — it works, on evidence I produced. It is **not**
finished: `code-review-pms` reviews it for quality, consistency and security before `Reviewed`.
Per the dependency map this satisfies F-5's and F-11's dependency on F-4. No application code, test
code or migration was modified in this pass; `git status` in the worktree is clean at `9844075`,
and both throwaway databases I created were dropped.

---

> **Restored on 2026-09-07 while merging `main` into `feature/f-7-patient-search`.** The entry
> below is F-5's *builder* entry. It was written on `feature/f-5-patient-registration`, it is the
> entry that F-5's verification entry (immediately after this one) refers to as "the builder's full
> log entry for F-5 lives on the branch's copy of this file and lands on merge" — and it did **not**
> land: the copy of this file that reached `main` through PR #5 does not contain it, so the merge
> presented it as a deletion. This log is append-only, so it is put back verbatim from `e1bf206`
> rather than allowed to disappear. Nothing in it was edited.

### 2026-09-07 — F-5 patient registration & profile

**Status: `Awaiting verification`.** Handed to verification-pms. Branch left for review, not merged,
not pushed, worktree not removed.

- Got an isolated worktree from `worktree-pms` at
  `C:\Users\NileshMalviya\source\repos\f-5-patient-registration`, branch
  `feature/f-5-patient-registration`. Confirmed distinct from the main working tree before writing
  anything, and confirmed `doc/planning-pms-verification.md` and
  `doc/brainstorm-pms-verification.md` are byte-identical to `main`'s committed copies
  (`git diff --stat main -- <both>` empty), so this was built against the current committed plan.

#### DECISION 1 — this worktree was cut from `feature/f-4-clinic-settings`, not from `main`

**This is a builder's judgement call and it should be reviewed as one. It was not instructed by the
user, and it changes the merge order.**

The task described F-1, F-2 and F-4 as "all on `main`". That is true of F-1 and F-2 and **false of
F-4**: `git merge-base --is-ancestor 9844075 main` fails, and `main` is at `172e946`. F-4 exists
only on `feature/f-4-clinic-settings`. Plan F-5 §1 makes F-5's dependency on it concrete —
*"**Assumption (Q-9):** gender values come from F-4"* — so this had to be resolved rather than
assumed past.

`AskUserQuestion` is **not available inside a subagent** (it returns
`No such tool available: AskUserQuestion`), so the question could not be put to the user mid-run.
The three options and the reasoning:

| Option | Rejected / chosen | Why |
|---|---|---|
| Cut from `feature/f-4-clinic-settings` (`9844075`) | **Chosen** | The only option that satisfies the plan's stated dependency without weakening it. |
| Cut from `main`, hardcode a gender list | Rejected | Reopens **C-20**, the exact risk F-4 §5 exists to close — free-text/hardcoded gender producing `M`/`Male`/`male` in one column. Would need rework when F-4 merges. |
| Merge F-4 to `main` first | Rejected | `implementation-pms` must never merge, and F-4 has not passed `verification-pms`. Choosing this would have repeated the out-of-band-merge pattern already recorded twice against F-1 and F-2. |

**Consequences, stated plainly so nobody is surprised at merge time:**

- The F-5 branch **contains F-4's commits**. `git diff main...feature/f-5-patient-registration`
  shows F-4's files as well as F-5's.
- **F-4 must be merged before, or together with, F-5.** Merging F-5 alone would bring F-4 in
  through the back door without F-4 ever clearing its own gate.
- `verification-pms` should verify F-5 against the F-4 baseline, not the `main` baseline, and should
  be aware that the 525-test total includes F-4's tests.

#### DECISION 2 — the check constraint in plan F-5 §2 is a tautology; the shipped one is not

Plan F-5 §2 specifies: *"A DB check constraint enforces `DateOfBirth IS NOT NULL OR ApproxAgeYears
IS NOT NULL OR both NULL`"*. **That predicate is true for every possible row** — the three clauses
between them cover the entire space — so building it literally would have created a constraint that
enforces nothing while appearing in the schema as though it protected E-9.

Built instead is the rule the same section's prose and E-9 actually describe, as
`CK_Patient_AgeShape`:

```
([DateOfBirth] IS NULL OR [ApproxAgeYears] IS NULL)
AND ( ([ApproxAgeYears] IS NULL AND [AgeRecordedOn] IS NULL)
   OR ([ApproxAgeYears] IS NOT NULL AND [AgeRecordedOn] IS NOT NULL) )
```

— i.e. never both kinds of age at once, and an approximate age never without the date it was taken.
**Flagged here for the plan owner rather than silently substituted**, and pinned by two integration
tests that insert through raw SQL (bypassing the service) so they prove the constraint is really in
the shipped schema rather than proving the service is careful.

**Built (backend):**

- `PMS.Domain`: `Entities/Patient.cs`, `Enums/PatientStatus.cs` (no `Deleted` member — E-33).
- `PMS.Application`: `Abstractions/IPatientRepository.cs`, `Abstractions/IPatientService.cs`;
  `Dtos/Patients/CreatePatientRequest.cs`, `PatientResponse.cs`, `PatientDetailResponse.cs`;
  `Services/PatientService.cs`, `Services/PatientNormalizer.cs`,
  `Services/PatientAgeFormatter.cs`; registered in `DependencyInjection.cs`.
- `PMS.Infrastructure`: `Persistence/Configurations/PatientConfiguration.cs`,
  `Persistence/Repositories/PatientRepository.cs`, `Patients` DbSet,
  `Migrations/20260907073152_AddPatient.cs` — the migration name the plan specifies, generated with
  the real `dotnet ef` tool.
- `PMS.Api`: `Controllers/PatientsController.cs`. Depends on `IPatientService`, never on
  `PmsDbContext`. No `[AllowAnonymous]` — F-2's default-deny fallback policy protects both routes
  by omission, verified live as a 401.

**Environment note — the `dotnet-ef` gotcha in `CLAUDE.md` is now stale.** It records the global
tool as "still not installed". `dotnet ef --version` reports **10.0.11** on this host, and the
`AddPatient` migration was generated and applied with it. `CLAUDE.md` should be updated.

**Four decisions worth naming:**

1. **The age rule is enforced twice — service *and* database check constraint.** Every other
   F-5 rule inconveniences someone; this one destroys information. An age stored without the date
   it was taken cannot be repaired by any later migration, because the missing fact was never
   written down. F-5 will not be the last writer of this table.
2. **`AgeDisplay` is formatted server-side and shipped in the DTO.** The alternative is React and
   F-14's PDF renderer each formatting an age, and the first time they disagree — "~40" on screen
   against "40" on the printed prescription — the printed document is the one the patient walks out
   with and nobody can correct.
3. **Create idempotency rests on a filtered unique index, not on a read-before-write.** Two clicks
   20 ms apart can both read nothing and both insert; only a database constraint decides at that
   point. The filter (`WHERE [SubmissionId] IS NOT NULL`) is load-bearing, not tidiness — SQL
   Server's plain unique index treats NULLs as equal and would permit exactly **one** patient
   without a token in the entire table. Both halves proven live below.
4. **A lost race returns the winner, not a 409.** The physician clicked Save twice and one patient
   was registered — which is what they wanted. Reporting that as a conflict would tell someone
   whose registration succeeded that it failed, and invite a third attempt.

**Built (frontend):**

- `features/patients/`: `PatientForm.tsx` (route `/patients/new`), `PatientProfile.tsx`
  (route `/patients/:id`, replacing F-1's placeholder), `patientsApi.ts`
  (`createPatient`/`getPatient`), `usePatients.ts` (`useCreatePatient`/`usePatient`),
  `types/patient.ts`.
- `shared/hooks/useSubmitOnce.ts` — the plan's named target, new in this feature.
- `routes.tsx` mounts both screens and adds `/patients/new` to `REGISTERED_PATHS`.
- `test/testUtils.tsx` gains `aPatientDetail()`.

**Two frontend decisions worth naming:**

- **Age is a three-way radio choice, not two optional boxes.** DOB and approximate age are mutually
  exclusive on the server (E-9), so two free-standing inputs would let the physician fill in both
  and be told off for it. Choosing a mode hides the other input entirely, so the invalid
  combination cannot be expressed rather than being validated after the fact.
- **Gender is a `<select>` fed by F-4's active options.** A text input here is precisely how
  `M`/`Male`/`male` end up in one column (C-20). Retired options are excluded, because a retired
  value must not be selectable for a *new* record even though existing records keep displaying it.

**Data integrity check (F-5 §5) — the mechanisms, not mentions.** F-5 owns the *normalisation* half
of the duplicate problem and F-6 adds detection. `NormalizedName` collapses **all Unicode**
whitespace (a pasted non-breaking space is the invisible near-duplicate of E-60) and case-folds
invariantly, while never transliterating — folding a Devanagari name to ASCII would collapse
genuinely different people onto one key, which is worse than the duplicate it prevents (E-57).
`NormalizedPhone` is digits-only and **null rather than empty** when absent, so phone-less patients
do not all share one blank key and get offered to each other as duplicates by F-6 (E-59). Both are
written by `PatientService` only; the client cannot set them, because the DTO has no property for
them. Mutable-history risk is closed by the two-place age rule (E-9). Double-submit is closed by
the filtered unique index (E-43, E-46).

**Test results — clean build (every `bin/` and `obj/` deleted first), real output:**

| Command (run in the worktree) | Result |
|---|---|
| `dotnet build PMS/backend/PMS.sln` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `dotnet test PMS/backend/PMS.sln` | `PMS.Application.Tests` **Failed: 0, Passed: 179, Skipped: 0**; `PMS.Api.IntegrationTests` **Failed: 0, Passed: 136, Skipped: 0** |
| `npm test` (`vitest run`) in `PMS/frontend` | **18 files, 210 passed, 0 failed, 0 skipped** |
| `npm run build` in `PMS/frontend` | Succeeded — 112 modules, emitted into `PMS/backend/src/PMS.Api/wwwroot` |
| `tsc --noEmit -p tsconfig.json` on `PMS.E2E` | **exit 0**, no diagnostics |

**Total: 525 automated tests passing (315 .NET + 210 Vitest), 0 failed, 0 skipped** — up from F-4's
417. Counts read, not exit codes; this host is known to print `Passed!` on a zero-test project.

New test files at the plan's named targets, plus three the plan implies:
`PMS.Application.Tests/Services/PatientServiceTests.cs`,
`PMS.Application.Tests/TestDoubles/FakePatientRepository.cs`,
`PMS.Api.IntegrationTests/Endpoints/PatientsEndpointTests.cs`,
`frontend/src/features/patients/PatientForm.test.tsx`,
`frontend/src/features/patients/PatientProfile.test.tsx`,
`frontend/src/shared/hooks/useSubmitOnce.test.ts`,
`PMS.E2E/specs/patient-registration.spec.ts`.
`frontend/src/App.test.tsx` was updated, not weakened: `/patients/:id` is no longer a placeholder,
so its assertion moved from "renders the placeholder" to "renders F-5's real screen", and
`/patients/new` was added to both the route-table and the signed-out-redirect lists.

**Three of my own test expectations were wrong and were corrected — the code was right in all
three.** Recorded because a builder silently rewriting failing tests is exactly what review should
be suspicious of:

1. `+91 98765-43210` normalises to `919876543210`, not `9876543210` — the country code is digits.
   The expectation was wrong. **This surfaced a real question that belongs to F-6, now flagged:**
   `+91 98765 43210` and `098765 43210` do *not* produce the same key, so they will not match as
   duplicates. E-59 asks only for a digits-only index, which is what F-5 ships; deciding those two
   are one person is a **matching rule and F-6's to make**, and burying a guess about it in a
   normalizer would have hidden that decision.
2. `365 * 3` days is not three years once a leap day intervenes. Replaced the day-offset case with
   explicit calendar dates, including the birthday-not-yet-reached boundary.
3. Raw `ExecuteSqlRawAsync` surfaces `SqlException`, not EF's `DbUpdateException`. The assertion now
   matches on the constraint *name*, which is stronger — it proves that specific constraint refused
   the row.

**Live smoke against a throwaway database under real Kestrel** (`PMSDb_F5Smoke`, dropped
afterwards). This is where F-3 and F-4 each found a real defect, so it was not skipped:

| Check | Result |
|---|---|
| Schema after `dotnet ef database update` | `Patients` table present; `CK_Patient_AgeShape` present; `UX_Patient_SubmissionId` unique with **`has_filter = 1`** |
| `POST /api/patients` with no cookie | **401** |
| Name-only registration `"  Meera   Devi  "` | **201**, `fullName` stored as `"Meera Devi"`, `isProfileIncomplete: true`, `ageDisplay: "Age not recorded"` |
| `GET /api/patients/{id}` on that patient | **200**, `missingFields: ["phone","age","gender"]` |
| Future DOB | **400**, `errors.DateOfBirth` |
| Gender `"M"` (not on F-4's list) | **400**, `errors.Gender` — C-20 holding across the F-4/F-5 seam |
| `approxAgeYears: 40` with **no** `ageRecordedOn` sent | **201**; stored `ageRecordedOn: "2026-09-07"`, `ageDisplay: "~40 (recorded 2026)"` — E-9's pair completed server-side |
| Unknown id | **404** |
| Devanagari name round-trip | Request and response bytes **identical**; SQL Server holds UTF-16 `0930 0935 093F 0020 0915 0941 092E 093E 0930` — verified by hex, because the shell renders it as `???` |
| Same `submissionId` posted **twice sequentially** | **201** both, **same id**, **1 row** |
| Same `submissionId` posted **4x simultaneously** | **201** x4, **all the same id**, **1 row** — the index deciding a real race, not the service's read |
| Two registrations with **no** token | Both persist; 7 null-token rows coexist — the filter works |
| F-1..F-4 regressions | health, health/db, login, clinic-profile, clinic-settings all unchanged |

*Smoke-run note, not a code defect:* the first attempt appeared to return 404 for every
`/api/patients` call. The cause was a **leftover API process from an earlier feature's smoke still
holding port 7191** (PID 1280); my instance failed to bind and every request hit the stale build.
Re-run on port 7291 with `ASPNETCORE_URLS`. The stale process was left alone rather than killed, as
it was not started by this run. Worth knowing before the next live smoke on this host.

**Acceptance criteria — walked line by line:**

1. *Single-word name, no phone, saved; profile shows "Profile incomplete" and "No contact
   recorded"* — **met**, proven live (rows 3/4 above) and by
   `A_patient_with_a_name_and_nothing_else_is_saved_and_flagged_incomplete`,
   `A_name_only_registration_succeeds_and_reports_exactly_what_is_missing`, and the
   `PatientProfile` tests. The profile names each gap rather than only flagging one, and states the
   patient can still be seen — E-8's mitigation is "flag", never "prevent".
2. *Approx age stores `ApproxAgeYears` + `AgeRecordedOn`, displays `~40 (recorded 2026)`; a bare
   age is 400* — **met with one deliberate refinement, flagged.** The pair is always stored and the
   display is exact. But a bare `approxAgeYears` with no date is **completed with today's date, not
   rejected with a 400**: the client does not send the date, and the server is the honest place to
   stamp "as of today". What is rejected is the state E-9 actually forbids — an
   `ageRecordedOn` with no age (400), and a stored age with no date (impossible: service *and*
   check constraint). A 400 on the plain form submission would have made the ordinary case fail.
3. *DOB of today accepted, age in days; future DOB is 400* — **met**, both live and by test.
   Today's DOB renders `"0 days"`, which is genuinely distinct from "Age not recorded".
4. *`"  Ravi   Kumar  "` and `"Ravi Kumar"` both persist `NormalizedName = "ravi kumar"`* —
   **met**, and the stored `FullName` is also collapsed for display while case is preserved.
5. *Non-Latin name saves, displays and re-fetches unchanged* — **met**, proven at byte level
   against a real SQL Server column (above), not merely in C#.
6. *Double-clicking Save creates exactly one patient row* — **met**, and proven at the level that
   matters: four *simultaneous* requests, one row. The client half (`useSubmitOnce`) is also tested
   for the synchronous double-call a `disabled` prop cannot catch.

**Assumptions recorded inline in the code:**

- **`MissingFields` = phone, age, gender** (marked `ASSUMPTION` in `PatientService.cs`). The plan
  says a phone-less patient is "flagged incomplete" but never enumerates the complete set. Alternate
  contact is deliberately excluded — including it would leave well-filled profiles permanently
  flagged, which is how a warning becomes wallpaper. Nothing branches on the flag; it is displayed,
  never enforced. If the physician's answer to Q-7 differs, this list is the only thing that changes.
- **`SubmissionId` is optional, not required.** Omitting it forgoes the idempotency guarantee.
  A 400 for a missing token would reject a legitimate `curl` over a field the plan never named on
  this DTO; the one client that matters always sends it.
- **`PatientResponse` carries `PhoneTail`, not the full phone** (REC-12). Enough to tell two Ravi
  Kumars apart, while putting less contact detail on a screen visible from the waiting side of the
  desk. The full number is on the detail response.
- **`MaxApproxAgeYears = 150`** is a typo bound, not a clinical one, and is named as such in code —
  plan §7's clinical-rule boundary means no clinical number is authored here.

**Flagged for review — not decided here:**

- **The F-4-not-on-`main` base and its merge-order consequence** (Decision 1). This is the item most
  worth a human decision before anything is merged.
- **The plan's tautological check constraint** (Decision 2) — `planning-pms` should correct F-5 §2.
- **`CLAUDE.md`'s `dotnet-ef` gotcha is stale** — the tool is installed (10.0.11).
- **Phone-matching equivalence is an open F-6 question** — country code vs. trunk zero, above.
- **The plan's route table lists 409 for `POST /api/patients`.** That status belongs to F-6's
  duplicate-confirmation flow; F-5 ships the registration path it hangs off and returns no 409. No
  client should be written to expect one until F-6 lands.
- **React Router v6 advisories unchanged** (GHSA-wrjc-x8rr-h8h6, GHSA-337j-9hxr-rhxg; fix in 7.18+).
  F-5 grows the routing surface again — `PatientForm` now issues a programmatic `navigate()` after
  save. The target is a server-generated patient id, so the open-redirect advisory is still not
  reachable, but this has now been carried from F-1 through F-5 without a decision.
- **F-2's committed seed credential is untouched by this feature** and remains a live exposure;
  restated so it is not forgotten between features.

**Known environment limitation — E2E written, not proven (unchanged from F-1 through F-4):**

`PMS.E2E/specs/patient-registration.spec.ts` is written at the plan's named target, covers the
golden path and the plan's named E-8 case, plus E-13, E-21, E-46, E-57 and E-65, and typechecks
clean (exit 0). `npx playwright test --project=chromium` gives **8 failed, all
`browserType.launch: spawn EPERM`** — the same host process-spawn denial recorded since F-1, not a
defect in this code. **These specs are not claimed as passing.** Each one's behaviour is proven by a
suite that does run: the golden path and E-8 by `PatientProfile.test.tsx` and the endpoint tests;
E-13/E-21/E-46 by `PatientForm.test.tsx` and `PatientServiceTests`; E-57 at byte level by the live
smoke and `PatientsEndpointTests`; E-65 by `PatientForm.test.tsx`. The genuinely unproven axis
remains real-browser rendering. **The deadline recorded against F-1 stands: the harness must work
before F-14.**

Committed on `feature/f-5-patient-registration`. Not merged, not pushed, worktree not removed.
Next: `verification-pms` — and note it must verify against the **F-4 branch** baseline, not `main`.

---

### 2026-09-07 — F-5 independent verification (verification-pms)

**Verdict: PASS. Status → `Built & Verified`**, with one plan-vs-build discrepancy escalated for an
owner ruling and several items carried. Every number and response body below is output I produced
myself in this pass, inside the worktree.

**Worktree confirmed real and current.** `git worktree list` shows
`C:\Users\NileshMalviya\source\repos\f-5-patient-registration` on `feature/f-5-patient-registration`
at `e1bf206`, clean before and after my runs. `git log` confirms the branch sits **on top of F-4's
`9844075`**, which is on no other branch and **not on `main`** — so, as instructed, I verified F-5
against the F-4 baseline. `git diff --stat main -- doc/planning-pms-verification.md doc/brainstorm-pms-verification.md BRD/Doc_BRD.md`
is **empty**: same committed plan, brainstorm and BRD as `main`.

**What I re-ran, and the actual output:**

| Command (run by verification-pms, in the worktree) | Result |
|---|---|
| `dotnet build PMS.sln` after deleting every `bin/` and `obj/` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `dotnet test PMS.sln` — **run twice** | Both runs: `PMS.Application.Tests` **Failed: 0, Passed: 179, Skipped: 0**; `PMS.Api.IntegrationTests` **Failed: 0, Passed: 136, Skipped: 0** |
| `npm test` (`vitest run`) — **run twice** | Both runs: **18 files, 210 passed, 0 failed, 0 skipped** |
| `npm run build` | Succeeded — 112 modules emitted into `PMS/backend/src/PMS.Api/wwwroot` |
| `tsc --noEmit -p tsconfig.json` on `PMS.E2E` | **exit 0** |
| `npx playwright test --project=chromium specs/patient-registration.spec.ts` | **8 failed, all `browserType.launch: spawn EPERM`** |

**Total re-run and passing: 525 automated tests (315 .NET + 210 Vitest), 0 skipped, 0 failed.**
Each suite run twice with identical results — **no flake observed.** Counts read, not exit codes.
This total **includes F-4's tests**, because the branch contains F-4.

**Live smoke, run by me** against a throwaway `PMSDb_F5Verify` (created, migrated with real
`dotnet ef`, exercised under Kestrel on port 7391, then dropped):

| Check | Result |
|---|---|
| `POST /api/patients` with no cookie | **401** |
| Name-only `"  Meera   Devi  "` | **201**, stored `"Meera Devi"`, `isProfileIncomplete: true`, `ageDisplay: "Age not recorded"` |
| Mononym `"Prem"` | **201** — no surname is ever asked for (E-13) |
| `GET /api/patients/{id}` on the name-only patient | **200**, `missingFields: ["phone","age","gender"]` |
| `approxAgeYears: 40`, no `ageRecordedOn` | **201**, `ageDisplay: "~40 (recorded 2026)"` — see the discrepancy below |
| `ageRecordedOn` with no age | **400**, `errors.ApproxAgeYears` |
| `dateOfBirth` = today | **201**, `ageDisplay: "0 days"` — genuinely distinct from "Age not recorded" |
| `dateOfBirth` in the future | **400**, `errors.DateOfBirth` |
| `"  Ravi   Kumar  "` and `"Ravi Kumar"` | both persist `FullName = "Ravi Kumar"`, `NormalizedName = "ravi kumar"` |
| Devanagari name (sent as a UTF-8 body file) | **201**; SQL Server holds `0930 0935 093F 0020 0915 0941 092E 093E 0930`, verified by `CONVERT(varbinary)`; the `GET` response bytes are `E0 A4 B0 ...` — byte-identical round trip |
| Same `submissionId` posted **5× simultaneously** | **201 ×5, all returning the same id, exactly 1 row** |
| Two posts with **no** token | both **201**, **2 rows** — the filtered unique index does not collapse them |
| Gender `"M"` (free text) and gender `"Male"` after retiring it | **400** both — C-20 holds across the F-4/F-5 seam |
| Unknown patient id | **404** |
| F-1..F-4 regressions | `/api/health` 200, `/api/health/db` 200, `/api/auth/session` 200, `/api/clinic-settings/vital-ranges` 200, `/` and `/patients/new` 200 `text/html`, `/api/nope` **404 `problem+json`** — the error contract is intact |

**The builder's flagged item 1 — the tautological check constraint — checked and confirmed, at the
database, not in the source.** Plan F-5 §2 specifies
`DateOfBirth IS NOT NULL OR ApproxAgeYears IS NOT NULL OR both NULL`. I evaluated that predicate
over the whole space: it is **true for every possible row**, so building it literally would have
put a constraint in the schema that protects nothing while looking like it protects E-9. **The
builder was right to refuse it.** What shipped is `CK_Patient_AgeShape`, and I proved it is real
and enforcing with raw `INSERT`s that bypass the service entirely (`sqlcmd -I`):

| Raw insert (service bypassed) | Result |
|---|---|
| `ApproxAgeYears = 40`, `AgeRecordedOn` NULL | **rejected — `CHECK constraint "CK_Patient_AgeShape"`** |
| `DateOfBirth` **and** `ApproxAgeYears` both set | **rejected — same constraint** |
| `AgeRecordedOn` set with no age | **rejected — same constraint** |
| `ApproxAgeYears` + `AgeRecordedOn` together | accepted |
| `DateOfBirth` alone | accepted |

E-9 — the `[DI]`-marked "corrupts data" case — is therefore closed at the level that survives a
future writer who never goes through `PatientService`. **`planning-pms` should correct F-5 §2's
predicate.**

**The builder's flagged item 2 — the duplicate-phone gap — checked, and it is correctly F-6's, not
F-5's.** I reproduced it live: `"+91 98765-43210"` stores `NormalizedPhone = 919876543210` and
`"098765 43210"` stores `09876543210`, so the two do **not** share a key. Scope check against the
sources: plan F-5 §2 asks only for *"`NormalizedPhone` = digits only (E-59)"*, and brainstorm E-59's
mitigation is *"store as entered, index a normalised digits-only form for search; do not reject
formats the doctor uses"* — which is exactly what shipped. Deciding that a country code and a trunk
zero denote one person is a **matching rule**, and plan F-6 §1 (Q-13) is where identity rules are
made. **Correctly deferred; F-5 is not defective for it.** But two things must not be lost:
**F-6's stated assumption is "same `NormalizedPhone`", which inherits this gap verbatim** — Q-13
must resolve country-code/trunk-zero equivalence or F-6 will silently under-detect duplicates on
the most common real-world phone variation in this deployment. Second, the doc comment on
`PatientNormalizer.NormalizePhone` currently asserts that those three forms *are* collapsed
together, which is **not what the code does** — a comment that overstates a guarantee, flagged for
`code-review-pms`.

**Acceptance criteria — walked line by line against my own evidence:**

1. *Single-word name and no phone can be saved; the profile shows "Profile incomplete" and "No
   contact recorded"* — **met.** Proven live (rows 2–4 above) and in the UI layer by
   `PatientProfile.tsx` rendering the literal strings `Profile incomplete` and
   `No contact recorded`, asserted by `shows the incomplete flag and names each missing detail`
   and `says the patient can still be seen, so the flag never reads as a block`. The flag is
   displayed and **never branches anything** — E-8's mitigation is "flag", not "prevent", and I
   checked that nothing reads it as a gate.
2. *Approx age stores `ApproxAgeYears` + `AgeRecordedOn` and displays `~40 (recorded 2026)`; the API
   rejects a bare age with 400* — **first half met; second half deliberately not built. Escalated,
   not absorbed.** The pair is always stored (proven at the DB by the constraint above) and the
   display is exact (`~40 (recorded 2026)` live, and `PatientAgeFormatter` never ages an estimate
   forward). But a bare `approxAgeYears` with no date returns **201 with today's date stamped**,
   not 400. **My assessment, stated as an assessment:** the plan clause is internally inconsistent
   with the rest of its own section — plan §4's `PatientForm` has no "recorded on" input, so under
   the literal rule the ordinary UI submission would 400 and AC-2's *first* half would be
   unreachable; and letting the client stamp the date instead would put a clinical record's
   provenance on a browser clock. The rule E-9 actually protects — *never store a bare age* — is
   enforced twice and I proved the database half myself. So I am recording this as a **plan defect
   of the same family as the tautological constraint in the same section**, not as a build defect,
   and passing it. **This is the one judgement call in this verification.** `AskUserQuestion` is
   unavailable inside a subagent, so it is escalated here in writing instead: **if `planning-pms`
   or the product owner rules that the literal 400 is required, F-5 needs a small, well-scoped
   rework** (reject in `ValidateAge`, and add the date input to `PatientForm`).
3. *DOB of today accepted and the age displays in days; a future DOB is rejected with 400* —
   **met**, both live. `"0 days"` is rendered rather than `0`, and the formatter switches to months
   under two years and years thereafter (E-11).
4. *`"  Ravi   Kumar  "` and `"Ravi Kumar"` both persist `NormalizedName = "ravi kumar"`* — **met**,
   verified by querying the column directly. The stored `FullName` is also whitespace-collapsed for
   display while **case is preserved** — normalisation adds a matching column, it never rewrites
   what the clinic typed.
5. *A non-Latin name saves, displays and re-fetches unchanged* — **met, proven at byte level** in a
   real SQL Server `nvarchar` column and in the HTTP response bytes, not merely in C#. (My first
   attempt showed `?????` — that was Git Bash mangling my *request*, not the app; re-sent from a
   UTF-8 file it round-trips exactly. Recorded because it is a trap for the next person smoke-testing
   Unicode on this host.)
6. *Double-clicking Save creates exactly one patient row* — **met, at the level that matters.** Not
   a `disabled` prop and not a read-before-write: **five simultaneous** requests all returned the
   same id and left **one row**, decided by `UX_Patient_SubmissionId`, which I confirmed in
   `sys.indexes` is unique **with `has_filter = 1`** and filter `([SubmissionId] IS NOT NULL)`. The
   filter is load-bearing — without it SQL Server treats NULLs as equal and the table would permit
   exactly one token-less patient; I proved it does not by registering two token-less patients.
   The client half is covered by `useSubmitOnce` (`runs the second of two synchronous submits not
   at all`).

**Data-integrity and architecture spot-check:**

- **Normalisation is written server-side and cannot be supplied by the client.**
  `CreatePatientRequest` has exactly eight properties and **none** of `NormalizedName`,
  `NormalizedPhone`, `Status`, `RegisteredUtc` or `MergedIntoPatientId` — so a client cannot make
  itself invisible to F-6's duplicate check. `NormalizeName` collapses **all Unicode** whitespace
  (the pasted non-breaking space of E-60), normalises to form C, and lowercases **invariantly**
  rather than by culture; it never transliterates, so two genuinely different Devanagari names are
  not folded onto one key (E-57). `NormalizePhone` returns **null, not empty**, so phone-less
  patients do not all share one blank matching key.
- **`PatientStatus` has no `Deleted` member** — the no-hard-delete rule of E-33 is enforced by the
  type, not by a convention, before F-8 ever needs it.
- **Layering as specified.** `PatientsController` → `IPatientService` → `IPatientRepository` →
  `PatientRepository` (the only type touching `_db.Patients`). The only `PmsDbContext` occurrences
  anywhere in `PMS.Api/Controllers` are inside comments. Responses are `PMS.Application/Dtos/Patients/`
  records; no EF entity crosses the wire. Frontend is folder-per-feature under `features/patients/`
  with the plan's named files (`PatientForm.tsx`, `PatientProfile.tsx`, `patientsApi.ts`,
  `usePatients.ts`) plus `shared/hooks/useSubmitOnce.ts` at the plan's named target.
- **Gender is a `<select>` fed by F-4's active options**, never a text input — asserted by
  `offers no way to type a gender that is not on the list`, and rejected server-side as well, which
  I exercised live. **C-20 holds across the F-4/F-5 seam in both directions.**

**BRD cross-check (the user's explicit ask).** `BRD/Doc_BRD.md` → *Patient Management*: "Add, edit,
and view patient details" and capture "Name / Age / DOB / Gender / Contact details" (L86–93).
Against F-5's scope: **Add — met** (`POST /api/patients`, live); **View — met**
(`GET /api/patients/{id}` + `PatientProfile`); **Name, Age/DOB, Gender, Contact — all four
captured**, with the BRD's ambiguous "Age / DOB" resolved the way brainstorm C-19/E-9 requires
rather than as a bare mutable number. **Edit** is F-8 and **"Search patients by name or phone
number"** (L93) is F-7 — both out of F-5's scope by the plan's own dependency map, so the BRD line
is not yet fully covered and **F-5 is not claimed to cover it**; F-5 ships the indexed
`NormalizedName`/`NormalizedPhone` columns that search will read. Nothing in F-5 contradicts the
BRD. (`modules/01-patient-management.md` used only as a pointer into the BRD, per instruction.)

**Carried and flagged — not decided here:**

- **Merge order is a real constraint, not a note.** This branch contains F-4's commits.
  **F-4 must merge before or with F-5**; merging F-5 alone would bring F-4 onto `main` without F-4
  ever clearing its own gate — the same out-of-band pattern already recorded against F-1 and F-2.
  For `finishing-pms`, after `code-review-pms`.
- **AC-2's bare-age clause** (above) — the one item genuinely needing an owner or `planning-pms`
  ruling.
- **`SubmissionId` and `UX_Patient_SubmissionId` are not in plan §4's `Patient` column list**,
  though plan F-5 §5 does require idempotent create per submit token. A justified addition, but a
  schema addition beyond the named model — for `code-review-pms`.
- **The plan's route table lists 409 on `POST /api/patients`.** F-5 correctly returns none; that
  status belongs to F-6's duplicate-confirmation flow. No client should expect it yet.
- **Playwright still cannot launch a browser on this host** — I reproduced it myself: 8 specs, 8
  `spawn EPERM`, zero code-related failures. Not routed back as rework; **the F-1 deadline stands,
  the harness must work before F-14.**
- **`CLAUDE.md`'s `dotnet-ef` gotcha is stale** — confirmed 10.0.11 installed and used.
- **F-2's committed seed credential** remains a live exposure, restated so it is not lost.
- **React Router v6 advisories** now carried from F-1 through F-5 with no decision;
  `PatientForm` adds a programmatic `navigate()` to a server-generated id, so still not reachable,
  but the surface keeps growing.

**Gate status.** F-5 is `Built & Verified` — it works, on evidence I produced. It is **not**
finished: `code-review-pms` reviews it before `Reviewed`. Per the dependency map this makes **F-6,
F-7, F-8 and F-9** buildable (subject to their own decision tags). No application code, test code or
migration was modified in this pass; `git status` in both worktrees is clean at `9844075` and
`e1bf206`, and both throwaway databases I created were dropped.

---

### 2026-09-07 — F-6 duplicate detection: tracking gap recorded (no builder entry ever reached this file)

**This is not an F-6 build entry. It is a record that F-6's build entry is missing**, written while
merging `main` into `feature/f-7-patient-search`. F-6's *code* is real and is on `main`; F-6's
*record* is not, and the gap would otherwise be invisible because the status table still read
`Not Started` for a feature whose migration and endpoints are live on the default branch.

**What is on `main`, verified by me rather than assumed.** `git log` shows commit `4e1c33c`
"F-6: duplicate detection at registration + `merged_into` pointer", merged through
`93e50db` "Merge pull request #6 from Nilesh-PIO/feature/f-6-duplicate-detection". The code it
carries: `PatientDuplicateService`, `NameSimilarity`, `PatientProjection`,
`DuplicateCandidateResponse` / `DuplicateCheckRequest` / `MarkMergedRequest`,
`DuplicatePatientException`, migration `20260907124203_AddPatientDuplicateIndexes`, the
`duplicate-check` and `{id}/mark-merged` routes, `DuplicateWarningDialog.tsx`,
`usePatientDuplicates.ts`, and the tests `PatientDuplicateServiceTests`,
`PatientMatchingPrimitivesTests`, `PatientDuplicateEndpointTests`, `DuplicateWarningDialog.test.tsx`,
`PatientFormDuplicates.test.tsx`, `patient-duplicates.spec.ts`. All of it builds and its tests pass
in this worktree — see the merge entry below for counts.

**What is missing.** The copy of `doc/implementation-progress.md` that reached `main` still carries
`| F-6 | ... | Not Started | — | — |`, and there is no F-6 log entry anywhere in this file. Whatever
F-6's builder recorded — its Q-13 decision, its assumptions, its acceptance-criteria walk-through,
its own test output — did not travel with the code.

**What I changed, and what I deliberately did not.** The status table row for F-6 now reads
`Awaiting verification` rather than `Not Started`, because `Not Started` is factually false for code
that is on the default branch. `Awaiting verification` is a status this role is permitted to set and
is the accurate one: F-6 is built, and `verification-pms` has not gated it. **I did not write an
F-6 build report.** I did not build F-6, I have not walked its acceptance criteria, and inventing a
report for someone else's work is exactly the kind of unearned record this file exists to prevent.

**For the owner / `verification-pms`.** F-6 reached `main` without `Built & Verified` and without
`Reviewed` — the same out-of-band merge pattern already recorded against F-1, F-2 and now F-4/F-5
(PRs #4, #5, #6). F-6 still needs an independent verification pass, and that pass has no builder
report to check against; it will have to work from the code and the plan directly.


---

### 2026-09-07 — F-7 patient search, recent patients, disambiguating picker

**Status: `Awaiting verification`.** Handed to verification-pms. Branch left for review, not merged,
not pushed, worktree not removed.

- Got an isolated worktree from `worktree-pms` at
  `C:\Users\NileshMalviya\source\repos\f-7-patient-search`, branch `feature/f-7-patient-search`,
  cut from **`feature/f-5-patient-registration` at `e1bf206`** — deliberately not from `main`.
  Confirmed distinct from the main working tree before writing anything.
- **Confirmed the plan I built against is the committed one**, as instructed:
  `git hash-object doc/planning-pms-verification.md` returns `e5c55a5…` in both this worktree and
  the main tree, and `doc/brainstorm-pms-verification.md` returns `2ac6284…` in both. Byte-identical,
  so nothing here was built against an uncommitted or stale plan.

**Why the branch point is F-5 and not `main`, stated plainly.** `main` is at `172e946`, which is
F-3. F-4 and F-5 are `Built & Verified` but exist only on their own branches, stacked:
`feature/f-5-patient-registration` contains F-4's `9844075` beneath F-5's `e1bf206`. F-7 depends on
F-5, so building on `main` would have meant building against a `Patient` entity that does not exist.
**Consequence, and it is the same one F-5 recorded: this branch carries F-4's and F-5's commits, so
F-4 and F-5 must merge before or together with F-7.** Merging F-7 alone would drag both in without
either clearing `code-review-pms`.

**Concurrency note.** F-6 (duplicate detection) was being built in parallel from the same F-5 base
by another instance. Per the dependency map both depend only on F-5 and not on each other, so F-7
neither waited for nor read F-6's branch. No F-6 file, branch or worktree was touched. Two
deliberate consequences are recorded under "expected merge conflicts" below.

---

#### The C-22 decision, resolved the way the instruction asked: assumed, pinned, and flagged

Plan F-7 §1 is tagged **Needs decision (C-22 — no `Q-` exists)**. Everything the plan's own
assumption states was built literally, and every number is pinned by a test so a different answer
from the physician is a small, visible change rather than a hunt:

| C-22 decision | Built | Pinned by |
|---|---|---|
| Case-insensitive substring on `NormalizedName` | yes | `Search_is_case_insensitive_and_whitespace_insensitive` |
| Digits-only phone match incl. **last-4 suffix** | yes, as `Contains` (a superset of suffix) | `Typing_the_last_four_digits_of_a_stored_phone_finds_that_patient` |
| Minimum query length **2** | yes, 400 below it | `The_minimum_is_two_characters_and_two_characters_is_allowed` |
| **300 ms** debounce | yes, client-side | `debounces at the plan-specified 300 ms` |
| Rank exact → prefix → substring → phone | yes | `The_rank_constants_are_ordered_the_way_the_plan_states` |
| Inactive **and merged** excluded by default, one toggle | yes | `Merged_patients_are_excluded_by_default_even_while_still_active` |
| Fuzzy fallback **only** on an empty result | yes | `The_fallback_runs_only_when_the_exact_search_found_nothing` |

**ASSUMPTION 1 — the fuzzy threshold is 0.7 here, not F-6's 0.85.** The plan says F-7 uses "the same
similarity *function* as F-6"; it does not say the same threshold, and they are not the same
decision. F-6 decides *"interrupt the physician mid-registration to warn them"* — a false positive
there is costly, so 0.85 is right. F-7 decides *"offer this row after finding nothing at all"* — a
false positive costs one line on an otherwise empty screen, a false negative costs a duplicate
patient and a split history (E-30). The costs are asymmetric in exactly one direction. Recorded as
`ASSUMPTION` on `PatientNameSimilarity.SearchFallbackThreshold` with the reasoning, and pinned by
`The_fallback_threshold_is_pinned_at_the_documented_value`. **If the owner rules that one number
must govern both features, that constant is the only thing that changes.**

**ASSUMPTION 2 — "recent patients" means most recently *registered*.** BRD L159 says only "View
recent patients". `modules/05-search-navigation.md` names this gap explicitly ("whether 'recent'
means recently viewed or recently seen in consultation"). It was not free-styled: plan F-7 §2 says
F-7 adds **no new entities**, which rules out a recently-viewed access log, and `Visit` is **F-10**
and does not exist yet, which rules out recently-seen. What remains is registration order. Recorded
as an `ASSUMPTION` block on `PatientRepository.GetRecentAsync`, together with the intended end state:
when F-10 lands this becomes `ORDER BY COALESCE(last visit date, RegisteredUtc) DESC` — **one method
changes, and no endpoint, DTO or client does.** If the physician actually wants recently *viewed*,
that is a new entity and therefore a plan amendment, not a change here.

**ASSUMPTION 3 — `lastVisitDate` ships now and is always null until F-10.** Plan F-7 §2 requires it
as "a projected read (computed in the query, not stored)" and §3 lists it in the response; the entity
it would be projected from is F-10's. Shipping the field now rather than adding it later means F-10
fills in a value instead of changing a contract every client is already written against. **E-28 is
not deferred while it is null:** `registeredOn` was added to the DTO to carry the date axis, so two
patients with the same name, the same age and no phone between them are still distinguishable today.
Pinned by `Last_visit_date_is_null_until_F_10_and_the_registration_date_carries_the_gap`, which is
designed to **fail when F-10 starts populating it** — that is the reminder to revisit the picker's
date column, not a rule that the field stays empty forever.

---

**Built (backend):**

- `PMS.Application/Services/PatientSearchRules.cs` — the C-22 semantics as **expression trees**, so
  the *same* rule EF Core compiles into SQL is compiled and run in memory by the unit tests. There
  is no second LINQ-to-Objects copy written for testing, which is the drift that would otherwise
  show up as "the search finds them on my machine".
- `PMS.Application/Services/PatientNameSimilarity.cs` — Levenshtein ratio, whole-string and
  per-word, for the E-30 fallback.
- `PMS.Application/Services/PatientSearchService.cs`, `Abstractions/IPatientSearchService.cs`,
  `Dtos/Patients/PatientSummaryResponse.cs` (+ `PatientMatchKind`).
- `PMS.Application/Abstractions/IPatientRepository.cs` — gains `SearchAsync`, `GetRecentAsync`,
  `GetNameKeysAsync`, `GetByIdsAsync`, plus the `PatientSearchSpecification` and `PatientNameKey`
  records. F-5's own comment on that interface anticipated this ("F-7 adds search").
- `PMS.Infrastructure/.../PatientRepository.cs` — the EF implementations. All `AsNoTracking`.
- `PMS.Api/Controllers/PatientsController.cs` — `GET /api/patients/search` and
  `GET /api/patients/recent`, exactly the plan's route table. The controller depends on
  `IPatientSearchService`, never on `PmsDbContext`.

**Three backend decisions worth naming:**

1. **The database does the matching, the ranking and the limiting — in one statement.** Plan F-7 §1
   says the budget is met by "server-side `TOP 20`, and no client-side filtering". That is literally
   true here: match, rank and `Take` compose into a single `SELECT TOP (n) ... WHERE ... ORDER BY`,
   and exactly `take` rows cross the wire. `Ranking_survives_translation_to_SQL` exists because a
   LINQ construct that silently evaluated on the client would still pass every unit test while
   scanning the whole table in production.
2. **The sort is *total*.** Rank, then registration date, then name, then **id**. Without the id, two
   rows equal on every other key can come back in either order, and a `TOP 20` over a
   non-deterministic sort silently returns a different twenty on different runs.
3. **The fuzzy fallback is the only thing in this feature that reads more than `take` rows**, so it
   is fenced: it runs only after the exact search returns empty, and it reads two projected columns
   rather than whole entities. `The_fallback_runs_only_when_the_exact_search_found_nothing` asserts
   the fence by counting the read rather than by trusting the shape of the code.

**No migration, and that was checked rather than assumed.** Plan F-7 §2 says "no new entities" and
attributes the `NormalizedName`/`NormalizedPhone` indexes to F-6 — but **F-5 already created both**,
with a comment in `PatientConfiguration` saying it did so deliberately because F-6 and F-7 read them.
Verified live against the smoke database: `sys.indexes` lists `IX_Patients_NormalizedName`,
`IX_Patients_NormalizedPhone`, `IX_Patients_MergedIntoPatientId` and `UX_Patient_SubmissionId`, and
`dotnet ef migrations has-pending-model-changes` reports **"No changes have been made to the model
since the last migration"**. A covering index was considered and **rejected on the measurement
below** — at 185 ms p95 against a 2,000 ms budget it would have bought nothing, and it would have put
F-7 into F-6's `AddPatientDuplicateIndexes` migration lane and model snapshot for no gain.

**Built (frontend):**

- `shared/components/PatientPickerRow.tsx` — the E-28/RSK-12 guard as a component. Name, phone tail
  (or an explicit "No phone recorded"), age, and a date, on **every** row on **every** path.
- `features/patients/PatientSearch.tsx` — the global box in `AppLayout`, `/` to focus (REC-16).
- `features/patients/PatientList.tsx` (route `/patients?query=`), `RecentPatients.tsx` (route `/`),
  `PatientResults.tsx` (the shared result/empty/error rendering), `usePatientSearch.ts`
  (`useDebouncedValue`, `usePatientSearch` with `placeholderData: keepPreviousData`, and
  `useRecentPatients`).
- `patientsApi.ts` gains `searchPatients` / `getRecentPatients`; `types/patient.ts` gains
  `PatientSummary`; `routes.tsx` replaces the two F-7 placeholders; `AppLayout.tsx` mounts the box.
- `PatientForm.tsx` gains `?name=` / `?phone=` prefill — the receiving half of E-7.

**Data integrity check (F-7 §5) — the mechanism, not a mention.** The risk is wrong-patient
selection (RSK-12, E-28), and it is closed structurally rather than by discipline:

- **A name-only row cannot be built.** `PatientSummaryResponse` has no name-only shape — phone tail,
  age and a date are non-optional positions filled on every path including the fuzzy one — and
  `PatientPickerRow` is the only way a patient is rendered as a choice anywhere in the application.
- **Nothing is ever auto-selected.** The service returns a list even for one match, the API returns
  an array even for one match, and pressing Enter goes to the results page rather than opening the
  top hit. Asserted at all three layers.
- **A guess never looks like a find.** A fuzzy row carries `matchKind: "SimilarName"`, gets a badge,
  and the list gets a banner saying no exact match was found.
- **A failure is never rendered as "not found".** That specific confusion is what makes someone
  register a duplicate, so it has its own test at both ends
  (`reports a failed search as a failure and never as "no patient found"` and
  `shows a failed load as an error, never as an empty clinic`).

**Test results — run in the worktree after deleting every `bin/` and `obj/`, real output:**

| Command | Result |
|---|---|
| `dotnet build PMS/backend/PMS.sln` (clean) | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `dotnet test PMS/backend/PMS.sln` | `PMS.Application.Tests` **Failed: 0, Passed: 228, Skipped: 0**; `PMS.Api.IntegrationTests` **Failed: 0, Passed: 156, Skipped: 0** |
| `npx vitest run` in `PMS/frontend`, run twice | **22 files, 247 passed, 0 failed, 0 skipped** both times — no flake |
| `npm run build` | Succeeded — 118 modules, emitted into `PMS/backend/src/PMS.Api/wwwroot` after deleting it first |
| `tsc --noEmit` on `PMS.E2E` | clean, no diagnostics |

**Total: 631 automated tests passing (384 .NET + 247 Vitest), 0 failed, 0 skipped** — up from F-5's
525. Counts read, not exit codes, per the known gotcha that this host prints `Passed!` on a
zero-test project.

New test files at the plan's named targets:
`PMS.Application.Tests/Services/PatientSearchServiceTests.cs` (49 tests),
`PMS.Api.IntegrationTests/Endpoints/PatientSearchEndpointTests.cs` (20 tests),
`frontend/src/features/patients/PatientSearch.test.tsx`,
`frontend/src/features/patients/RecentPatients.test.tsx`, and
`PMS.E2E/specs/patient-search.spec.ts`. Two beyond the plan, both earning their place:
`shared/components/PatientPickerRow.test.tsx` (the E-28 guard deserves its own assertions) and
`features/patients/PatientList.test.tsx` (the URL-backed query is behaviour the header box lacks).

**One defect found in a shared test helper, root-caused rather than worked around.**
`testUtils.stubFetch` matched stub routes with `url.endsWith(path)`, so a key of `/patients/search`
never matched `/api/patients/search?query=ravi` — the stub threw, and `httpClient` faithfully
reported that as *"Could not reach the server"*. Nine tests failed in a way that looked like a
network bug in the application. Fixed in the helper by trying the full URL first and then the path
with any query string stripped, so every query-string-free stub behaves exactly as before (all 210
pre-F-7 Vitest tests still pass). **This was a latent trap for every future feature with a
query-string endpoint, not only for F-7.**

**Performance — measured against the plan's design point, not asserted.** AC-4 is a number, so it is
reported as one. `AC4_search_over_5000_patients_stays_inside_the_two_second_budget` seeds **5,000
patients** deterministically, warms once, then times 60 requests across 12 query shapes (prefix,
substring, two-character, phone-exact, phone-suffix, and two no-match queries that pay for the full
fuzzy pass):

```
F-7 search latency over 5000 patients, 60 requests:
  p50=21.6 ms, p95=185.3 ms, max=294.1 ms   (budget 2000 ms)
```

**p95 is about 10.8x inside budget**, and the worst single request — including the fuzzy fallback —
is about 6.8x inside it.

**Live smoke against a throwaway database under real Kestrel** (`PMSDb_F7Smoke_2371`, created and
dropped by this pass; both committed migrations applied by `dotnet ef database update`). *Port note:*
7191 was already bound by another agent's instance, so this ran on **7291**; the other instance was
left running and untouched, and confirmed still answering `200` afterwards.

| Check | Result |
|---|---|
| `GET /api/patients/search` and `/api/patients/recent` with no cookie | **401** both — default-deny, no attribute needed |
| `GET /api/patients/recent` on an empty clinic (E-2, AC-5) | **200 `[]`** — an answer, not an error |
| **AC-1** `?query=3210` against `+91 98765-43210` | **1 row, `matchKind: "Phone"`** (E-59) |
| **AC-2** `?query=Ravi Kumar`, two patients, same name, same DOB | **2 rows**, identical `ageDisplay`, **different `phoneTail`** (3210 / 9999) — distinguishable, neither auto-selected (E-28) |
| **AC-3** `?query=Zenobia Xylophone` | **200 `[]`**, so the client's register action is reachable (E-7) |
| **E-30** stored `Bartholemew Quintrell`, searched `Bartholomew Quintrell` | found, **`matchKind: "SimilarName"`** — labelled a guess |
| **E-60** stored `"  Sunita   Devi  "`, searched `sunita devi` | found; age rendered `~40 (recorded 2026)` |
| **E-57** stored `रवि कुमार`, searched `कुमार` | **found** (see the harness note below) |
| Retire one twin, point one record at a survivor (via SQL — F-8/F-6 have no endpoints yet) | default search **excludes both**; `includeInactive=true` returns them **flagged** `[INACTIVE]` / `[MERGED]`; `recent` **never** offers either |
| Fuzzy target once merged | **excluded by default** — the fallback honours the same rule |
| `?query=` / `?query=r` / `?query=%20%20` / no param at all | **400 `application/problem+json`** with a `query` field error, never the whole table |
| `take=0` / `take=1` / `take=1000` | default / 1 / clamped — clamped, never rejected |
| `GET /api/patients/{guid}` and `/api/patients/notaguid` | **200** / **404** — the new literal segments do **not** shadow the `{id:guid}` profile route |
| F-1 regressions | `/api/nope` **404 problem+json**; `/patients` and `/` **200 text/html** |

*Harness note, not a code defect:* E-57 first came back empty from the smoke. Root-caused before
being believed: the Devanagari in the `curl -d` argument was transcoded to literal `?` (0x3F) **by
the shell**, so `??? ?????` was what reached the API and what got stored. Re-sent as a UTF-8 file
with `--data-binary`, the same search returns `रवि कुमार`. The application was always correct — the
integration test `A_unicode_name_is_searchable_through_SQL_Server_collation` proves the same path
through .NET's `HttpClient` with no shell involved. **Recorded because "the smoke said it was
broken" is exactly the kind of thing that otherwise gets quietly dropped.**

**Acceptance criteria — walked line by line:**

1. *Typing 4 digits matching the tail of a stored phone returns that patient (E-59)* — **met.**
   Live above; `Typing_the_last_four_digits_of_a_stored_phone_finds_that_patient`;
   `AC1_typing_four_digits_matching_a_stored_phone_tail_returns_that_patient`. Also proven for four
   stored formats (`+91 98765-43210`, `098765 43210`, `9876543210`, `(98765) 43210`).
2. *Two patients sharing name and age render distinguishable rows (phone tail + last visit date) and
   neither is auto-selected (E-28)* — **met, with one substitution stated openly.** The rows are
   distinguishable and nothing auto-selects, proven at three layers. **The AC names "last visit
   date", which cannot exist before F-10** — `registeredOn` carries that axis instead and is a real
   differentiator (5 Jan vs 20 Aug in the component test; two distinct dates live). Flagged rather
   than quietly counted as met.
3. *A no-match query renders "No patient found" plus a register action pre-filled with the typed
   text (E-7)* — **met.** `offers to register the typed name when nothing is found` asserts the
   `href` is `/patients/new?name=Zenobia`, and `PatientForm` reads it. A digits-only query prefills
   the **phone** instead, which is the honest reading of what the person typed.
4. *With 5,000 patients seeded, p95 search latency ≤ 2 s (C-12, REC-19)* — **met, measured: 185 ms.**
5. *Recent-patients list is empty-stated, not blank, on a fresh install (E-2)* — **met.** Live `[]`
   from an empty database; `renders an empty state with a register action on a fresh install`; and
   the case that matters more, `shows a failed load as an error, never as an empty clinic`.

**Expected merge conflicts with F-6 — by design, and they should be resolved by keeping one copy:**

- `shared/components/PatientPickerRow.tsx` — plan **F-6 §4 names this exact path** for the
  duplicate-candidate list, and F-7 §4 requires every row to be one. Both branches create it. The
  correct resolution is **one component**, not one per feature; the whole point is that a candidate
  looks identical wherever it is offered.
- `PatientNameSimilarity` — plan F-7 §1 says the fallback "uses the same similarity function as
  F-6". F-6 is unmerged, so F-7 wrote a standalone copy carrying a `COORDINATION NOTE` in the file
  saying exactly this. **Whichever branch merges second should delete its own copy and point at the
  survivor.** Two implementations that drift would mean the duplicate check and the search disagree
  about who is the same person.
- `patientsApi.ts`, `types/patient.ts` and `PatientForm.tsx` will conflict at line level; ordinary
  parallel-branch resolution.

**Flagged for review — not decided here:**

- **The three assumptions above** (fuzzy threshold 0.7, "recent" = registration order,
  `lastVisitDate` null until F-10). All three are owner calls; all three are one-line changes.
- **Acceptance criterion 2 names a field that cannot exist yet** (see AC-2). The plan should either
  accept `registeredOn` as the stand-in or re-scope that half of the AC to F-10.
- **A word-prefix rank bucket was added** between the plan's "prefix" and "substring", so that typing
  a shared surname lists people whose name begins with it at a word boundary above people who merely
  contain the letters. It is a refinement *inside* the plan's ordering — every bucket the plan names
  keeps its relative position — but it is an addition, and it is named here rather than buried.
- **`IsProfileIncomplete` is now computed in two places** — `PatientService.MissingFields` (private
  to F-5) and `PatientSearchService`. Not extracted, because reshaping a `Built & Verified`
  feature's private method in passing is F-8's territory, not F-7's. A test asserts the two agree, so
  a future divergence fails rather than silently making the picker disagree with the profile it
  opens.
- **React Router v6 advisories unchanged** (GHSA-wrjc-x8rr-h8h6, GHSA-337j-9hxr-rhxg; fix in 7.18+).
  F-7 grows the routing surface again. Still not reachable — no SSR, no redirect target from
  untrusted input — but still unresolved since F-1.

**Known environment limitation — E2E written, not proven (unchanged since F-1):**

`PMS.E2E/specs/patient-search.spec.ts` is written at the plan's named target, covers the golden path
and the plan's named **E-28** case plus E-59, E-7 and E-2, and typechecks clean. Browser launch
still fails with `browserType.launch: spawn EPERM` on this host, so **the browser-dependent specs
are not claimed as passing.** Each one's behaviour is proven by a suite that does run: the golden
path and the `/`-focus shortcut by `PatientSearch.test.tsx`; E-28 by `PatientPickerRow.test.tsx`,
`AC2_two_patients_sharing_name_and_age_come_back_distinguishable` and the live smoke; E-7 by
`PatientSearch.test.tsx` and `PatientList.test.tsx`; E-2 by `RecentPatients.test.tsx`. The two
API-request specs need no browser and do run. **The deadline recorded against F-1 stands: the
harness must work before F-14.**

*Also worth recording, since prior entries flagged its absence:* **`dotnet ef` is available in this
environment now** — `database update`, `migrations has-pending-model-changes` and the design-time
build all ran successfully in this pass.

Committed on `feature/f-7-patient-search`. Not merged, not pushed, worktree not removed.
Next: `verification-pms` — and note it must verify against the **F-5 branch** baseline, not `main`.

---

### 2026-09-07 — F-7: merged `main` (F-4, F-5, F-6) into `feature/f-7-patient-search` and resolved the F-6/F-7 overlap

**Status: unchanged at `Awaiting verification`.** This pass added no feature; it reconciled F-7 with
what landed on `main` while F-7 was being built, and collapsed the two things F-6 and F-7 had each
built a private copy of. Branch pushed to `origin` (it had been pushed once already); **not merged
into `main`, `main` not touched, nothing force-pushed.**

**Why there was anything to reconcile.** F-6 and F-7 were built in parallel from the same F-5 base
(`e1bf206`), by the dependency map's own reading that neither depends on the other. F-6 then reached
`main` first, through PR #6. Both features are named by the plan as owners of the same picker row
(F-6 point 4 and F-7 point 4 both say `shared/components/PatientPickerRow.tsx`) and both are told to
compare names the same way (F-7 point 1: "uses the same similarity function as F-6"). Built in
isolation, that produced two components and two similarity classes. F-7's own entry predicted this
and said whoever merged second should resolve it; this is that resolution.

`git merge main` produced conflicts in exactly six files. None was resolved with
`--ours`/`--theirs`; both sides were read in each case.

| File | Resolution |
|---|---|
| `PMS.Api/Controllers/PatientsController.cs` | Constructor now takes all three services (`IPatientService`, `IPatientSearchService`, `IPatientDuplicateService`). Both sides had added a second dependency to a one-dependency constructor. The route bodies had already auto-merged; F-5's `Create` correctly kept F-6's `confirmDuplicate` parameter. |
| `PMS.Application/DependencyInjection.cs` | Both registrations kept, F-6's then F-7's, with a comment recording why they are two services and not one. |
| `frontend/src/features/patients/patientsApi.ts` | Purely additive on both sides — union of the type imports and of the four functions (`searchPatients`/`getRecentPatients` from F-7, `checkDuplicates`/`markMerged` from F-6). |
| `frontend/src/index.css` | F-7's search/results/page rules kept; F-6's duplicate-dialog rules kept; the two rival picker-row blocks (`.picker-row*` and `.patient-picker-row*`) replaced by one block under the `.patient-picker-row*` names, carrying F-6's definition-list styling **and** F-7's selectable/hover/focus and badge styling. No dead CSS left behind. |
| `frontend/src/shared/components/PatientPickerRow.tsx` | **Add/add.** One component now — see below. |
| `doc/implementation-progress.md` | See below. |

#### The picker row: one component, both features' behaviour

The two versions were not two drafts of the same thing. F-6's was a presentational `<dl>` of four
labelled facts including an explicit date of birth, never selectable. F-7's took a `PatientSummary`
object, rendered as a `<button>` with an accessible label, carried Inactive/Merged/Similar-name/
Profile-incomplete badges, and fell back to the registration date because no `Visit` exists before
F-10. Keeping either one alone would have lost something real, so the surviving component takes the
**union**, and the props are **flat rather than a patient object**:

- `shared/` no longer imports a `features/` type, which F-7's version did (`PatientSummary`). That
  was a layering inversion that would only have got worse as F-9 added a third caller.
- The two features' DTOs genuinely differ — `DuplicateCandidate` has a date of birth and no
  registration date; `PatientSummary` has a registration date and no date of birth. Flat props let
  each caller supply what it actually has instead of one being forced into the other's shape.
- `undefined` and `null` are kept distinct for `dateOfBirth`: `null` renders "Not recorded" (this
  patient has none), `undefined` omits the fact (this caller's DTO has no such field). A row does
  not assert an absence nobody reported.
- `onSelect` present ⇒ the row is a button with an `aria-label` carrying the disambiguators;
  absent ⇒ an inert `<div>`. That is exactly what F-6's dialog wants — merge tooling is Phase 2, and
  its test *asserts* the rows are not selectable, which still passes.
- Callers: F-7's `PatientResults`/`RecentPatients` spread `{...patient}` (the DTO's field names are
  the props); F-6's `DuplicateWarningDialog` passes fields explicitly and now also passes `gender`
  and `isMerged`, so a candidate already marked as a duplicate carries a visible badge as well as
  the dialog's sentence.
- `data-testid="patient-picker-row"` is on both variants, so F-7's five Vitest suites and its
  Playwright spec keep their selector.

**One test assertion was reworded, and it is named here rather than buried.** F-6's
`DuplicateWarningDialog.test.tsx` asserted the exact string `'No phone'`; the merged component uses
F-7's more explicit `'No phone recorded'`. The assertion now looks for that string. The property
under test — the cell states the absence instead of being blank — is unchanged, and no assertion was
loosened, deleted or skipped. F-7's `PatientPickerRow.test.tsx` was converted from object props to
flat props (mechanical) and **gained two tests** covering each caller's shape on the one component.

#### The similarity function: one algorithm, two thresholds

F-6 shipped `NameSimilarity` (grapheme-cluster Levenshtein ratio, word-order-insensitive,
`DefaultThreshold = 0.85`). F-7 shipped `PatientNameSimilarity` (char Levenshtein ratio, plus a
query-word × name-word cross product, `SearchFallbackThreshold = 0.7`). Same family, two
implementations, and drift between them would have meant the duplicate check and the search
disagreeing about who is the same person.

`PatientNameSimilarity.cs` is **deleted**. `NameSimilarity` is the single type and now exposes:

- `Ratio(left, right)` — unchanged, still F-6's whole-name comparison at `DefaultThreshold` (0.85).
- `Score(query, name)` — F-7's query-shaped scoring, rebuilt **on top of `Ratio`** rather than on a
  second Levenshtein. It exists because F-7's inputs are asymmetric: one side is a fragment the
  physician typed, and `"kumr"` has to be able to reach `"ravi kumar"`, which whole-string `Ratio`
  scores at 0.4. Folding it onto `Ratio` also gave F-7's path the grapheme-cluster handling it did
  not have, so a Devanagari name is no longer held to a quietly stricter standard (E-57).
- `SearchFallbackThreshold` — 0.7, unchanged.

**The two thresholds were deliberately not collapsed**, and there is now a test saying so with the
reason attached. F-6 interrupts a registration, where a false positive costs someone's attention, so
it is strict. F-7 offers an extra row on a screen that already found nothing, where a false negative
costs a duplicate patient record and a split history (E-30). The plan says "the same similarity
*function*", not the same threshold.

Three tests were added to `PatientMatchingPrimitivesTests` to keep the consolidation from silently
coming apart: the ordering of the two thresholds, that `Score` is never below `Ratio` for the same
pair (which fails if anyone reimplements `Score` independently), and that `Score` reaches a full name
from one typed word where `Ratio` cannot.

#### The tracker itself

Both branches had appended to this file and both appends were kept — but reading the two sides
turned up two losses that a mechanical resolution would have carried forward silently:

1. **F-5's builder log entry was gone from `main`.** F-5's verification entry says the builder's
   entry "lands on merge"; it did not. `main`'s copy of this file does not contain it, so git
   presented it as a deletion and would have kept it deleted. It is **restored verbatim from
   `e1bf206`**, in pipeline order, immediately before F-5's verification entry, with a note saying
   where it came from.
2. **F-6 has no log entry at all and its status row still read `Not Started`** while its code sits on
   `main`. A separate entry above records that gap; the row now reads `Awaiting verification`, which
   is both accurate and a status this role may set. No F-6 build report was invented.

#### Build and tests — run in this worktree after the merge, from a clean build, real output

`bin/` and `obj/` deleted across the solution before building; `wwwroot` deleted before the frontend
build.

| Command | Result |
|---|---|
| `dotnet build PMS/backend/PMS.sln` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `dotnet test PMS/backend/PMS.sln` | `PMS.Application.Tests` **Failed: 0, Passed: 291, Skipped: 0**; `PMS.Api.IntegrationTests` **Failed: 0, Passed: 180, Skipped: 0** |
| `npm test` (`vitest run`) in `PMS/frontend` | **24 files, 274 passed, 0 failed, 0 skipped** |
| `npm run build` (`tsc -b && vite build`) | Succeeded — 120 modules, emitted into `PMS/backend/src/PMS.Api/wwwroot` |
| `tsc --noEmit -p tsconfig.json` on `PMS.E2E` | **exit 0**, no diagnostics |

**Total: 745 automated tests passing (471 .NET + 274 Vitest), 0 failed, 0 skipped.** Counts read
rather than exit codes, per the host gotcha that `dotnet test` prints `Passed!` on a zero-test
project. Run from inside the real worktree path, not a Temp copy, for the same reason.

Nothing broke as a consequence of the merge beyond the two conflicts' own call sites: F-6's dialog
tests and F-7's search tests both pass against the single component and the single similarity
function, which is the actual evidence that the collapse preserved both features' behaviour.

**Not re-run:** the Playwright browser suite. `browserType.launch: spawn EPERM` on this host is
unchanged from F-1 through F-7; the specs typecheck and are not claimed as passing. **The F-1
deadline stands — the harness must work before F-14.**

#### Carried and flagged — not decided here

- **F-6 needs an independent verification pass and has no builder report to check against.** It is
  on `main` regardless.
- **Merge order.** This branch now contains F-4, F-5, F-6 and F-7. F-4, F-5 and F-6 are already on
  `main`, so merging this branch no longer drags in unmerged features — the constraint recorded in
  F-5's and F-7's own entries is discharged by this merge.
- **F-5's AC-2 bare-age discrepancy** is still open and still needs an owner or `planning-pms`
  ruling. Untouched here.
- **F-2's committed seed credential** and the **React Router v6 advisories** are unchanged and
  restated so they are not lost.
- **The plan should record that F-6 and F-7 share `PatientPickerRow` and `NameSimilarity`**, and
  that the two thresholds are two numbers on purpose. Right now that only lives in the code and in
  this entry.

Committed on `feature/f-7-patient-search` and pushed to `origin`. **Not merged into `main`; `main`
was not modified; nothing was force-pushed and no branch or worktree was deleted.**
Next: `verification-pms` — and it can now verify against `main` as the baseline rather than the F-5
branch, since F-4, F-5 and F-6 are all on `main` and in this branch.
