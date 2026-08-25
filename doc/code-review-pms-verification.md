# Patient Management Application — Code Review Gate Readiness Verification

- **Verifies:** whether `code-review-pms`'s own gate mechanism — find the feature that is `Built & Verified` and unreviewed, inspect its worktree diff independently, run the four review dimensions against it, route Must-fix findings to `implementation-pms`, re-review after the fix, and be the sole writer of `Reviewed` — can actually operate in this repo/environment right now
- **Grounded in:** `BRD/Doc_BRD.md` (198 lines); `doc/planning-pms-verification.md` (on-disk copy, header dated 2026-08-18, **uncommitted**); `doc/brainstorm-pms-verification.md` (on-disk copy, **uncommitted**); `doc/implementation-progress.md` (**does not exist** — re-confirmed today); `doc/implementation-pms-verification.md` and `doc/verification-pms-verify.md` read as **claims about the pipeline, not as evidence about the code**
- **Date:** 2026-08-24
- **Scope:** Phase 1 only (single general physician, single clinic). Relevant here only as the source of F-1's acceptance criteria and the conventions my consistency dimension checks against
- **Status:** Readiness verification of the review gate itself. **No feature was reviewed, because none is `Built & Verified`.** No application code, test code, migration, plan content, brainstorm content, BRD content, or progress tracker was written or modified. The only file this run creates inside the repo is this report; the one synthetic artefact used to prove the diff mechanism (a throwaway worktree + branch, created outside the repo) was removed inside this check.

---

## 1. Is anything actually `Built & Verified` and awaiting review? No.

**Plainly: there is nothing for me to review, and I am not inventing something to review.**

`doc/implementation-progress.md` — the tracker I read to find review candidates and the only file I write status into — **does not exist**. Confirmed directly today:

```
$ ls doc/
brainstorm-pms-verification.md      gap-analysis-pms-verification.md
implementation-pms-verification.md  planning-pms-verification.md
verification-pms-verify.md          worktree-pms-verification.md
```

No tracker means no feature is in any status at all — not `Built & Verified`, not `Awaiting verification`, not `Needs rework`. Corroborating repo state, all checked rather than assumed:

```
$ ls
.claude/  .git/  .gitignore  BRD/  doc/  README.md      <- no backend/, no frontend/, no .sln, no package.json

$ git worktree list
C:/Users/NileshMalviya/source/repos/Hospital-managment  cccb356 [main]     <- one worktree, no feature worktree

$ git branch -a
* main
  remotes/origin/HEAD -> origin/main
  remotes/origin/main                                                      <- no feature branch

$ git log --oneline -2
cccb356 Add BRD, brainstorm/planning verification docs, and PMS agent definitions
4cddd0f Initial commit
```

This matches the pre-implementation state other agents' readiness reports recorded. Per my grounding rule, a real review run stops here. Everything below is a readiness check on the machinery, labelled as such, and **nothing below is a sign-off on F-1 or anything else**.

---

## 2. Mechanical capability check — can I inspect a diff independently?

This is the central mechanic: I review the actual diff, not another agent's summary of it. Since no feature worktree exists, I proved the mechanism against a **synthetic throwaway worktree**, then removed it. Every line below is real output from this session.

**Probe setup** (worktree created *outside* the repo, in the session scratchpad, on a throwaway branch off `HEAD`):

```
$ git -C <repo> worktree add -b throwaway/review-probe <scratchpad>/probe-wt HEAD
Preparing worktree (new branch 'throwaway/review-probe')
HEAD is now at cccb356 ...

$ git -C <repo> worktree list
C:/.../Hospital-managment            cccb356 [main]
C:/.../scratchpad/probe-wt           cccb356 [throwaway/review-probe]
```

A trivial C# file was committed inside the probe worktree (`synthetic/Probe.cs`, three lines, deliberately containing a reviewable defect — a `Run(string s)` method that dereferences an unguarded parameter).

