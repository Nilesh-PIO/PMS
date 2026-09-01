# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

Application code now exists, under a top-level **`PMS/`** folder (`PMS/backend/`, `PMS/frontend/` — see the folder-root deviation noted under Fixed technology stack). It was scaffolded by `implementation-pms` as Feature F-1 (solution scaffolding, app shell, health check, error contract), independently re-verified by `verification-pms`, and is `Built & Verified` per `doc/implementation-progress.md`. Everything else in the plan (F-2 through F-21) is still `Not Started` or `Blocked`. `PMS/README.md` is the day-to-day reference for this code — prerequisites, first-time setup, and the command table below are sourced from it; check it directly before assuming a command has changed.

Real build/test commands now exist:
- Backend build: `dotnet build PMS/backend/PMS.sln`
- Backend tests (unit + integration): `dotnet test PMS/backend/PMS.sln`
- Frontend install: `cd PMS/frontend && npm install`
- Frontend unit tests: `cd PMS/frontend && npm test`
- Frontend production build: `cd PMS/frontend && npm run build`
- Run the API (serves the built SPA): `cd PMS/backend/src/PMS.Api && dotnet run`
- Frontend dev server (proxies `/api`): `cd PMS/frontend && npm run dev`
- E2E: `cd PMS/backend/tests/PMS.E2E && npm install && npm run install-browsers && npm test` — see the Playwright gotcha below before relying on this.

The rest of the repo still holds the requirements and the multi-agent planning/build pipeline that produced and gates this code, defined in `BRD/Doc_BRD.md`.

## Architecture: the PMS agent pipeline

The core of this repo is a sequence of purpose-built subagents in `.claude/agents/*-pms.md`, each owning one narrow stage. Understanding the pipeline requires reading multiple agent files together — no single file explains the whole flow:

