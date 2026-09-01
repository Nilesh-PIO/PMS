# Patient Management Application — Implementation Readiness Verification

- **Verifies:** whether `implementation-pms` can actually start real feature work in this repo/environment right now — repo state, plan freshness, worktree isolation, stack tooling, per-feature build readiness, and whether my own hand-off discipline matches the current plan
- **Grounded in:** `BRD/Doc_BRD.md` (198 lines); `doc/brainstorm-pms-verification.md` (**modified, uncommitted on disk** — see §1); `doc/planning-pms-verification.md` (**modified, uncommitted on disk** — see §1); `doc/implementation-progress.md` (**does not exist yet**)
- **Date:** 2026-08-20
- **Scope:** Phase 1 only (single general physician, single clinic)
- **Status:** Readiness verification only. **No feature was implemented — no application code, entity, migration, component, or test was written anywhere, and nothing was committed.** The only file this run creates inside the repo is this report. Other artefacts were a temporary worktree/branch (created and removed inside the check) and two throwaway probe files in the session scratchpad **outside the repo**.
- **Supersedes:** the prior contents of this file in full. This is a re-check against my current system prompt (`Built & Verified` is now `verification-pms`'s call, not mine) and against the **refreshed plan on disk, whose Feature IDs are now `F-1..F-21` with `Q-` / `RSK-` / `REC-` prefixes**. No finding is carried over from a previous run without being re-established today.

---

## 1. Repo & plan state

- **Git repository:** yes — `C:\Users\NileshMalviya\source\repos\Hospital-managment` is a valid repo.
- **Branch:** `main`, at `cccb356 Add BRD, brainstorm/planning verification docs, and PMS agent definitions` (only two commits exist: `4cddd0f`, `cccb356`).
- **Origin:** `https://github.com/Nilesh-PIO/PMS` (fetch and push). `origin/main` tracked; `origin/HEAD → origin/main`.
- **Git identity configured:** `Nilesh Malviya <Nilesh.Malviya@programmers.io>` — commits inside a feature worktree will succeed without prompting.
- **Working tree: not clean.** `git status --porcelain`:

| File | State |
|---|---|
| `.claude/agents/worktree-pms.md` | Modified, unstaged |
| `doc/brainstorm-pms-verification.md` | **Modified, unstaged** |
| `doc/planning-pms-verification.md` | **Modified, unstaged** |
| `.claude/agents/implementation-pms.md` | Untracked |
| `.claude/agents/verification-pms.md` | Untracked |
| `doc/implementation-pms-verification.md` | Untracked (this report) |
| `doc/verification-pms-verify.md` | Untracked |
| `doc/worktree-pms-verification.md` | Untracked |

- **Tracked at HEAD (8 files only):** `.claude/agents/{brainstorm-pms,planning-pms,worktree-pms}.md`, `.gitignore`, `BRD/Doc_BRD.md`, `README.md`, `doc/brainstorm-pms-verification.md`, `doc/planning-pms-verification.md`.
- **`BRD/Doc_BRD.md` is clean at HEAD** — the source-of-truth requirement document is identical on disk and in any worktree. That is the only one of my three grounding documents of which this is true.

### 1.1 Does the plan on disk match HEAD? No — and **both** grounding documents have drifted

Unlike the last time this check was run, it is not only the plan. **Both** `doc/planning-pms-verification.md` and `doc/brainstorm-pms-verification.md` are uncommitted, and both were rewritten under new ID schemes.

| | On disk (working tree) | At HEAD `cccb356` |
|---|---|---|
| `planning-pms-verification.md` blob | `e5c55a5086da8878e19f102a719229dd04fc5c10` | `8d6f6307e6fef6d59f9ae29284c2e367e9623d7e` |
| Size / lines | 104,601 bytes / 1,013 lines | 125,853 bytes / 1,213 lines |
| Feature IDs | **F-1 .. F-21** | **F-1 .. F-22** |
| Open-question prefix | **`Q-1..Q-16`** | **`OQ-1..OQ-17`** |
| Risk / recommendation prefixes | **`RSK-`, `REC-`** | neither prefix appears at all (0 occurrences) |
| Diff vs HEAD | **1,484 changed lines** | — |
| `brainstorm-pms-verification.md` size / lines | 70,843 bytes / 568 lines | 80,590 bytes / 704 lines |
| Brainstorm ID scheme | `C-1..C-49`, **`E-1..E-66`**, **`RSK-1..RSK-15`**, **`REC-1..REC-19`**, **`Q-1..Q-16`** | `C-1..C-41`, **`EC-1..EC-75`**, **`RISK-1..RISK-22`**, **`R-1..R-24`**, **`OQ-1..OQ-19`** |
| Brainstorm diff vs HEAD | **978 changed lines** | — |

**Both documents claim the same date line.** The on-disk plan's line 5 reads `- **Date:** 2026-08-18`, and so does HEAD's. The on-disk plan is identifiable as the newer one only from its `Grounded in:` line ("refresh dated 2026-08-20") and its `Supersedes:` line. **The date header cannot be used to tell which plan you are holding** — that matters directly for §1.2.

**The Feature IDs collide while meaning different things.** Concretely, across the same problem space:

| ID | Means on disk (current plan) | Means at HEAD (superseded plan) |
|---|---|---|
| F-1 | Solution scaffolding, app shell, error contract (**Ready**) | Solution skeleton, configuration, error handling, clinic clock |
| F-6 | Duplicate detection at registration + `merged_into` pointer | ClinicProfile + first-run setup gate |
| F-8 | Patient edit + deactivate (no hard delete) | Patient search + recent patients |
| F-13 | **Medications** | **Consultation draft lifecycle + autosave + concurrency guards** |
| F-15 | Visit amendments (append-only) | Complaints, diagnosis, medications + pre-finalize review |
| F-20 / F-21 | **Blocked** — backup/encryption (Q-1, Q-12) and credential recovery (C-44) | Export CSV/PDF; Backup + visible backup status |
| F-22 | *does not exist* | Retention & deletion policy enforcement |

The edge-case IDs the plan's test strategy cites (`E-41`, `E-47`, `E-62` …) **do not exist in the HEAD brainstorm at all** — that document numbers them `EC-`. So a build driven by the HEAD copies would label branches and commits under Feature IDs that mean something else, and every "covers edge case E-nn" claim in a test would be unresolvable against the brainstorm doc sitting beside it.

### 1.2 Practical consequence for a worktree cut right now — confirmed empirically today

`git worktree add` branches from a **commit**, never from a dirty working tree. The live worktree created during this run (§2) contained **exactly the 8 files tracked at `cccb356`**, verified by `git ls-files` inside it and diffed against the main tree's tracked list — i.e. the **superseded F-1..F-22 / OQ- / EC- documents**, byte-for-byte, and none of the untracked agent definitions or verification reports.

**So: if I cut a worktree today and — as my operating rules tell me to — treated that worktree path as the root for every file operation including reading the spec, I would build against the superseded plan.** Worse, because the date line is identical (§1.1), the usual sanity check ("is this the 2026-08-20 plan?") would pass while I held the wrong document. This is the single highest-severity finding in this report, and one commit on `main` fixes it. **Committing to `main` is the user's action, not mine** — my rules forbid me from merging or committing to the main branch.

**Mitigation if the user prefers not to commit yet:** I can read the plan and brainstorm from the **main tree's** absolute paths while writing all code to the **worktree's** absolute path. That works, but it splits my read-root from my write-root, which is exactly the kind of quiet inconsistency the isolation rule exists to prevent, and it leaves the worktree's own copy of the spec wrong for `verification-pms`, which reads acceptance criteria independently. I would rather have the commit.

---

## 2. Isolated worktree from `worktree-pms` — actually exercised today

I spawned `worktree-pms` and asked it to create and verify an isolated worktree for a test feature named `readiness-check`, then to remove it as explicit cleanup. What actually happened:

- **`EnterWorktree`: failed**, with the pinned-cwd subagent error — *"EnterWorktree cannot create a worktree from a subagent with a cwd override … it would mutate the parent session's process-wide working directory. To work in a different directory (including a worktree), spawn an Agent with cwd set to it."* **This is the established normal path, not a regression.**
- **Manual fallback: succeeded.** `git worktree add` from `main` at `cccb356`.
- **Usable path and branch received back:**
  - Absolute path: `C:\Users\NileshMalviya\source\repos\Hospital-managment-readiness-check` (a *sibling* of the repo, outside it)
  - Branch: `feature/readiness-check-f-worktree-verify`
  - **The pre-write check I am required to run — "worktree path ≠ main working tree path" — passes cleanly on this shape.** The two paths are unambiguously different strings, and `git worktree list` showed two entries on two distinct branches at the same commit.
- **Main tree unaffected:** `git rev-parse --abbrev-ref HEAD` / `git rev-parse HEAD` in the main tree still returned `main` / `cccb356` after creation, and the pre-existing uncommitted changes were untouched.
- **No application code written into it by either of us.** `git ls-files` in the worktree returned the same 8 tracked files as HEAD — reported as `IDENTICAL - no extra files` — and status was `working tree clean`. `worktree-pms` wrote nothing (it cannot, by design); I wrote nothing. This run produced no `.cs`, `.tsx`, `.csproj`, migration, or test file anywhere on disk.
- **Cleanup completed as explicitly instructed:** `git worktree remove` + `git branch -d feature/readiness-check-f-worktree-verify` → `Deleted branch feature/readiness-check-f-worktree-verify (was cccb356)`.
- **Post-cleanup state re-verified by me independently, not just reported to me:** `git worktree list` → only the main tree; `git branch -a` → `* main` plus the two remote refs; the sibling directory `Hospital-managment-readiness-check` no longer exists; the main tree's 8 modified/untracked entries are unchanged.

**Verdict on the isolation mechanism: working end to end.** The `EnterWorktree` failure costs nothing — the manual fallback is hardened and produced a correct, isolated, verified worktree, and removal was complete. The isolation machinery is not what is blocking me; the **content** it isolates (§1.2) is.

---

## 3. Stack tooling present

| Tool | Status | Version / detail (measured today) |
|---|---|---|
| `dotnet` SDK | **Present** | `10.0.302` — the only SDK installed |
| .NET runtimes | Present | `Microsoft.NETCore.App` 8.0.28 / 8.0.29 / 9.0.18 / 10.0.10; **`Microsoft.AspNetCore.App` 10.0.10 only** |
| `dotnet-ef` global tool | **ABSENT** | `dotnet tool list --global` returns an empty table (header rows only) |
| `dotnet-ef` availability | Reachable | `dotnet tool search dotnet-ef` → `dotnet-ef 10.0.11, Microsoft, verified` |
| `node` | Present | `v22.23.1` |
| `npm` | Present | `10.9.8` |
| `git` | Present | `2.55.0.windows.2` |
| SQL Server | **Present via LocalDB** | `(localdb)\MSSQLLocalDB`, **State: Running**; live query returned `Microsoft SQL Server 2019 (RTM) - 15.0.2000.5 (X64)` |
| `sqlcmd` | Present | `15.0.1300.359` |
| SSMS | Present | `C:\Program Files\Microsoft SQL Server Management Studio 22` |
| NuGet feed | **Reachable and proven** | `nuget.org` + VS offline packages registered; a scratch `dotnet new webapi` **restored and built successfully** (`Build succeeded`, 0 errors, `net10.0`) |
| npm registry | Reachable | `npm ping` → `PONG 613ms` |
| Playwright browsers | **Absent** | not installed; `npx playwright install` not yet run |
| Disk | Fine | 303 GB free on `C:` |

**What would block or shape my first real build step:**

1. **`dotnet-ef` is not installed — this blocks F-1's definition of done.** F-1 §2 names migration `InitialCreate`, and acceptance criterion 2 is literally `dotnet ef migrations add InitialCreate …` plus `database update` creating the database visible in SSMS. Fix: `dotnet tool install --global dotnet-ef` (feed proven reachable, tool proven available at 10.0.11). Under a minute — but it is a machine-level change, so I would rather the user runs it or explicitly approves it than have me silently mutate the global tool set.
2. **Only the .NET 10 SDK and only the ASP.NET Core **10** shared runtime exist; the plan never pins a target framework.** Targeting `net8.0`/`net9.0` would restore and build, but `WebApplicationFactory<Program>` integration tests (plan §8) need the ASP.NET Core shared framework at that version, which is **not installed for 8 or 9**. I would proceed with `// ASSUMPTION (plan §2 Architecture, no TFM stated): TFM = net10.0 across all four projects + EF Core 10.x` — flagged, not asked, because every reasonable choice here yields the same downstream code shape.
3. **Plan §2 pins React 18; `npm create vite@latest --template react-ts` today scaffolds React 19.** I would pin React 18 explicitly in `package.json` per the plan rather than silently accepting 19, and note it. Flagged, not overridden.
4. **Playwright browsers are not installed.** F-1's test strategy names `PMS.E2E/app-shell.spec.ts`, and §8 requires Chromium/Edge/WebKit coverage (BRD L192). `npx playwright install` is a several-hundred-MB first-run download inside F-1 — not a blocker, but real elapsed time.
5. **LocalDB is what exists (SQL Server 2019, 15.0), not a standalone instance.** Plan §8 explicitly targets LocalDB for integration tests, so this is aligned rather than a deviation. Note the version gap: EF Core 10 against SQL Server 2019 is supported, but `UseSqlServer` compatibility-level-sensitive features must not be assumed. No action for F-1.

---

## 4. Scaffolding state — nothing exists yet

- Repo root contains exactly: `BRD/`, `doc/`, `README.md`, `.gitignore`, `.claude/`, `.git/`.
- **`backend/` — does not exist.** **`frontend/` — does not exist.** No `*.sln`, `*.csproj`, or `package.json` anywhere.
- **`.gitignore` already anticipates the planned layout** — `backend/**/bin|obj`, `frontend/node_modules|dist|build`, `.vs/`, `*.env`, `appsettings.*.local.json`. Useful, and it means no ignore work is needed before the first build — **with one gap**, see G-5 in §7: the plan has `npm run build` emit the SPA into `backend/src/PMS.Api/wwwroot`, which **is not ignored**, so the built bundle would land in git as tracked source unless an ignore rule is added in F-1.
- **`doc/implementation-progress.md` does not exist.** Per my operating rules I create it from the template as the very first action of the first real run, then update it in place forever after (append-only log). Its absence is expected at this stage, not a defect.

**Conclusion: true greenfield.** F-1 is genuinely unbuilt; there is no partial scaffolding to resume from, reconcile, or accidentally overwrite.

---

## 5. Plan readiness cross-check — all 21 Feature IDs

Straight from §5 of the **on-disk** plan (the current one). "Startable today" applies my own discipline: a feature is startable only if (a) its readiness tag permits it **and** (b) every dependency is already `Built & Verified`. Nothing is built, so (b) binds for 20 of 21.

| ID | Feature | Depends on | Effort | Plan readiness | Startable today, without stopping to ask? |
|---|---|---|---|---|---|
| F-1 | Solution scaffolding, app shell, health check, error contract | — | M | **Ready** | **YES — the only one.** No `Q-` touches it; no dependencies |
| F-2 | Login, session policy, idle screen lock | F-1 | L (M after session call) | Needs decision (**C-44 / REC-11 — no `Q-` exists**) | No — F-1 unbuilt. Then buildable against the stated assumption (5-min idle lock, 12-h absolute session, 12-char password, no rotation) |
| F-3 | ClinicProfile + first-run setup gate | F-1, F-2 | L (S after Q-4) | Needs decision (**Q-4**) | No — dependency-gated. Assumption: §4 fields, PNG signature ≤ 200 KB, no print until `IsSetupComplete` |
| F-4 | Doctor-configured settings (gender list, vitals reasons, plausibility ranges) | F-3 | L (S after Q-9, Q-10) | Needs decision (**Q-9, Q-10**) | No — dependency-gated. **Vitals thresholds stay blank/doctor-supplied — I will not invent clinical ranges** (E-12, REC-3) |
| F-5 | Patient registration & profile | F-1, F-2, F-4 | L (M after Q-7, Q-16, Q-9) | Needs decision (**Q-7, Q-16, Q-9**) | No — dependency-gated. Assumptions: phone optional-but-prompted; DOB else approx age + recorded-on |
| F-6 | Duplicate detection + `merged_into` pointer | F-5 | L (M after Q-13) | Needs decision (**Q-13**) | No — dependency-gated. Assumption: similarity ≥ 0.85 AND (phone OR DOB); **warn, never block** |
| F-7 | Patient search, recent patients, disambiguating picker | F-5 | L (M after search-semantics call) | Needs decision (**C-22 / C-35 — no `Q-` exists**) | No — dependency-gated. Assumption: substring + digits-only phone incl. last-4, fuzzy fallback |
| F-8 | Patient edit + deactivate (no hard delete) | F-5, **F-17** | L (M after Q-6) | Needs decision (**Q-6**) | No — dependency-gated (note: also needs the audit feature F-17) |
| F-9 | Appointments: scheduling, daily list, status machine, walk-in start | F-5, F-7 | L (M after Q-5, Q-14) | Needs decision (**Q-5, Q-14**) | No — dependency-gated. Also carries the time-model assumption (**C-24 — no `Q-` exists**) |
| F-10 | Visit lifecycle: draft, autosave, resume, single-tab lock, finalize | F-9 | L (L after Q-3) | Needs decision (**Q-3**) | No — dependency-gated. The largest single feature in the plan |
| F-11 | Vitals capture — mandatory-or-reason | F-10, F-4 | L (M after Q-2, Q-10) | Needs decision (**Q-2, Q-10**) | No — dependency-gated. Assumption: null + explicit reason, never a sentinel value (E-18) |
| F-12 | Complaints & diagnosis capture | F-10 | L (S after Q-8) | Needs decision (**Q-8**) | No — dependency-gated. Carries the plan's one stated deviation (text columns on `Visit`, not child rows) |
| F-13 | Medications | F-10 | L (M after required-subset call) | Needs decision (**C-31 / E-22 — no `Q-` exists**) | No — dependency-gated. Assumption: Name + Dosage required, rest optional |
| F-14 | Prescription generation, print, reprint | F-3, F-11, F-12, F-13 | L (M after Q-4, Q-8) | Needs decision (**Q-4, Q-8**) | No — dependency-gated (four deps). Also carries the QuestPDF licence item (§9 #22) |
| F-15 | Visit amendments (append-only) | F-14 | L (M after Q-3) | Needs decision (**Q-3**) | No — dependency-gated |
| F-16 | Patient history + date filter | F-10, F-14, F-15 | M | **Ready** (upstream-gated only) | No — three unbuilt dependencies. Readiness-clean, but deep in the chain |
| F-17 | Audit trail (six event types) | F-1 | L (M after audit acceptance) | Needs decision (**REC-9 / C-48 — no `Q-` exists**) | No — F-1 unbuilt. **Shallowest feature after F-1** (depends on F-1 alone) and F-8 needs it |
| F-18 | Export CSV / PDF | F-14, F-16, F-17 | L (M after Q-11) | Needs decision (**Q-11**) | No — dependency-gated. Assumption: visit/patient/date-range only, **no whole-DB export** |
| F-19 | Keyboard-first input + performance instrumentation | F-10, F-11, F-12, F-13 | L (S after Q-15) | Needs decision (**Q-15**) | No — dependency-gated |
| F-20 | Backup, restore rehearsal, backup-status indicator, encryption at rest | F-1 | **L** | **Blocked (Q-1, Q-12)** | **NO — readiness-gated.** I will not build it, will not substitute a default backup story, and will not invent a deployment model. Its only build dependency (F-1) clears long before the block does |
| F-21 | Credential recovery, lockout policy | F-2 | **L** | **Blocked (C-44 — brainstorm §12 carries no question for it)** | **NO — readiness-gated.** Will not be defaulted; the plan itself flags that §12 needs this item added |

**Summary:** **exactly one feature — F-1 — is startable today.** Two are readiness-`Ready` (F-1, F-16); **seventeen** are `Needs decision` but buildable against explicit stated assumptions once dependencies land; **two are `Blocked` and stay blocked here** (F-20 → Q-1 + Q-12; F-21 → C-44).

**On my "never advance past a `Blocked` feature's dependents" rule:** per plan §5, **nothing is downstream of F-20 or F-21** — they are go-live gates, not build gates. So the two blocks do not stall my queue, and working F-1 → F-2 → F-3 → F-5 → … does not trip that rule. I will hold both at `Blocked` in the tracker and re-raise them at go-live rather than letting them quietly become "trailing work": shipping without F-20 means real patient data with no rehearsed restore (E-50) and no decided encryption-at-rest story (C-45), and without F-21 one forgotten password locks the clinic out of every record it owns.

### 5.1 Two things in the plan I am flagging rather than quietly working around

1. **The stated critical path contains links that are not dependencies.** §1 and §5 both give `F-1 → F-2 → F-3 → F-5 → F-9 → F-10 → F-11 → F-13 → F-14 → F-15 → F-17`. But per the §5 table, **F-13 depends on F-10, not F-11**, and **F-17 depends on F-1 alone** — so the `F-11 → F-13` and `F-15 → F-17` links are not real blocking edges. Meanwhile **F-18 (depends on F-14, F-16, F-17)** is the genuine tail and is not on the stated path. Practical effect: the stated path over-serialises the middle and understates the end. This does not change what I build first (F-1 either way), so I am flagging it, not overriding it.
2. **Feature-count inconsistency in the plan's own header.** The header and §1 say *"Fifteen features are buildable today behind a stated default"* / *"Fifteen carry an explicit `Assumption:` line"*, but the §5 table has **seventeen** `Needs decision` rows (F-2..F-15, F-17, F-18, F-19). The tag on each individual feature is unambiguous, so this is a counting slip in the summary, not a readiness ambiguity — but it should be corrected so the headline number is trustworthy.

Minor, non-blocking nits in F-1 I would resolve myself without asking: the database is called `PmsDb` in F-1 §2 and `PMSDb` in acceptance criterion 2 (I would use one name and record it), and the acceptance criterion's `dotnet ef … -p PMS.Infrastructure -s PMS.Api` omits the `backend/src/` path prefix implied by §3.

---

## 6. Definition-of-ready-for-verification — internal consistency check

Re-checked my six-point checklist against what the current plan actually demands, specifically to confirm I am **not** about to self-declare `Built & Verified` (no longer my call).

| My checklist item | Does the current plan support it? | Notes |
|---|---|---|
| Code at the plan's file targets | **Yes** | Plan §3 gives a complete end-of-Phase-1 tree; each feature §4 names exact `*.tsx`/service/controller files. F-1's targets are explicit down to `shared/api/httpClient.ts` |
| Architecture conventions honoured | **Yes** | §2 is unambiguous: DTOs ≠ EF entities, controllers never touch `PmsDbContext`, folder-per-feature React, TanStack Query throughout, cookie auth (not JWT-in-storage), server-side QuestPDF |
| Tests exist per the plan's strategy | **Yes, with one addition to my own wording** | Plan §8 fixes tooling: xUnit + NSubstitute + FluentAssertions; `WebApplicationFactory<Program>` on LocalDB; **Vitest** + RTL (explicitly chosen over Jest); **Playwright E2E as a fourth layer**. My checklist names three layers; F-1 alone requires a Playwright smoke spec, so **I will treat E2E as part of "tests exist" wherever the plan's coverage table marks `Y`** |
| Tests pass, run by me, with real output | **Yes** | Nothing in the plan conflicts. Needs `dotnet-ef` (§3 gap 1) and Playwright browsers (§3 gap 4) installed before F-1's suite can actually be green |
| Acceptance criteria met line by line | **Yes** | Every feature §7 is a checkbox list; F-1's five are concrete and mechanically checkable |
| Tracker updated before the step is finished | **Yes** | `doc/implementation-progress.md` does not exist yet; I create it first, then update in place, append-only |
| **Hand-off:** I stop at `Awaiting verification` | **Consistent** | `verification-pms` independently re-runs the suite, re-checks acceptance criteria against evidence, and is the **sole** authority for `Built & Verified` — including bouncing to `Needs rework`. **I set every status value except that one.** Nothing merges, and no dependent feature starts, until it passes |

**No conflict found.** One clarification I am adopting explicitly: the plan's coverage table marks F-20 and F-21 as `Blocked` at *every* test layer, so "tests exist" is unsatisfiable for them by construction — which is another reason they cannot reach `Awaiting verification` and must stay `Blocked` rather than being attempted with partial coverage.

---

## 7. Gaps that would stop the first real implementation task

| # | Gap | Severity | Blocks F-1? | Fix / owner |
|---|---|---|---|---|
| G-1 | **Both grounding docs are uncommitted; HEAD holds ID-incompatible predecessors (F-1..F-22 / OQ- / EC-).** A worktree cut now silently delivers the wrong spec — confirmed live today (§1.2) — and the identical date lines defeat the obvious sanity check | **Critical** | **Yes** | Commit `doc/planning-pms-verification.md` + `doc/brainstorm-pms-verification.md` to `main`. **User's action — I do not commit to `main`.** Fallback in §1.2 exists but is worse |
| G-2 | `dotnet-ef` global tool absent; F-1's acceptance criteria require `migrations add InitialCreate` and `database update` | **High** | **Yes** | `dotnet tool install --global dotnet-ef` (10.0.11 available, feed proven). Machine-level change — user's approval preferred |
| G-3 | No TFM in the plan; only the .NET 10 SDK and only the ASP.NET Core **10** shared runtime are installed, so net8/net9 targets would break `WebApplicationFactory` tests | Medium | Yes, silently | Proceed with `ASSUMPTION: net10.0 + EF Core 10.x`, flagged in the F-1 report. Reversible, no design fork |
| G-4 | Plan §2 pins **React 18**; today's Vite template scaffolds **React 19** | Low | No | Pin React 18 in `package.json` per the plan; flagged, not silently upgraded |
| G-5 | **`.gitignore` does not ignore `backend/src/PMS.Api/wwwroot`**, yet F-1 acceptance criterion 5 has `npm run build` emit the SPA bundle there — the build output would be committed as tracked source | Medium | Yes, silently | Add an ignore rule for the emitted bundle as part of F-1 and note it in the F-1 report |
| G-6 | Playwright browsers not installed; F-1 requires `PMS.E2E/app-shell.spec.ts` and §8 requires 3 browser engines | Low | No | `npx playwright install` during F-1; meaningful first-run download time |
| G-7 | `doc/implementation-progress.md` does not exist | Low | No | I create it from the template as the first action of the first real run |
| G-8 | Plan internal inconsistencies: critical path contains non-dependency links; "fifteen" vs seventeen `Needs decision`; `PmsDb`/`PMSDb`; ef-command paths omit `backend/src/` (§5.1) | Low | No | Flagged for the plan owner; the `PmsDb` and path nits I resolve myself and record |
| G-9 | QuestPDF licence tier unconfirmed (plan §9 #22) | Low now | No | Gates F-14/F-18, not F-1. Confirm before go-live |
| G-10 | **F-20 and F-21 have zero build and therefore zero test coverage at any layer**, while carrying four Critical edge cases between them (E-48, E-49, E-50, E-54) — the plan calls this "the most important row in the table" | High at go-live | No | Owner decisions: Q-1 + Q-12 (deployment model, RPO/alerting) and C-44 (credential recovery/lockout, which brainstorm §12 does not yet ask). One decision session clears both plus most assumption-gated features |

**Not gaps:** git identity is configured; the worktree/isolation path works end to end including cleanup; NuGet restore + build proven with a real scratch project; npm registry answers; LocalDB 2019 is running and query-verified; SSMS 22 installed; 303 GB free; `.gitignore` otherwise matches the planned layout; the BRD is clean at HEAD.

---

## Verdict

**Not ready to start real implementation this minute — but everything standing in the way is small, known, and mostly not mine to do.** The environment is genuinely in good shape: `worktree-pms` produced a real isolated worktree today (`EnterWorktree` failing to its hardened manual fallback exactly as expected), handed back a usable absolute path and branch, no application code was written into it by either of us, and cleanup left the repo byte-identical to its prior state; .NET 10, Node 22.23.1, npm 10.9.8, git 2.55, LocalDB (SQL Server 2019, running) and SSMS 22 are all present, both package feeds answer, and a scratch `dotnet new webapi` restored and built cleanly. Two things must happen before I cut a real branch, and the first is the user's call, not mine: **(1) commit `doc/planning-pms-verification.md` and `doc/brainstorm-pms-verification.md` to `main`** — this is correctness, not housekeeping, because a worktree created right now was empirically confirmed today to contain the superseded F-1..F-22 / `OQ-` / `EC-` documents in which `F-13` means "consultation draft lifecycle" rather than "Medications" and in which the `E-nn` edge-case IDs my tests are supposed to cite do not exist at all, and because both files carry the identical `Date: 2026-08-18` header so the mistake would not announce itself; and **(2) `dotnet tool install --global dotnet-ef`**, without which F-1's `InitialCreate` migration and its `database update` acceptance criterion cannot be satisfied and F-1 could never legitimately pass the gate. With those two done, the concrete first feature I take on is **F-1 — Solution scaffolding, app shell, health check, error contract**: the only `Ready` feature with no dependencies, built to its five acceptance criteria with xUnit unit + `WebApplicationFactory` integration + Vitest + a Playwright smoke spec, carrying `ASSUMPTION: net10.0 / EF Core 10.x` (G-3), the React-18 pin (G-4) and the `wwwroot` ignore rule (G-5) flagged in its report rather than silently absorbed — and I take it only as far as **`Awaiting verification`**, committed in its own worktree with `doc/implementation-progress.md` created and updated, then hand it to **`verification-pms`**, which alone decides whether it becomes `Built & Verified`. F-20 and F-21 stay `Blocked` and untouched; since nothing is downstream of them, they gate go-live, not the build queue.