| Capability I need in a real review | Command exercised | Result |
|---|---|---|
| See that a feature worktree exists and where | `git -C <repo> worktree list` | **Works** — lists both worktrees with path, SHA, branch |
| Read the feature's full patch from inside its worktree | `git -C <wt> show` | **Works** — full unified diff, `new file mode`, hunk headers, +/- lines |
| Scope the review (what changed, how much) | `git -C <wt> show --stat`, `git diff HEAD~1 --numstat` | **Works** — reported 1 file changed, 3 insertions |
| Review a whole branch against `main` **without** entering the worktree | `git -C <repo> diff main...throwaway/review-probe` | **Works** — same patch, run from the main repo |
| Get just the changed-file list, to drive a file-by-file pass | `git -C <repo> diff --name-only main...<branch>` | **Works** |
| Read a file at a branch tip with no checkout | `git -C <repo> show <branch>:synthetic/Probe.cs` | **Works** — printed file content |
| See uncommitted work the builder left behind (a real review hazard) | `git -C <wt> diff --stat`, `git -C <wt> status --short` | **Works** — showed the added dirty line and the modified-file marker |
| Read full files for context beyond the hunks | `Read` on the worktree path | **Works** — read the probe file including its uncommitted line |
| Search patterns across the worktree (`dangerouslySetInnerHTML`, `FromSqlRaw`, hardcoded connection strings) | `Grep` scoped to the worktree path | **Works** — matched the expected line with line number |
| git version | `git --version` | 2.55.0.windows.2 |

**Conclusion: the independence mechanic is proven, not assumed.** I can obtain the diff, the changed-file list, the file contents, and the builder's uncommitted leftovers first-hand, from a worktree that lives outside the repo directory, without any agent handing me a summary. Both review modes work — `git -C <worktree-path>` from within, and the `main...<branch>` range diff from the main repo.

**Cleanup, verified:**

```
$ git -C <repo> worktree remove --force <scratchpad>/probe-wt
$ git -C <repo> branch -D throwaway/review-probe
Deleted branch throwaway/review-probe (was 564995e).

$ git -C <repo> worktree list   -> only the main worktree
$ git -C <repo> branch -a       -> main + remotes only
$ git -C <repo> status --short  -> byte-identical to the pre-check listing (same 3 modified, same 8 untracked)
$ ls <scratchpad>/probe-wt      -> No such file or directory
```

One honest residue: the throwaway commit object `564995e` remains unreachable in the object database until git's normal garbage collection. It has no ref, no branch, no worktree, and is invisible to every command above. Nothing else was left behind.

**Note on the "never delete a worktree/branch" rule:** that rule protects *feature* worktrees, which are `worktree-pms`'s and the user's to manage. The artefact removed here was created by me, in this session, purely to prove the mechanism, and lived outside the repo. No feature worktree or branch was touched — there are none.

---

## 3. Loop mechanics — can I actually close the loop?

**Tooling check:** the `Agent` tool is available to me and `implementation-pms` is a registered agent type in this environment, so the routing hop is executable, not theoretical. I did **not** spawn it in this run — there is no real finding to fix, and spawning a builder with a hypothetical would be fabricating work.

**The loop as I would run it, per feature:**

1. Tracker says `Built & Verified`, no `Reviewed` note against it, so I review the diff.
2. **No Must-fix** — I edit `doc/implementation-progress.md`: status becomes `Reviewed`, plus a log entry of what I checked and every Should-fix/Nit, with any Should-fix the user or builder declines recorded as an explicit **accepted note** rather than dropped. I do not merge; the merge decision is the user's.
3. **Must-fix present** — status becomes `Needs rework` with each finding listed (file, line/area, what is wrong, why it matters), then I spawn `implementation-pms` with those findings; it fixes and returns the feature to `Awaiting verification`; **`verification-pms` re-runs the suite and flips it back to `Built & Verified`** (a Must-fix change alters behaviour, so it needs functional re-verification, not just my re-read); then I re-review, round 2.
4. **Hard cap of 3 review rounds.** If Must-fix findings are still open after round 3, I stop, report the standing findings plainly, and escalate to the user. I never downgrade a Must-fix to make the count look better, and I never write `Reviewed` over an open one.

**What my first message to `implementation-pms` would concretely look like** — a hypothetical F-1 finding, written out in full so the routing format is proven rather than gestured at:

