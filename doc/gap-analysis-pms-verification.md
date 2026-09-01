# Patient Management Application — BRD Gap Analysis (Requirements-Coverage Score)

- **Scored against:** `BRD/Doc_BRD.md` (198 lines, read in full) — the only ground truth. Every row below cites a BRD line number, not a summary of it.
- **Requirement inventory from:** `doc/brainstorm-pms-verification.md` coverage map §4 (`C-1..C-49`) and parking lot §11 (exclusions)
- **Traceability from:** `doc/planning-pms-verification.md` (`F-1..F-21`, §5 dependency map + §6 feature plans + §9 open items)
- **Build status from:** `doc/implementation-progress.md` — **does not exist** (re-confirmed on disk today, not assumed)
- **Date:** 2026-08-24
- **Mode:** Scoring-only. One pass, report, scorecard updated, stop. No closed-loop remediation was attempted and no agent was spawned.
- **Status:** First real scoring run against actual project state. This document does not authorise any build.

---

## 1. Headline score

# 0 / 38 = **0.0%** — **FAIL** (threshold ≥ 95%)

**Met: 0 · Gap — not built: 33 · Gap — blocked: 3 · Gap — not planned: 2 · N/A — excluded: 11 (denominator 49 − 11 = 38).**

**Nothing has been built.** This is not a tracker-drift finding or a verification dispute — there is no application code of any kind in this repository. The score is 0% because the denominator is real and the numerator is empty, and that is the accurate reading of the project, not a failure of this check.

Three things follow, and they are different from each other:

1. **33 requirements are simply unbuilt** — they have a feature in the plan, the plan is credible, and no one has started. Route: `implementation-pms`.
2. **3 requirements cannot be built by anyone** — `F-20` and `F-21` are marked `Blocked` in the plan and no amount of engineering resolves them. Route: **product owner** (`Q-1`, `Q-12`, and `C-44` for which brainstorm §12 carries **no question at all**).
3. **2 requirements were never turned into a feature** — BRD L77 ("80% reduction in paper usage") and L80 ("minimal training required") appear in no feature, no acceptance criterion, and no test in the plan. Route: `planning-pms`. These are planning gaps, and building faster will never close them.

**Also load-bearing, and not counted in the score because it is a process defect rather than a requirement:** `BRD/Doc_BRD.md` L196–198 states **"Open Questions: None (all major product decisions defined for Phase 1)."** Sixteen open questions (`Q-1..Q-16`) plus four plan-originated findings with no `Q-` at all are outstanding, **none recorded as answered anywhere in `doc/`**. Thirty of the 33 buildable gaps below would today be built against an unratified assumption. That is buildable — the plan names a default for each — but every one of those defaults is a place where a build can be correct against the plan and wrong against the doctor.

---

## 2. Traceability table — every requirement, scored

Status key: ✅ Met · ⛔ NB = Gap, not built · ⛔ BL = Gap, blocked · ⚠️ NP = Gap, not planned · N/A = excluded from denominator.
"OQ" names the unresolved open question the work sits behind; a gap can be `NB` and still carry an OQ (the plan states a default to build against).

