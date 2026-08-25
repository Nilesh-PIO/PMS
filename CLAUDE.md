# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

This repository currently contains **no application code** — no `backend/`, no `frontend/`, no `.sln`/`.csproj`/`package.json` anywhere. It holds the requirements and a multi-agent planning/build pipeline for a Patient Management Application (PMS) for a single-physician clinic, defined in `BRD/Doc_BRD.md`.

There are no build, lint, or test commands yet because nothing has been scaffolded. Once `implementation-pms` (see below) builds the first feature, the real commands will live under `backend/` (`dotnet build`, `dotnet test`) and `frontend/` (`npm run build`, `npm test`) — don't invent commands before that scaffolding exists.

## Architecture: the PMS agent pipeline

The core of this repo is a sequence of purpose-built subagents in `.claude/agents/*-pms.md`, each owning one narrow stage. Understanding the pipeline requires reading multiple agent files together — no single file explains the whole flow:

1. **`brainstorm-pms`** — reads `BRD/Doc_BRD.md`, produces exhaustive edge-case/data-integrity analysis. Output: `doc/brainstorm-pms-verification.md`.
2. **`planning-pms`** — turns the BRD + brainstorm findings into a concrete implementation plan (Feature IDs, dependency map, effort, acceptance criteria, test strategy) for the fixed stack below. Output: `doc/planning-pms-verification.md`.
3. **`implementation-pms`** — the primary builder. Reads the plan, decides the next buildable feature (respecting the dependency map and each feature's Readiness tag), writes the actual React/ASP.NET Core/EF Core code and tests itself. Does not delegate implementation. Tracks progress in `doc/implementation-progress.md` (not yet created — first appears on the first real build).
4. **`worktree-pms`** — pure git infrastructure. The *only* agent that creates/removes git worktrees and branches; every other agent that needs isolation asks this one rather than running `git worktree` itself. Never touches application code.
5. **`verification-pms`** — independent functional gate. Re-runs the full test suite itself (never trusts a builder's report), and is the sole authority that sets a feature's status to `Built & Verified`.
6. **`code-review-pms`** — independent quality gate, runs after `Built & Verified`. Reviews correctness-beyond-tests, quality, consistency, and security; is the sole authority that sets `Reviewed`. Routes Must-fix findings back to `implementation-pms` and re-reviews, capped at 3 rounds.
7. **`finishing-pms`** — the only agent allowed to merge, push, or (via `worktree-pms`) delete a worktree, and only after a fresh explicit per-action confirmation. Presents merge / open-PR / clean-up as options once a feature is `Reviewed`.
8. **`gap-analysis-pms`** — periodic whole-BRD audit (not per-feature). Scores implementation coverage against the original BRD requirements directly, binary per requirement, no partial credit. Gate threshold is 95%; below that, gaps are routed back to `implementation-pms`, `planning-pms`, or the product owner depending on why the gap exists.

**Status vocabulary** used in `doc/implementation-progress.md`: `Not Started` → `In progress` → `Awaiting verification` → `Built & Verified` (verification-pms) → `Reviewed` (code-review-pms) → `Merged`/`PR opened` (finishing-pms), with `Needs rework` and `Blocked` as off-ramps back to `implementation-pms` or the product owner.

Each agent also has its own readiness/self-check report in `doc/<agent-name>-verification.md` — these were produced by actually running the agent against the real repo state (spawning `worktree-pms`, checking real tooling, etc.), not written by hand, and are useful evidence of what does/doesn't currently work in this environment (see Known gotchas below).

## Source-of-truth documents

- `BRD/Doc_BRD.md` — the original requirements. Every other document derives from this; when in doubt, this wins.
- `doc/brainstorm-pms-verification.md` — BRD coverage map, edge cases, open questions, and the data-integrity analysis that `planning-pms` builds on.
- `doc/planning-pms-verification.md` — the actual build spec: Feature IDs, dependency map, per-feature data model/API/frontend design, acceptance criteria, and test strategy. `implementation-pms` builds *from this plan*, not by re-deriving from the BRD each time.

These three documents have repeatedly drifted from what's actually committed to `main` during this project's history (uncommitted on-disk edits vs. a stale committed version with an incompatible Feature-ID scheme, even a case where the committed plan specified Angular against this repo's fixed React stack). **Before trusting any of these docs, check `git status` and confirm you're reading the version you think you're reading** — a worktree cut from `HEAD` will silently carry whatever was last committed, not what's on disk.

## Fixed technology stack

Not a default — an explicit, repo-wide decision, stated in every `-pms` agent:

- **Frontend:** React (TypeScript), folder-per-feature under `frontend/src/features/<feature>/`.
- **Backend:** ASP.NET Core Web API, layered Controller → Service → Repository/EF Core.
- **Database:** SQL Server, managed via SSMS.
- **Data access:** EF Core, Code-First with named migrations.

Full conventions (solution layout, DTO/entity separation, auth approach, test tooling) are in `doc/planning-pms-verification.md`'s architecture overview once it exists — don't redecide architecture per feature.

## Known environment gotchas

Discovered empirically by the agents' own readiness checks (see `doc/*-verification.md`), not assumptions:

- **`EnterWorktree` fails when invoked from a subagent with a pinned working directory** ("cannot create a worktree from a subagent with a cwd override") — this is the normal path for every `-pms` agent, not an edge case. `worktree-pms` falls back to a manual `git worktree add -b <branch> <sibling-path>` cycle; this is expected behavior, not a bug to fix.
- **`dotnet-ef` global tool is not installed** in this environment — blocks any EF Core migration step until `dotnet tool install --global dotnet-ef` is run.
- **`gh` CLI is not installed** — blocks `finishing-pms`'s "create a PR" option until installed and authenticated.
- **Only the .NET 10 SDK/runtime is installed** (no 8 or 9) — relevant when a plan or feature doesn't state a target framework explicitly.