> **Feature F-1 (Solution scaffolding, app shell, error contract) is returned as `Needs rework`. 2 Must-fix findings from code review round 1.** Tracker `doc/implementation-progress.md` already updated to `Needs rework`. Fix in the existing F-1 worktree (path, branch as recorded in the tracker); do not open a new one.
>
> **MF-1 (Security) — `backend/src/PMS.Api/Middleware/ProblemDetailsMiddleware.cs`, unhandled-exception branch.** The 500 response puts the full exception text into `detail`, so the stack trace and inner-exception message reach the browser. Plan section 7 requires unhandled exceptions to return 500 with a correlation id and **no internal detail**. This is the application that will carry PHI: exception text on a consulting-room screen leaks server paths today and, once services are wired, can carry patient data inside an EF exception message. Log the detail server-side against the correlation id; return only the id.
>
> **MF-2 (Correctness beyond tests) — `frontend/src/shared/api/httpClient.ts`, the `request` error path.** The error branch parses the response body as JSON unguarded, so a non-JSON error body (an HTML 502 from a proxy, or an empty 401) throws a parse error instead of the typed `ProblemDetailsError` that every caller catches. The Vitest spec covers a well-formed ProblemDetails body only. This is exactly the E-47 failure mode the plan says F-1 exists to make impossible: a swallowed or mistyped rejection is how "the doctor believes it saved" starts. Parse defensively, fall back to a synthetic `ProblemDetailsError` carrying the status code, and add a spec for the non-JSON body — the plan's F-1 test strategy already names "non-JSON error bodies" as in scope, so this is a missing case, not new scope.
>
> Two Should-fix items and one Nit are in my review report as well; they are not blocking, but tell me if you decline any so I can record them as accepted rather than lose them.
>
> When both Must-fixes are done: re-run the suite, set the tracker to `Awaiting verification`, and hand back to `verification-pms`, not to me. I re-review only after it flips to `Built & Verified` again. This is round 1 of a maximum of 3.

**Blocker on steps 2 and 3 as of today, in this repo:** the tracker file does not exist, and creating it is `implementation-pms`'s documented first action, not mine. Until it exists I have no status field to write. That is expected at this stage, not a defect in the loop.

---

## 4. F-1 walkthrough — what I would concretely check

Against the **on-disk** plan section 6, F-1 ("Solution scaffolding, app shell, error contract", `Ready`, no gating question), its five acceptance criteria, the section 2 conventions, the section 7 cross-cutting concerns, and the brainstorm edge cases that actually touch scaffolding. The plan's section 8 coverage table lists F-1's edge cases as none; the applicable ones below are my own derivation from brainstorm section 8, which is precisely the "gaps a passing suite did not probe" job.

### 4.1 Correctness beyond the tests

- **The `httpClient` error path** — a non-JSON error body (proxy HTML, empty 401 body), a 204 with no body being parsed as JSON, and a network-level rejection must all reach callers as the typed `ProblemDetailsError`, not as a raw parse or type error. **E-47** lives or dies here: everything later in the app depends on a failed write being loudly typed, and F-1's own plan text says so ("every failed write surfaces as a typed error the UI must render, never a swallowed promise").
- **`GET /api/health/db`** — does it return 503 for *unreachable*, and behave sanely for *reachable but migrations not applied*, *wrong database name*, and *login failure*? Does the probe have a timeout, or does an unreachable server hang the request until the SQL client default? Does it catch too broadly and report 200 on a half-broken connection?
- **`ProblemDetailsMiddleware`** — response already started (headers flushed mid-stream), a client-cancelled request classified as a 500, and the 400/409/500 mapping table actually matching section 7 rather than only the case the integration test exercises.
- **The clock abstraction** — the plan makes a deterministic `IClock` the substrate of every later service test. Is it injected everywhere, or does F-1 code still call `DateTime.Now`? Is it UTC-based with clinic-local conversion at one boundary? A wrong-by-design clock here silently poisons F-9, F-10 and F-16 dates.
- **Route table** — the nine placeholder routes plus SPA fallback: does the fallback swallow unmatched `/api/*` requests and serve `index.html` with a 200 instead of a 404? That is the classic scaffolding bug, and no F-1 test in the plan probes it.
- **Empty-state substrate** — `EmptyState.tsx` exists in F-1 precisely because **E-2, E-3 and E-4** demand purposeful empty states later. Is it a real reusable primitive with an action slot, or a placeholder that F-7, F-9 and F-16 will each fork?

### 4.2 Quality

- Layering per section 2: does anything in `PMS.Api` touch `PmsDbContext` directly, and does `PMS.Domain` stay framework-free (no EF, no ASP.NET package references)? The health endpoint is the likely first violation.
- Composition root readable — one `Program.cs` with grouped registrations, not a 300-line pile.
- Frontend shape matches the folder-per-feature convention (`features/<f>/` with a co-located API module, hooks, `types/`), shared code in `shared/`. F-1 sets the template that twenty later features copy; a sloppy shape here multiplies.
- `queryClient.ts` configured exactly as specified (`retry: 1`, `refetchOnWindowFocus: false`), in one place, not re-configured per hook.
- No dead scaffolding left from the `dotnet new` or Vite templates (weather-forecast controller, default counter component, unused CSS), and no commented-out blocks.
- Naming: `HealthResponse` as a DTO distinct from any entity; names later features can follow without ambiguity.