| # | BRD section / line | Brainstorm ID | Feature ID(s) | Status | Route | Evidence checked |
|---|---|---|---|---|---|---|
| 1 | Users — "General Physician (Single User)" (L14) | C-2 | F-2, F-10 | ⛔ NB | implementation-pms | No `AppUser` entity, no auth code, no single-tab lock on disk; F-2 `Needs decision` in plan §5 |
| 2 | Scope — "Web-based access (browser-based system)" (L42) | C-6 | F-1 (+F-20 for deployment) | ⛔ NB | implementation-pms | No `.sln`, `.csproj`, `package.json` anywhere; F-1 is the only `Ready` feature and is unstarted. OQ `Q-1` (deployment model) still open |
| 3 | Scope — "Data export (CSV/PDF)" (L52) | C-7 | F-18 | ⛔ NB | implementation-pms | No export code; F-18 `Needs decision` (OQ `Q-11`, scope) |
| 4 | Success — consultation record in 2–3 min (L75) | C-11 | F-19 | ⛔ NB | implementation-pms | No instrumentation, no `typical-visit.spec.ts`; OQ `Q-15` (how the target is measured) |
| 5 | Success — search/history in 2–5 s (L76) | C-12 | F-7, F-16, F-19 | ⛔ NB | implementation-pms | No search endpoint, no seeded perf test; plan sets p95 ≤ 2 s at 5,000/25,000 (REC-19) but nothing measures it |
| 6 | Success — ≥80% reduction in paper usage (L77) | C-13 | **none** | ⚠️ NP | planning-pms | Grepped the full plan for `C-13`, "paper" — **zero hits**. No feature, no criterion, no test. Brainstorm §5.5 also flags it as unmeasurable (no baseline, and the flagship output is printed) |
| 7 | Success — smooth generation and printing (L78) | C-14 | F-14 | ⛔ NB | implementation-pms | No QuestPDF renderer, no `PrescriptionIssue`; F-14 unstarted |
| 8 | Success — successful CSV/PDF export (L79) | C-15 | F-18 | ⛔ NB | implementation-pms | Same as #3; nothing exports |
| 9 | Success — high usability, minimal training (L80) | C-16 | **none** | ⚠️ NP | planning-pms | Grepped plan for "training", "walkthrough", "usability", "unassisted" — **zero hits**. F-19 covers keyboard speed, not first-use learnability; no acceptance criterion exists for L80 |
| 10 | Patient Mgmt — add, edit, view patient details (L87) | C-17 | F-5, F-8 | ⛔ NB | implementation-pms | No `Patient` entity or `PatientsController`; F-8 `Needs decision` (OQ `Q-6`, hard delete/retention) |
| 11 | Patient field — Name (L89) | C-18 | F-5 | ⛔ NB | implementation-pms | No `Patient.FullName`/`NormalizedName`; plan §4 specifies both, nothing built |
| 12 | Patient field — Age / DOB (L90) | C-19 | F-5 | ⛔ NB | implementation-pms | No `DateOfBirth`/`ApproxAgeYears`/`AgeRecordedOn`; OQ `Q-16` |
| 13 | Patient field — Gender (L91) | C-20 | F-4, F-5 | ⛔ NB | implementation-pms | No `SettingOption` table; OQ `Q-9` (value list) |
| 14 | Patient field — Contact details (L92) | C-21 | F-5 | ⛔ NB | implementation-pms | No `PrimaryPhone`/`NormalizedPhone`; OQ `Q-7` (phone required?) |
| 15 | Patient Mgmt — search by name or phone (L93) | C-22 | F-7 | ⛔ NB | implementation-pms | No search endpoint or index; plan §9 item 19 notes **no `Q-` exists** for search semantics |
| 16 | Appointments — schedule appointments (L98) | C-24 | F-9 | ⛔ NB | implementation-pms | No `Appointment` entity; OQ `Q-5` (walk-ins) + plan §9 item 18 (time model, **no `Q-` exists**) |
| 17 | Appointments — view daily appointment list (L99) | C-25 | F-9 | ⛔ NB | implementation-pms | No `DailyAppointmentList.tsx`, no route `/` |
| 18 | Appointments — status Scheduled/Completed/Cancelled/No-show (L100–104) | C-26 | F-9 | ⛔ NB | implementation-pms | No status machine; OQ `Q-14` (legal transitions) |
| 19 | Consultation — mandatory vitals: temp, BP, pulse (L110–114) | C-28 | F-11 (+F-4) | ⛔ NB | implementation-pms | No `VisitVitals`; OQ `Q-2` (mandatory-or-reason is formally a **BRD change** needing owner acceptance) + `Q-10` (units/thresholds) |
| 20 | Consultation — complaints, free text (L119) | C-29 | F-12 | ⛔ NB | implementation-pms | No `Visit.ComplaintText`, no `ComplaintSection.tsx` |
| 21 | Consultation — record diagnosis notes (L124) | C-30 | F-12 | ⛔ NB | implementation-pms | No `Visit.DiagnosisText`; OQ `Q-8` (required before print?) |
| 22 | Medication — name, dosage, frequency, duration, instructions (L129–134) | C-31 | F-13 | ⛔ NB | implementation-pms | No `MedicationLine`; plan §9 item 20 notes **no `Q-` exists** for the required subset |
| 23 | Prescription — printable, header/patient/vitals/diagnosis/meds/footer (L136–142) | C-32 | F-14 (+F-3) | ⛔ NB | implementation-pms | No `ClinicProfile`, no renderer. Brainstorm rates this `Clear` + **Blocker** (nothing in the BRD creates the header's source); plan answers it with F-3, unstarted. OQ `Q-4` (header/footer/signature content) |
| 24 | Patient History — previous visits + vitals/complaints/diagnosis/prescriptions (L147–152) | C-33 | F-16 | ⛔ NB | implementation-pms | No `Visit` entity, no history endpoint; F-16 is `Ready` in the plan but gated on unstarted upstream |
| 25 | Patient History — filter by date (L153) | C-34 | F-16 | ⛔ NB | implementation-pms | No `HistoryDateFilter.tsx` |
| 26 | Search & Nav — quick patient search (L158) | C-35 | F-7 | ⛔ NB | implementation-pms | No `PatientSearch.tsx`; same OQ as #15 |
| 27 | Search & Nav — view recent patients (L159) | C-36 | F-7 | ⛔ NB | implementation-pms | F-7's title and §6 name `RecentPatients.tsx` and `getRecentPatients(take)` — planned, not built |
| 28 | Search & Nav — easy navigation profile ↔ visits (L160) | C-37 | F-1, F-10, F-16 | ⛔ NB | implementation-pms | No `AppLayout`, no `useBeforeUnloadGuard.ts` (the plan's answer to this row's silent-loss exposure) |
| 29 | Data Export — CSV (L165–167) | C-38 | F-18 | ⛔ NB | implementation-pms | No `Rfc4180CsvWriter.cs`; injection/quoting/BOM hardening exists only as plan text |
| 30 | Data Export — PDF (L168) | C-39 | F-18 | ⛔ NB | implementation-pms | No PDF path; shares F-14's renderer per plan §7 |
| 31 | NFR Usability — minimal UI for fast entry (L173–174) | C-40 | F-19 | ⛔ NB | implementation-pms | No hotkey layer, no tab-order test; OQ `Q-15` |
| 32 | NFR Performance — page load < 2 s, fast search (L176–178) | C-41 | F-19, plan §7 | ⛔ NB | implementation-pms | No Vite build, no code splitting, no measurement |
| 33 | NFR Reliability — **"No data loss"** (L180–181) | C-42 | F-10 (+F-20) | ⛔ NB | implementation-pms | No autosave, no draft state, no `SaveStateBadge`. Brainstorm's largest single exposure (RSK-1); OQ `Q-3` must be answered **before** F-10 ships, per plan F-15 |
| 34 | NFR Reliability — regular automated backups (L182) | C-43 | **F-20 (Blocked)** | ⛔ **BL** | **product owner** | Plan §5 marks F-20 **Blocked** on `Q-1` + `Q-12`; plan §8 confirms F-20 has **no test coverage at any layer** and §9 records "none — no steps written" |
| 35 | NFR Security — secure login, single-user auth (L184–185) | C-44 | F-2 + **F-21 (Blocked)** | ⛔ **BL** | **product owner** | Login itself sits in F-2, but recovery/lockout is F-21, marked **Blocked** on `C-44`, for which **brainstorm §12 carries no `Q-` at all**. With one user there is nobody to reset a password — undecided, not unbuilt |
| 36 | NFR Security — encryption at rest and in transit (L186) | C-45 | **F-20 (Blocked)** | ⛔ **BL** | **product owner** | Plan §7: in-transit is decided (HSTS/HTTPS, `Secure` cookie) but **at-rest is explicitly "Blocked on Q-1"** — TDE vs BitLocker vs cloud storage are three answers to three deployment models |
| 37 | NFR Scalability — single clinic, moderate volume (L188–189) | C-46 | F-19, plan §7 | ⛔ NB | implementation-pms | Ceiling named in plan (≤40/day, ≤6,000 patients, ≤30,000 visits) but nothing enforces or tests it |
| 38 | NFR Compatibility — Chrome, Edge, Safari (L191–192) | C-47 | F-1, F-14, F-19 | ⛔ NB | implementation-pms | Playwright is chosen in plan §8 for exactly this; no `PMS.E2E` project exists |

### 2.1 Excluded rows — every exclusion named (11 of 49)

Nothing here counts as a pass or a fail. Each exclusion is explicit and reasoned; none was made to improve the score, which is 0% under any of these choices.

| Brainstorm ID | BRD line | Why excluded | Note |
|---|---|---|---|
| C-1 | Product Goal (L5–7) | Not a buildable requirement — narrative framing; its testable load is carried by C-11..C-16, all scored | — |
| C-3 | Secondary users: None (L17) | **N/A — deferred.** BRD out-of-scope L60 (receptionist/multi-user); parking lot §11 | — |
| C-4 | Stakeholders (L19–22) | Not a requirement — organisational context | — |
| C-5 | Problem Statement (L26–34) | Not a requirement — motivation; its content is scored via C-17/C-22/C-33/C-42 | — |
| C-8 | Out of Scope list (L58–69) | Meta — this list *defines* the exclusions; scoring it would be circular | All 10 of its entries are honoured below and in §11 of the brainstorm |
| C-9 | Offline out of scope (L65) vs "no data loss" (L181) | **N/A — deferred.** Offline is BRD out-of-scope L65 and parking-lot deferred | The *conflict* is real and unresolved (`Q-1`); the "no data loss" half is scored as row 33 |
| C-10 | Follow-up alerts/reminders (L69) | **N/A — deferred.** BRD out-of-scope L69; parking lot §11 | Accepted risk: the prescription's Duration field creates an expectation the product will not meet |
| C-23 | *Absent:* patient identity/uniqueness rule | **Not a BRD requirement** — derived by the brainstorm. The BRD never states an identity rule | Planned as F-6; a prerequisite of rows 11/14/15, not double-counted |
| C-27 | *Absent:* the Visit / consultation entity | **Not a BRD requirement** — derived. The BRD never names a visit entity | Planned as F-10; it is the mechanism for rows 24 and 33, scored there |
| C-48 | *Absent:* audit trail, retention, consent | **Not a BRD requirement** — derived; BRD is silent | Planned as F-17. Retention/erasure is parking-lot deferred pending legal input (`Q-6`) |
| C-49 | "Open Questions: None" (L196–198) | Process defect, not a requirement | **Reported in §1 rather than scored.** It is materially false: 16 `Q-`s plus 4 plan findings with no `Q-` are open |

**Where the BRD is silent, I counted the requirement in Phase 1 rather than guessing it away.** The only rows removed as deferred are ones the BRD's own out-of-scope list (L58–69) or the brainstorm's parking lot §11 name explicitly.

---

## 3. Gap list — prioritized and routed

Severity uses the brainstorm's own Critical/Major/Minor impact rating, inherited from the risk register (`RSK-1..RSK-15`) where the requirement carries one.

### 3.1 Route → product owner (3 gaps) — **build cannot clear these**

These are decisions, not code. `implementation-pms` must not be pointed at them.

| Pri | Req | BRD | Severity | Blocked on | What is actually needed |
|---|---|---|---|---|---|
| P0 | C-45 encryption at rest | L186 | **Critical** (RSK-5) | **`Q-1`** — clinic PC / LAN server / cloud? | One meeting. `Q-1` also unblocks backup destination, session-security context and outage behaviour — it is the single highest-leverage decision in the project |
| P0 | C-43 automated backups | L182 | **Critical** (RSK-8) | **`Q-1` + `Q-12`** — RPO, and who is told when a backup fails | F-20 has *no steps written* and **no test coverage at any layer**; an untested restore is not a control |
| P1 | C-44 secure login / recovery | L184–185 | **Critical** (RSK-11) | **`C-44` — no `Q-` exists** | Brainstorm §12 has no question for this; it must be **added to the decision agenda**. With one user there is nobody to reset a password or unlock a lockout |

### 3.2 Route → planning-pms (2 gaps) — **never turned into a feature**

| Pri | Req | BRD | Severity | Finding |
|---|---|---|---|---|
| P2 | C-13 — ≥80% reduction in paper usage | L77 | Minor (not a build gate) | Zero plan coverage. Also unmeasurable as written: no baseline exists and the flagship output is a printed prescription. Plan a countable substitute (brainstorm §5.5 proposes "% of consultations with a complete digital record" and "% of lookups without a paper file") — **that substitution is the owner's call, not planning's, and not mine** |
| P2 | C-16 — high usability, minimal training | L80 | Minor | Zero plan coverage. F-19 measures keyboard *speed*, which is not learnability. Needs one acceptance criterion attached to an existing feature (brainstorm's default: unassisted first consultation after a ≤10-minute walkthrough) |

### 3.3 Route → implementation-pms (33 gaps) — **the build queue**

Ordered by the plan's own critical path, because that is the order in which they can physically be built: `F-1 → F-2 → F-3 → F-5 → F-9 → F-10 → F-11 → F-13 → F-14 → F-15 → F-17`.

| Order | Feature | Closes requirements | Severity of worst row | Plan readiness |
|---|---|---|---|---|
| 1 | **F-1** scaffolding, app shell, error contract | C-6, C-37 (partial), C-47 (partial) | Major | **`Ready`** — the only feature with no gate at all. **Start here.** |
| 2 | F-2 login, session, idle lock | C-2, C-44 (login half only) | Critical | `Needs decision` (C-44 session call) |
| 3 | F-3 ClinicProfile + first-run gate | C-32 (prerequisite) | Critical (RSK-4) | `Needs decision` (`Q-4`) |
| 4 | F-4 doctor-configured settings | C-20, C-28 (units/thresholds) | Major | `Needs decision` (`Q-9`, `Q-10`) |
| 5 | F-5 patient registration & profile | C-17, C-18, C-19, C-20, C-21 | Critical (RSK-7) | `Needs decision` (`Q-7`, `Q-16`, `Q-9`) |
| 6 | F-6 duplicate detection | (supports C-18, C-22) | Critical (RSK-2) | `Needs decision` (`Q-13`) |
| 7 | F-7 search, recent patients, picker | C-22, C-35, C-36, C-12 (partial) | Critical (RSK-12 wrong-patient) | `Needs decision` (no `Q-` exists) |
| 8 | F-8 patient edit + deactivate | C-17 | Critical (RSK-7) | `Needs decision` (`Q-6`) |
| 9 | F-9 appointments + walk-in start | C-24, C-25, C-26 | Major (RSK-6) | `Needs decision` (`Q-5`, `Q-14`) |
| 10 | **F-10** visit lifecycle, autosave, finalize | **C-42**, C-37 | **Critical (RSK-1 — the largest single exposure)** | `Needs decision` (**`Q-3` must be answered before this ships**) |
| 11 | F-11 vitals (mandatory-or-reason) | C-28 | Critical (RSK-3) | `Needs decision` (`Q-2` — **a BRD change**, needs owner acceptance) |
| 12 | F-12 complaints & diagnosis | C-29, C-30 | Major | `Needs decision` (`Q-8`) |
| 13 | F-13 medications | C-31 | Major | `Needs decision` (no `Q-` exists) |
| 14 | F-14 prescription generate/print/reprint | C-32, C-14, C-47 | Critical (RSK-4) | `Needs decision` (`Q-4`, `Q-8`) |
| 15 | F-15 visit amendments | (supports C-33, C-42) | Critical | `Needs decision` (`Q-3` — if answered "freely editable", **this feature ceases to exist**) |
| 16 | F-16 patient history + date filter | C-33, C-34, C-12 | Major | **`Ready`** — gated only by upstream |
| 17 | F-17 audit trail | (supports C-17, C-32) | Critical (RSK-9) | `Needs decision` (no `Q-` exists) |
| 18 | F-18 export CSV/PDF | C-7, C-15, C-38, C-39 | Critical (RSK-10 — formula injection, PHI) | `Needs decision` (`Q-11`) |
| 19 | F-19 keyboard-first + perf instrumentation | C-11, C-40, C-41, C-46, C-12 | Major (RSK-14) | `Needs decision` (`Q-15`) |

---

## 4. Spot-check results

**Every ✅ claim I could have inherited, I checked. There were none to inherit — and I verified that absence directly rather than concluding it from a missing file.**

| What I checked | How | Result |
|---|---|---|
| Does `doc/implementation-progress.md` exist? | `ls doc/implementation-progress.md` | **No such file or directory.** Confirmed today, not carried over |
| Is any feature marked `Built & Verified` anywhere? | `grep -rn "Built & Verified" doc/` | 6 hits, **all of them prose about the status label** in `implementation-pms-verification.md` and `verification-pms-verify.md`. **Zero status rows. No feature holds any status at all** |
| Does any application code exist? | `find . -path ./.git -prune -o \( -name "*.csproj" -o -name "*.sln" -o -name "*.cs" -o -name "*.ts" -o -name "*.tsx" -o -name "package.json" \) -print` | **Zero matches** across the entire working tree |
| Is code hidden in another branch or worktree? | `git branch -a`, `git worktree list` | Only `main` and `origin/main`; one worktree (the repo itself). Nowhere for code to hide |
| What is actually committed? | `git ls-files`, `git log --oneline` | 8 files, 2 commits (`4cddd0f`, `cccb356`). Docs and agent definitions only — **no source file has ever been committed** |
| Do the two prior agent reports' claims hold? | Read both in full | **They hold.** `implementation-pms-verification.md` states plainly "No feature was implemented — no application code, entity, migration, component, or test was written anywhere"; `verification-pms-verify.md` states "there is nothing for me to verify". Both are accurate against the disk. **No `Built & Verified` claim failed to hold up, because none was ever made** |
| Are the traced features real, or did I trust a summary? | Read plan §4, §5, §6 (F-1, F-5, F-7, F-9, F-16, F-19), §7, §8, §9 directly | Real. F-20 and F-21 are marked `Blocked` in §5 **and** §8 ("no test coverage at any layer") **and** §9 ("none — no steps written") — three independent confirmations, so the two blocked routings are not a single-source claim |

**One drift finding worth naming, inherited but re-established today.** Both prior reports flag that `doc/planning-pms-verification.md` and `doc/brainstorm-pms-verification.md` are **uncommitted on disk** and differ substantially from their `HEAD` versions (`HEAD` specifies **Angular + Jasmine/Karma**, against the fixed React stack, with a disjoint ID scheme in which `F-13` means something else entirely). I confirmed the working-tree state independently: `git ls-files` shows both files tracked, and my scoring used the **on-disk** versions, which match the fixed stack and carry the `F-1..F-21` / `C-1..C-49` IDs cited above. **A worktree cut today would carry the wrong documents.** That does not change this score — nothing is built either way — but it will silently corrupt the first build if it is not fixed before `F-1` starts.

---

## 5. Verdict

# FAIL — 0/38 = 0.0%

**Do not proceed until a re-run of gap-analysis-pms scores ≥95%.**

No claim of "Phase 1 complete" or "BRD satisfied" can be made or accepted. The §3 gap list is the work queue, in full, in that order.

**Single highest-leverage next action:** run the one-hour decision session on `Q-1..Q-16`, **with three items added to the agenda that brainstorm §12 does not currently carry** — credential recovery/lockout (`C-44`), the appointment time model (`C-24`), and search match semantics (`C-22`/`C-35`). `Q-1` alone converts both blocked-route gaps (C-43, C-45) into buildable work and settles encryption, backup destination and outage behaviour in one call; `Q-2`, `Q-3`, `Q-4` and `Q-5` de-risk the four largest features on the critical path. **Thirteen of the sixteen are one-meeting policy calls.**

**Second action, and it can run the same day:** commit the on-disk `planning-pms-verification.md` and `brainstorm-pms-verification.md` to `main` (see §4), install `dotnet-ef`, then hand **F-1** — the only feature that is `Ready` with no gate at all — to `implementation-pms`. F-1 needs no open question answered, so it is not waiting on the meeting.

**Not to be routed to implementation:** C-43, C-44, C-45 (owner decisions) and C-13, C-16 (planning gaps). Building will not close any of the five.

---

*Scoring method: binary, no partial credit. `Score = ✅ ÷ (✅ + all Gaps) × 100`. N/A rows appear on neither side of the fraction and every exclusion is named in §2.1. A feature that is 80% built scores exactly as a feature that is 0% built. Only `Built & Verified` in the tracker, plus independent spot-check confirmation, earns ✅ — and today there is no tracker and nothing on disk to confirm.*