1. **`brainstorm-pms`** — reads `BRD/Doc_BRD.md`, produces exhaustive edge-case/data-integrity analysis. Output: `doc/brainstorm-pms-verification.md`.
2. **`planning-pms`** — turns the BRD + brainstorm findings into a concrete implementation plan (Feature IDs, dependency map, effort, acceptance criteria, test strategy) for the fixed stack below. Output: `doc/planning-pms-verification.md`.
3. **`implementation-pms`** — the primary builder. Reads the plan, decides the next buildable feature (respecting the dependency map and each feature's Readiness tag), writes the actual React/ASP.NET Core/EF Core code and tests itself. Does not delegate implementation. Tracks progress in `doc/implementation-progress.md`, which now exists — F-1 is `Built & Verified`, F-20/F-21 are `Blocked`, the rest are `Not Started`.
4. **`worktree-pms`** — pure git infrastructure. The *only* agent that creates/removes git worktrees and branches; every other agent that needs isolation asks this one rather than running `git worktree` itself. Never touches application code.
5. **`verification-pms`** — independent functional gate. Re-runs the full test suite itself (never trusts a builder's report), and is the sole authority that sets a feature's status to `Built & Verified`.
6. **`code-review-pms`** — independent quality gate, runs after `Built & Verified`. Reviews correctness-beyond-tests, quality, consistency, and security; is the sole authority that sets `Reviewed`. Routes Must-fix findings back to `implementation-pms` and re-reviews, capped at 3 rounds.
7. **`finishing-pms`** — the only agent allowed to merge, push, or (via `worktree-pms`) delete a worktree, and only after a fresh explicit per-action confirmation. Presents merge / open-PR / clean-up as options once a feature is `Reviewed`.
8. **`gap-analysis-pms`** — periodic whole-BRD audit (not per-feature). Scores implementation coverage against the original BRD requirements directly, binary per requirement, no partial credit. Gate threshold is 95%; below that, gaps are routed back to `implementation-pms`, `planning-pms`, or the product owner depending on why the gap exists.

**Status vocabulary** used in `doc/implementation-progress.md`: `Not Started` → `In progress` → `Awaiting verification` → `Built & Verified` (verification-pms) → `Reviewed` (code-review-pms) → `Merged`/`PR opened` (finishing-pms), with `Needs rework` and `Blocked` as off-ramps back to `implementation-pms` or the product owner.

Each agent also has its own readiness/self-check report in `doc/<agent-name>-verification.md` — these were produced by actually running the agent against the real repo state (spawning `worktree-pms`, checking real tooling, etc.), not written by hand, and are useful evidence of what does/doesn't currently work in this environment (see Known gotchas below).

## Source-of-truth documents

- `BRD/Doc_BRD.md` — the original requirements. Every other document derives from this; when in doubt, this wins.
- `modules/00-overview.md` through `modules/08-authentication-authorization.md` — the same BRD, reorganized module-by-module (patient management, appointments, consultation workflow, patient history, search/navigation, data export, non-functional requirements, authentication/authorization) purely for easier navigation. **Nothing here overrides the BRD**; if a module file and `BRD/Doc_BRD.md` ever disagree, the BRD wins. Use `modules/00-overview.md`'s module index as the entry point rather than reading `Doc_BRD.md` linearly.
- `doc/brainstorm-pms-verification.md` — BRD coverage map, edge cases, open questions, and the data-integrity analysis that `planning-pms` builds on.
- `doc/planning-pms-verification.md` — the actual build spec: Feature IDs, dependency map, per-feature data model/API/frontend design, acceptance criteria, and test strategy. `implementation-pms` builds *from this plan*, not by re-deriving from the BRD each time.
- `doc/implementation-progress.md` — the running record of what's actually built, per Feature ID, against the status vocabulary below. Check this before assuming a feature is or isn't done; don't infer status from the plan alone.

These documents have repeatedly drifted from what's actually committed to `main` during this project's history (uncommitted on-disk edits vs. a stale committed version with an incompatible Feature-ID scheme, even a case where the committed plan specified Angular against this repo's fixed React stack). **Before trusting any of these docs, check `git status` and confirm you're reading the version you think you're reading** — a worktree cut from `HEAD` will silently carry whatever was last committed, not what's on disk.

## Fixed technology stack

Not a default — an explicit, repo-wide decision, stated in every `-pms` agent:

- **Frontend:** React (TypeScript), folder-per-feature under `PMS/frontend/src/features/<feature>/`.
- **Backend:** ASP.NET Core Web API, layered Controller → Service → Repository/EF Core.
- **Database:** SQL Server, managed via SSMS.
- **Data access:** EF Core, Code-First with named migrations.

Full conventions (solution layout, DTO/entity separation, auth approach, test tooling) are in `doc/planning-pms-verification.md`'s architecture overview.

**Folder-root deviation from the plan:** `doc/planning-pms-verification.md` §3 places the solution at repo-root `backend/`/`frontend/`. At the user's explicit direction, the application instead lives under a top-level **`PMS/`** folder — `PMS/backend/` and `PMS/frontend/`. This is the only change from the plan's layout: the four-project backend split (`PMS.Domain`, `PMS.Application`, `PMS.Infrastructure`, `PMS.Api`), Controller → Service → Repository/EF Core layering, DTO/entity separation, the folder-per-feature React structure, and the test project layout under `PMS/backend/tests/` all follow the plan as written. See `PMS/README.md` for the concrete solution/test-project map.

## Known environment gotchas

Discovered empirically by the agents' own readiness checks (see `doc/*-verification.md`) and by the F-1 build/verify/review cycle, not assumptions:

- **`EnterWorktree` fails when invoked from a subagent with a pinned working directory** ("cannot create a worktree from a subagent with a cwd override") — this is the normal path for every `-pms` agent, not an edge case. `worktree-pms` falls back to a manual `git worktree add -b <branch> <sibling-path>` cycle; this is expected behavior, not a bug to fix.
- **`dotnet-ef` global tool is still not installed** in this environment (re-confirmed as of the F-1 code review) — blocks any EF Core migration step until `dotnet tool install --global dotnet-ef` is run. This is a machine-level change every `-pms` agent has deliberately left for the user to approve/run rather than doing it silently.
- **`gh` CLI is not installed** — blocks `finishing-pms`'s "create a PR" option until installed and authenticated against the `origin` remote. Merge and worktree-cleanup are unaffected.
- **Only the .NET 10 SDK/runtime is installed** (no 8 or 9) — relevant when a plan or feature doesn't state a target framework explicitly.
- **Playwright's browser harness could not be proven to run on this host** during F-1 (a host process-spawn denial, not a code defect) — E2E specs exist but are unverified by real-browser execution here. This is a carried risk, not resolved: it must be closed out before **F-14** (printed prescription across Chrome/Edge/WebKit is a stated BRD compatibility requirement), and any `test.fixme` skip must be removed by **F-2**.
- **On this machine, `dotnet test` exits 0 on a zero-test project** and prints `Passed!` even in a run containing skipped tests — always read the `Total:`/`Skipped:` counts and reconcile them against the specs the plan names; don't trust the exit code alone.
- **Test suites must be run from inside their actual worktree path, not a Temp-tree copy** — assembly loading is denied from the Temp tree on this host, which will produce false failures on otherwise-good code.
- **Process note, not an environment gotcha:** F-1 was merged to `main` before `verification-pms` or `code-review-pms` ran (recorded as a sequencing violation in `doc/implementation-progress.md`). The pipeline's rule — only `finishing-pms` merges, and only after a feature reaches `Reviewed` — was bypassed once already; don't assume something on `main` has cleared its gates without checking `doc/implementation-progress.md`.