### 4.3 Consistency

- F-1 is the **first** feature, so there are no previously-reviewed siblings to match. The baseline is the plan itself, and F-1 *becomes* the baseline — I would review it with that weight and record the conventions it establishes in the tracker log so later reviews have something concrete to compare against.
- Checked specifically against sections 2 and 7: RFC-7807 `ProblemDetails` everywhere, including the health endpoints (no ad-hoc error JSON anywhere); the `/api` prefix; DTOs separate from entities with no entity crossing the wire; TanStack Query v5 for all server state, with plain component state only for uncommitted form input; React Router v6 paths exactly as listed (`/login`, `/setup`, `/`, `/patients`, `/patients/:id`, `/visits/:id`, `/settings/clinic`, `/export`, `/audit`); the migration named `InitialCreate`, not auto-named.
- Stack conformance is not cosmetic here — see G-2 in section 5. React-versus-Angular scaffolding is a Must-fix deviation regardless of a green suite.

### 4.4 Security

- **AC-4 is a review item, not only a test item.** I would grep the entire diff for connection strings, passwords, peppers, keys, and any `Server=` or `Data Source=` literal — in `appsettings.json`, `Program.cs`, launch settings, the integration-test fixture and `TestWebAppFactory`. Test fixtures are where a LocalDB connection string usually gets hardcoded and nobody notices.
- **A `.gitignore` gap already visible today, before any code exists:** the file ignores `*.env`, `*.env.local` and `appsettings.*.local.json`, but **not** `appsettings.Development.json` — the conventional home for a developer connection string, which would therefore be committed by default. It also has **no rule for `backend/src/PMS.Api/wwwroot`**, which F-1's AC-5 makes the output target of `npm run build`; without one, the built SPA bundle gets committed on every build. Both would be raised against F-1's diff (Must-fix if a secret actually lands there, Should-fix if it is only the missing rule).
- HTTPS posture per section 7: `UseHsts` and `UseHttpsRedirection` present and correctly ordered relative to static files and the SPA fallback.
- The health endpoints are anonymous **by design** in the plan, so the question is what they disclose: the 503 body must not carry server name, database name, connection-string fragment or exception text — a status and a correlation id only.
- **PHI-free operational logs (section 7)** — the logging setup F-1 establishes must not log request bodies or query strings wholesale, because every later feature's bodies are patient data. Getting this wrong once, here, leaks for the life of the product.
- 500 responses carry a correlation id and no internal detail; the developer exception page is not enabled outside Development.
- Deliberately **not** manufactured as F-1 findings: authorization enforcement (arrives with F-2, though I would confirm F-1 leaves no endpoint pattern F-2's blanket policy cannot cover); CSV formula-injection escaping per **E-56** (F-18); output escaping per **E-58** (F-14); encryption at rest (**Blocked on Q-1**, F-20 — a plan-level gate, not an F-1 defect).

**Verdict on the checklist itself: actionable.** Every item names a file or a convention that exists in the plan on disk, and roughly half target things F-1's four planned suites do not exercise — which is the point of the gate.

---

## 5. Gaps — what would stop me reviewing F-1 the moment it is handed over

| # | Gap | Severity for my gate | Owner |
|---|---|---|---|
| G-1 | Nothing is `Built & Verified`; `doc/implementation-progress.md` does not exist | Expected, not a defect | pipeline |
| G-2 | **The plan and brainstorm on disk are uncommitted, and the committed versions are a materially different document** | **High — would corrupt my consistency and correctness dimensions** | user |
| G-3 | No tracker file yet, so `Reviewed` / `Needs rework` has no target field to write | Low — `implementation-pms` creates it on its first real run | implementation-pms |
| G-4 | No previously-reviewed feature exists, so "consistency with siblings" has no baseline for F-1 | Low — F-1 becomes the baseline; I log the conventions it sets | me |
| G-5 | `dotnet-ef` is still not installed (`dotnet tool list --global` returns header rows only, re-checked today) | Medium — F-1's AC-2 (`InitialCreate` migration) cannot produce evidence for the gate before mine | user / implementation-pms |
| G-6 | No `.editorconfig`, no ESLint/Prettier config, no CI in the repo | Low — my quality dimension has no objective style baseline, so style findings stay Nits until a convention exists | user |
| G-7 | Q-1 (deployment model) still open, so encryption-at-rest has no answer | None for F-1 — correctly parked in `Blocked` F-20; I will not raise it against F-1 | product owner |

**G-2 in detail, because it is the one that would actually damage a review.** Both `doc/planning-pms-verification.md` and `doc/brainstorm-pms-verification.md` are modified-but-uncommitted on `main`:

```
$ git diff --stat doc/planning-pms-verification.md doc/brainstorm-pms-verification.md
 doc/brainstorm-pms-verification.md |  978 ++++++++++--------------
 doc/planning-pms-verification.md   | 1484 ++++++++++++++++--------------------
 2 files changed, 1063 insertions(+), 1399 deletions(-)
```

A worktree cut from `HEAD` therefore contains the **superseded** plan. I checked what that superseded F-1 actually says, and the divergence is not cosmetic:

| | On-disk plan (current) | Committed plan (what a worktree from HEAD contains) |
|---|---|---|
| F-1 title | Solution scaffolding, app shell, error contract | Solution skeleton, configuration, error handling, clinic clock |
| Frontend stack in F-1 | React 18 + TypeScript, Vite, `main.tsx` / `App.tsx` / `routes.tsx` | **Angular** — `ng new pms --standalone --routing --style=scss`, `error.interceptor.ts`, `*.spec.ts` |
| F-1 migration | `InitialCreate` ships in F-1 | none in F-1; first migration ships with F-3 |
| F-1 acceptance criteria | 5 criteria, including `npm run build` emitting to `wwwroot` | 6 criteria, including `ng serve` and a `ClinicClockService` timezone criterion |
| Edge-case ID scheme | `E-nn` | `EC-nn` |
| Meaning of `F-13` | Medications | Consultation draft lifecycle + autosave |

Both files carry the same `Date: 2026-08-18` header, so reading the wrong one **does not announce itself**. Concretely: reviewing an F-1 diff while reading the committed plan would score correct React code against an Angular specification and cite edge-case IDs (`EC-48`) that do not exist in the current brainstorm at all. My mitigation until this is fixed is to **always read the plan and brainstorm from the main working copy at `C:\Users\NileshMalviya\source\repos\Hospital-managment\doc\`, never from the feature worktree**, and to state in every review report which copy I used. The mitigation works, but this is a landmine for the whole pipeline rather than just for me — committing the two documents to `main` removes it permanently. `implementation-pms` and `verification-pms` recorded the same finding; it has not been actioned, and it is the user's call, not mine.

---

## 6. Verdict

**The review gate is mechanically ready to operate, and there is nothing yet for it to operate on.** The one capability the role depends on — inspecting a feature's actual diff first-hand rather than trusting a summary — was proved end to end this session against a synthetic throwaway worktree: `git worktree list`, `git -C <wt> show` with `--stat`, `git diff` on the worktree, the `main...<branch>` range diff and `git show <branch>:<file>` from the main repo, plus `Read` and `Grep` scoped to a worktree path outside the repo, all returned real output, and the probe worktree, its branch and its file were removed with the repo left byte-identical to its pre-check state. The routing half of the loop is executable too: the `Agent` tool and the `implementation-pms` agent type are both present, and section 3 sets out the literal first message a Must-fix hand-off would carry, together with the rule that a fixed feature goes back through `verification-pms` before I re-review, under a hard three-round cap. Two things must happen before I can actually review F-1, and neither is mine to do: **(1) commit `doc/planning-pms-verification.md` and `doc/brainstorm-pms-verification.md` to `main`** — without it, a worktree cut from `HEAD` carries a plan that specifies **Angular**, renumbers the edge cases from `E-nn` to `EC-nn`, and gives F-1 a different title, a different migration story and six different acceptance criteria, all under an identical date header that hides the swap; and **(2) `implementation-pms` must build F-1 and create `doc/implementation-progress.md`, and `verification-pms` must flip it to `Built & Verified`** — until that status exists my precondition is unmet, and reviewing earlier would burn a cycle on code that is still about to change. The F-1 checklist in section 4 is written against the on-disk plan's real acceptance criteria, real file paths and the brainstorm's real edge cases (E-47, E-2/E-3/E-4, with E-56 and E-58 correctly deferred to F-18 and F-14), and roughly half of it targets things F-1's four planned suites do not exercise, so the gate adds signal rather than repeating someone else's. **No application code, test code, migration, plan content, brainstorm content, BRD content, or progress tracker was created or modified by this check**: the only repo file written is this report, and the only other artefacts — one throwaway worktree, one throwaway branch and one three-line synthetic file, all created outside the repo — were removed before this report was written, leaving nothing but an unreachable dangling commit object that git will collect on its own.
