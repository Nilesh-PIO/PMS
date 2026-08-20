# Patient Management Application — BRD Brainstorm + Edge-Case Verification Review

- **Document under review:** `BRD/Doc_BRD.md`
- **Review type:** Whole-document brainstorming + edge-case verification pass (refresh; re-derived from the current BRD)
- **Date:** 2026-08-20
- **Scope:** Phase 1 only (single general physician, single clinic)
- **Status:** Pre-build readiness review. Not implementation design, and not authorisation to build.

---

## 1. Headline

**The BRD's biggest risk is its last line — "Open Questions: None."** The document defines *what* to build, almost never *what happens when it goes wrong*, and then closes the door on that conversation. Behind that door sit **eight build-readiness Blockers** — gaps where a developer cannot build the requirement at all as written, not merely build it differently from a colleague.

The single largest concrete Blocker: **the consultation has no lifecycle and no save model.** There is no draft state, no moment at which a visit becomes a permanent record, and no rule for editing after a prescription is printed. That gap sits directly under the non-functional requirement "No data loss," which the BRD as written cannot satisfy. Close second: **patients have no identity rule**, so duplicate records and split clinical histories are a matter of *when*, not *if*. Newly surfaced in this pass: **"web-based access" never states a deployment model**, and "encryption at rest" plus "regular automated backups" are unbuildable-as-written until it does.

**Top pick (R-1):** define the consultation as a **continuously autosaved draft, explicitly finalized at print**, with finalized visits immutable and corrections appended as dated amendments. It converts "no data loss" from a slogan into a testable property and costs the doctor zero extra clicks against the 2–3 minute target.

**Data integrity:** the BRD as written is exposed to all four failure modes simultaneously — **Duplicate** (no patient identity rule), **Orphan** (patients deletable while visits reference them; appointment status with no link to the visit that justifies it), **Mutable history** (no amendment or audit concept, so a finalized prescription can silently change meaning after it was handed to a patient), and **Silent loss** (no save model, no stated recovery objective, no visible backup status). Every finding below is scored against these four.

---

## 2. Frame

The decision this review supports: **is `BRD/Doc_BRD.md` complete enough to hand to a development team, and if not, which gaps must close before build starts?**

I read it as a Phase 1 build-authorisation document rather than a vision doc. No clarifying question is needed — both readings produce the same finding list; the vision reading only lowers urgency, not content.

---

## 3. Rubric (stated once; every rated table below uses it)

**Clarity verdict** — can a developer read this and build it as written?
- **Clear** — unambiguous; two developers would build the same thing.
- **Ambiguous** — buildable, but two developers would reasonably build it differently.
- **Missing detail** — the BRD does not state a decision the build requires.
- **Contradiction** — conflicts with another statement in the BRD, or with clinic reality.

**Build-readiness** — does the gap actually stop work? (Independent of clarity.)
- **Ready** — build as-is, or with an obvious low-risk default that this review names.
- **Needs decision** — buildable only after the product owner makes one explicit, nameable call.
- **Blocker** — cannot be built at all: a missing entity, a safety issue, or a contradiction that breaks another requirement.

*An item can be `Ambiguous` and still `Ready` (pick a sane default, state the assumption). An item can be perfectly `Clear` and still a `Blocker` — see C-25, a flawlessly worded requirement with no entity to hang it on. **Build-readiness, not clarity, drives priority** in §9 and §10.*

**Data-integrity exposure** — the four failure modes, applied to every idea and finding:
- **Duplicate** — two records can describe the same real-world fact.
- **Orphan** — a record can lose its parent, or point at something that no longer exists.
- **Mutable history** — a recorded fact can silently change meaning afterwards, with no trail.
- **Silent loss** — a write can be lost between "the doctor typed it" and "it's on disk", with no signal.
- **None** — no integrity exposure.

**Likelihood** — **High:** expected in normal single-clinic use within the first month (roughly weekly or more) · **Med:** plausible within the first year · **Low:** needs unusual circumstances; may never occur in this clinic's lifetime.

**Impact** — **Critical:** silent data loss or corruption; a clinical record or prescription attributed to the wrong patient; an unrecoverable record; or patient health data exposed outside the clinic · **Major:** blocks or stalls a live consultation, forces re-entry, or produces a record the doctor cannot trust without a second source · **Minor:** cosmetic or trivially recoverable; no record affected, no time lost beyond seconds.

**Effort** (sequencing only, not estimation) — **S:** under a day · **M:** two to five days · **L:** over a week, or a product decision plus build.

**Phase** — **1:** build now · **later:** parking lot (§11) · **accepted:** knowingly unhandled, risk absorbed.

**Cost to resolve** (open questions, §12) — **Policy call:** decidable in a meeting, no design work · **Design effort:** needs a designed flow or model change first.

---

## 4. BRD coverage map

Every row carries two independent labels — clarity and build-readiness — plus its data-integrity exposure. Sections containing more than one distinct decision are split into separate rows, because a section can be half-Ready and half-Blocker.

| ID | BRD section / decision | Clarity verdict | Build-readiness | Gap or tension | Data integrity |
|---|---|---|---|---|---|
| C-1 | Product Goal | Clear | Ready | — | None |
| C-2 | Users — "General Physician (Single User)" | Ambiguous | Needs decision | States an *access* model, never a *device* model. Shared clinic PC vs. private laptop changes session timeout, auto-lock, autofill and cache requirements | None (drives exposure, not integrity) |
| C-3 | Problem Statement | Clear | Ready | Well-drawn; the four listed pains map cleanly to requirements | None |
| C-4 | Scope — "Web-based access (browser-based system)" | Missing detail | Needs decision | **No deployment model.** Hosted service, clinic-server, or local machine? This one unstated decision determines what "encryption at rest" (C-36), "automated backups" (C-33), network-drop behaviour (EC-50) and export exposure (C-6) actually mean. Highest-leverage unnamed decision in the document | Silent loss — backup and recovery cannot be specified until this is answered |
| C-5 | Scope — "Basic search functionality" | Ambiguous | Ready | Default worth naming: case/diacritic-insensitive substring on name, digits-only normalised match on phone, type-ahead. This one-liner is load-bearing — search is the duplicate-prevention mechanism (D-2) | Duplicate — weak search means the doctor re-registers instead of finding |
| C-6 | Scope — "Data export (CSV/PDF)" | Missing detail | Needs decision | No export scope, filename convention, warning, audit or CSV escaping. Highest data-egress risk in the document, expressed in three words | None internally; privacy exposure (§8.9) |
| C-7 | Out of Scope | Clear | Ready | Unusually disciplined and specific. Keep exactly as is | None |
| C-8 | Success — "consultation record within 2–3 minutes" | Contradiction | Needs decision | One flat number for a workflow with 12+ required inputs. See B-1 | Silent loss — a target that pressures the doctor encourages abandoning records mid-entry |
| C-9 | Success — "search and history retrieval within 2–5 seconds" vs. NFR "page load < 2s" | Contradiction | Ready | Two different numbers for overlapping operations, and neither is a keystroke budget. Default: adopt the tighter figure, add a type-ahead budget. See B-6 | Duplicate — slow search pushes the doctor past the near-match check |
| C-10 | Success — "80% reduction in paper usage" | Ambiguous | Needs decision | No baseline, no denominator — and the product's headline output is printed paper. See B-5 | None |
| C-11 | Success — "smooth generation and printing", "high usability with minimal training" | Ambiguous | Needs decision | Both are unfalsifiable as written; they will be scored charitably at sign-off. Replace with countable proxies (see B-5 pattern): e.g. prescription renders correctly on all three supported browsers; doctor completes a full consultation unaided after one walkthrough | None |
| C-12 | Patient Management — fields (Name, Age/DOB, Gender, Contact) | Ambiguous | Needs decision | "Age / DOB" — which is stored? Both? Derived? Which fields are required? No field lengths. No gender value list. "Contact details" undefined (phone only? address? email?) | Mutable history — a bare "45" recorded in 2026 silently means something else in 2029 |
| C-13 | Patient Management — identity and uniqueness | Missing detail | **Blocker** | No uniqueness rule, no duplicate policy, no near-match check. Nothing prevents the same human existing twice with half a history each | Duplicate — the primary duplicate vector in the product |
| C-14 | Patient Management — "Add, edit, and view" with no delete/archive rule | Missing detail | **Blocker** | No record lifecycle. If delete exists, deleting a patient with visits orphans clinical records; if it does not exist, the BRD must say so | Orphan + Mutable history — visits detached from their patient; edited demographics silently rewrite past prescriptions |
| C-15 | Patient search by name or phone | Ambiguous | Ready | Partial vs. full phone match, prefix vs. substring, families sharing one number. Default per C-5 | Duplicate |
| C-16 | Appointment Management — "Schedule appointments" | Missing detail | Needs decision | No slot model, no duration, no double-booking rule, no working hours. Owner must say whether this is a time-slot calendar or a simple dated list | Duplicate — two appointments for one patient on one day, with no rule |
| C-17 | Appointment Management — four statuses | Missing detail | Needs decision | Statuses listed, transitions never defined. No rule for a Scheduled date that has passed. See §7.2 | Mutable history — status is clinical-adjacent and currently rewritable with no trail |
| C-18 | Appointment ↔ consultation relationship | Missing detail | **Blocker** | The BRD never links an appointment to the consultation it produces. "Completed" therefore has no source of truth; the daily list and visit history can never reconcile | Orphan — a Completed appointment with no visit, and a visit with no appointment, are both legal today and indistinguishable from error |
| C-19 | Consultation Workflow — lifecycle and save model | Missing detail | **Blocker** | No draft state, no finalize event, no save semantics, no rule for editing after print. Cannot be built against "No data loss" as written | Silent loss + Mutable history — the largest single exposure in the document |
| C-20 | Vitals Capture — "(Mandatory)" | Contradiction | **Blocker** | Mandatory with no exception path. When a vital genuinely cannot be taken, the doctor abandons the record or invents a number. Safety issue. See B-2 | Silent loss (abandoned record) or fabricated clinical values — invisible corruption |
| C-21 | Vitals — units, formats, plausible ranges | Missing detail | Ready | Default worth naming: BP as two numeric fields, temperature as value + unit selector, pulse numeric; plausibility bounds configured by the doctor, blank until set | Mutable history — free-text vitals cannot be compared across visits |
| C-22 | Complaints — free text | Missing detail | Ready | No length limit, no encoding statement, no paste handling. Default: stated max length with counter, full Unicode, formatting stripped on paste | Silent loss — text clipped at print or export is invisible clinical loss |
| C-23 | Diagnosis — "Record diagnosis notes" | Missing detail | Needs decision | Never marked mandatory or optional. A prescription can therefore print with no diagnosis and the BRD does not say whether that is acceptable | None directly; record-completeness risk |
| C-24 | Medication fields (name, dosage, frequency, duration, instructions) | Ambiguous | Needs decision | Which of the five are required? Maximum count? Ordering on the printed page? | None directly; incomplete-record risk |
| C-25 | Printable prescription — "Clinic/doctor header" | Clear | **Blocker** | Perfectly clear requirement with **no entity to hang it on.** The BRD never says where clinic name, doctor name, registration number, address or signature block come from. No clinic-profile concept exists anywhere in the document | None; but the Phase 1 headline deliverable cannot be rendered at all |
| C-26 | Prescription — "Patient details" contents | Missing detail | Ready | Which fields print? Name and age are obvious; phone and address on a document handed to a patient are a deliberate choice, not a default. Default: name, age/DOB, gender, visit date, patient reference — no phone | None; minor exposure |
| C-27 | Prescription issuance, reprint and amendment | Missing detail | **Blocker** | Printing is never recorded as an event. No reprint policy, no amendment policy, no snapshot. "What did I actually give this patient, and when" is unanswerable | Mutable history — the paper in the patient's hand and the record on screen can diverge with nothing to reconcile them |
| C-28 | Patient History — "Filter by date" | Ambiguous | Ready | Single date or range? Are drafts and cancelled appointments visible? Default: visit-date range; drafts shown and clearly flagged | Silent loss — a draft hidden from history is a record nobody knows exists |
| C-29 | Search & Navigation — "View recent patients" | Ambiguous | Ready | Recently *viewed* or recently *consulted*? How many? Default: last 10 consulted | None |
| C-30 | Search & Navigation — "Easy navigation between patient profile and visits" | Ambiguous | Ready | Intent clear, no acceptance criterion. Default: any visit reachable from the profile in one click and vice versa, with the patient identity pinned on screen throughout (also serves EC-30) | None |
| C-31 | NFR Usability | Ambiguous | Ready | "Fast data entry" stated; keyboard-first is implied by the 2–3 minute target but never required. Make it explicit — it is the actual lever on the target | None |
| C-32 | NFR Performance — page load < 2s | Clear | Ready | Testable. Reconcile with C-9 | None |
| C-33 | NFR Reliability — "No data loss", "Regular automated backups" | Contradiction | Needs decision | Unachievable as an absolute, untestable as written. "Regular" undefined; no retention window; no restore verification; blocked behind C-4. See B-3 | Silent loss — a backup nobody has restored is not a backup |
| C-34 | NFR Security — "Secure login (single user authentication)" | Missing detail | Needs decision | No session timeout, no lockout policy, no auto-lock, no statement about shared machines | None directly; exposure risk |
| C-35 | NFR Security — credential recovery | Missing detail | **Blocker** | Single user means there is no second account to reset the password. With no recovery path, one forgotten password locks the clinic out of every record it owns, permanently | Silent loss — total and unrecoverable |
| C-36 | NFR Security — "Data encryption (at rest and in transit)" | Missing detail | Needs decision | In transit is unambiguous. At rest means entirely different work for a hosted database vs. a clinic PC, and is undefined until C-4 is answered. Also silent on whether exports and generated PDFs are covered — they are the copies that leave | None; exposure |
| C-37 | Audit trail | Missing detail | Needs decision | The BRD is silent on audit entirely. For health records that silence is a gap, not a simplification — but whether it lands in Phase 1 is the owner's call | Mutable history — no answer to "who changed what, when" |
| C-38 | Retention and deletion policy | Missing detail | Needs decision | Silent. Right-to-be-forgotten vs. required medical-record retention is jurisdiction-specific and I will not invent one | Mutable history / Orphan, depending on the policy chosen |
| C-39 | NFR Scalability — "single clinic, moderate volume" | Clear | Ready | Appropriately unambitious. Do not over-engineer against it | None |
| C-40 | NFR Compatibility — Chrome, Edge, Safari | Ambiguous | Ready | Print rendering differs materially across these three, and printing is a Phase 1 deliverable. Default: verify print output on all three before go-live | Silent loss — content clipped by a browser's print engine is invisible |
| C-41 | Open Questions — "None (all major product decisions defined for Phase 1)" | Contradiction | Needs decision | This review found 18 decisions the BRD does not make, 8 of them Blockers. See B-4 | All four — undecided integrity rules get decided implicitly, in code |

**Blocker index (drives §9 and §10 ordering):** C-13, C-14, C-18, C-19, C-20, C-25, C-27, C-35. Nothing else in the document stops work outright.

**Coverage tally:** 41 rows — 8 Blocker · 14 Needs decision · 19 Ready. Clarity: 8 Clear · 13 Ambiguous · 16 Missing detail · 4 Contradiction. Note the divergence between the two columns: three of the eight Clear rows are fine prose, and one of them (C-25) is a Blocker.

---

## 5. Challenging the BRD

Each item states what the BRD says, why it may not hold, and what I would put in its place. These are findings for the owner to accept or reject — not edits I have made.

### B-1 — The 2–3 minute consultation target vs. the mandatory workflow
**BRD says:** "Doctor can complete a consultation record within 2–3 minutes", with mandatory vitals plus complaints, diagnosis and medications.
**Why it may not hold:** one flat number for a workflow with at least twelve required inputs (3 vitals + complaints + diagnosis + 5 fields per medication). A single-medication visit is achievable with keyboard-first entry. A three-medication visit is not — and the target will then read as a software failure when it is a metric failure. It also silently assumes the doctor types rather than dictating or writing while talking.
**What I would put in its place:** *median* consultation record ≤ 3 minutes for a one-to-two-medication visit, measured from opening the consultation to finalizing it, with no cap stated for complex visits — plus an explicit **keyboard-only completion path** requirement, which is the actual lever that makes any number reachable.
**Data integrity:** Silent loss — a target that pressures the doctor produces abandoned half-entered consultations, which is exactly what C-19 has no answer for.

### B-2 — "Mandatory" vitals with no exception path
**BRD says:** vitals capture is mandatory for every consultation.
**Why it may not hold:** ordinary clinic situations exist where a vital genuinely cannot be taken — equipment unavailable, a distressed or uncooperative patient, a quick review visit. A hard block leaves two outcomes: abandon the record (data loss, the exact thing the BRD forbids), or type a plausible fake number (data corruption, which is worse because it is invisible). Both are worse than the missing value the block was meant to prevent.
**What I would put in its place:** keep "mandatory" in the sense that a vital **cannot be silently skipped** — the doctor enters a value *or* selects an explicit "not recorded" reason from a short list the doctor defines. History and the printed prescription then read "BP: not recorded" rather than a blank. Cost against the 2–3 minute target: zero on the normal path, one keystroke on the exception path.
**Data integrity:** removes a fabricated-value corruption vector; "not recorded — cuff unavailable" is a durable recorded fact, whereas a blank is an ambiguity someone will misread years later.

### B-3 — "No data loss" as an absolute
**BRD says:** Reliability — "No data loss", "Regular automated backups".
**Why it may not hold:** as an absolute it is neither testable nor achievable — a power cut one keystroke after typing loses that keystroke. Stated absolutely, it gets signed off as met and then quietly violated. "Regular" is undefined, and a backup that has never been restored is not a backup.
**What I would put in its place:** a stated recovery objective — *no finalized visit is ever lost; an in-progress consultation loses at most N seconds of typing* (owner picks N: 5 or 10). Plus backup frequency in hours, a stated retention window, a **verified restore rehearsed before go-live**, and a visible signal when a backup does not complete.
**Data integrity:** Silent loss — a silently failing backup is the most dangerous single item in this review, because it is invisible until the day it matters.

### B-4 — "Open Questions: None"
**BRD says:** "None (all major product decisions defined for Phase 1)."
**Why it may not hold:** §12 lists 18 open questions and §4 identifies 8 build Blockers. Declaring zero open questions does not remove them — it removes the place the team was supposed to write them down. The predictable outcome: developers make these calls implicitly in code, and the clinic discovers each decision the first time it goes wrong with a real patient.
**What I would put in its place:** replace the line with the §12 table. A BRD with 18 named open questions is a healthier document than one with none.
**Data integrity:** all four modes — every identity, lifecycle and retention rule is currently scheduled to be invented by whoever writes that file first.

### B-5 — "80% reduction in paper usage" as a success criterion
**BRD says:** at least 80% reduction in paper usage.
**Why it may not hold:** the product's headline output is a **printed prescription**. Every consultation produces paper by design. Without a baseline (sheets per day today) and a defined denominator (does the prescription count?), it cannot be evaluated and will be interpreted charitably at sign-off — which makes it decorative. The same objection applies to C-11's "smooth" and "minimal training".
**What I would put in its place:** drop it, or restate as a countable proxy — *zero handwritten patient records after go-live; paper limited to the printed prescription given to the patient.*
**Data integrity:** None.

### B-6 — "Search and history retrieval within 2–5 seconds"
**BRD says:** Success Criteria — retrieval within 2–5 seconds; NFR — page load < 2s and "fast patient search".
**Why it may not hold:** two different numbers describe overlapping operations, and neither is a *keystroke* budget. A 2–5 second search is far too slow to support search-first registration (D-2), which is the mechanism this review depends on for duplicate prevention. A doctor with a patient waiting will not sit through a 3-second wait before registering; they will jump straight to "add new patient".
**What I would put in its place:** two separate budgets — **type-ahead results < 300ms** for patient search, and **full visit-history load < 2s**. Retire the 2–5 second figure. The tighter number is not gold-plating; it is what makes the duplicate prevention actually get used.
**Data integrity:** Duplicate — slow search is a duplicate-creation mechanism.

### B-7 — "Web-based" and "encryption at rest" without a deployment model
**BRD says:** Scope — "Web-based access (browser-based system)"; NFR — "Data encryption (at rest and in transit)", "Regular automated backups".
**Why it may not hold:** "web-based" describes the *interface*, not where the data lives. A hosted service, a clinic-server install and a single-PC install produce three different products against these NFRs: different backup mechanics, different meanings of "at rest", different behaviour when the network drops mid-consultation, and a very different privacy story for exports. Two developers reading this BRD could build both, and only discover the divergence at the first restore. The BRD is not wrong here — it is silent, and the silence is load-bearing.
**What I would put in its place:** one sentence naming the deployment model, and one naming who operates backups. Then C-33 and C-36 become specifiable. This is a product/ops decision with cost and privacy consequences, not an architecture detail — the architecture that follows from it is a build-team decision and hands off there.
**Data integrity:** Silent loss — no recovery guarantee can be written until this is answered.

---

## 6. Divergent options for the open decisions

Eight decisions the BRD leaves open. Options are genuinely distinct rather than variations of one; each set includes the minimal option and at least one that *removes* a step rather than adding a feature.

### D-1 — Consultation save model (Blocker C-19)

| Option | What it is | Effect on 2–3 min | Effort | Risk |
|---|---|---|---|---|
| A. Single save at the end | Everything held in the browser; one Save after medications | Fastest happy path | S | Critical: refresh, crash, session expiry or a stray back button loses the whole consultation |
| B. Save per section | Vitals saved, then complaints, then diagnosis, then meds | +3–4 clicks | M | Partial records everywhere; "what is a half-saved visit" becomes permanent |
| C. Autosave draft + explicit finalize | Draft row created on open, autosaved on pause/blur; "Finalize & Print" commits | Zero extra clicks | M | Draft clutter if consultations are abandoned; needs a draft-visibility rule |
| D. Autosave + finalize + append-only amendments | As C, plus finalized visits immutable and corrections added as dated amendments | Zero extra clicks on the normal path | M–L | History rendering more complex; doctor must grasp that corrections append |
| E. Two-stage commit with an end-of-day close | As C, plus all of today's drafts must be resolved before the day closes | +1 daily ritual | M | Adds a habit the doctor may simply ignore |
| F. Event-log / journal model | Every keystroke batch stored as an event; the record is a projection | Zero | L | Over-engineered for one clinic; violates "don't build for scale that isn't there" |

**Converged: D**, borrowing E's "unfinished consultations" prompt as a login-time nudge rather than a blocking ritual. D is the only option that satisfies "no data loss" *and* answers "can I edit after printing?" — a question the BRD never asks but the clinic will ask in week one. C is the acceptable cheaper version if amendments are deferred, but the amendment policy must then be written down as an accepted risk, not left blank.
**Data integrity:** closes **Silent loss** (autosave bounds the loss window) and **Mutable history** (amendments append, never overwrite). Introduces one exposure to manage: an abandoned draft is **Orphan**-adjacent if hidden from history — hence the rule that drafts are always visible and clearly flagged.

### D-2 — Patient identity and duplicates (Blocker C-13)

| Option | What it is | Trade-off |
|---|---|---|
| A. No constraint | Anyone can be registered any number of times | Zero friction, guaranteed split histories. This is the current BRD |
| B. Hard unique constraint on phone | One patient per phone number | Breaks in week one — families share a number |
| C. Soft duplicate warning at registration | On save, show near-matches (same phone, or similar name + same age); doctor confirms "this is a new patient" | One keystroke, and only when a match exists |
| D. Search-first registration | Registration is reachable only *after* a search returns nothing | **Removes** a step — the doctor is searching anyway. Strongest prevention |
| E. Clinic-assigned patient number on a card | Patient brings a number; lookup is exact | Works well until the card is lost, which is always |
| F. Full merge tooling | Merge two patients, re-parent visit history | Real design effort; cures rather than prevents |

**Converged: D + C together.** Search-first is the prevention and is a removed step, not an added one; the near-match warning is the safety net. F goes to the parking lot (P-1) with a stated interim: until merge exists, one of the pair is **archived, never deleted**, with a pointer note to the survivor.
**Data integrity:** directly targets **Duplicate**. The archive-not-delete interim exists specifically to avoid trading a duplicate for an **Orphan** — deleting the loser of a duplicate pair would detach its visits.

### D-3 — Age vs. DOB (C-12)

| Option | Trade-off |
|---|---|
| A. DOB required | Precise, auto-updating, sorts correctly — but many walk-ins do not know their DOB and the doctor will invent 01-01-1980 |
| B. Age only | Fast and honest to how the clinic works — but the record silently rots; "45" recorded in 2026 is meaningless in 2029 |
| C. DOB optional + age-at-registration stored **with the date it was captured** | Both paths work; display derives from DOB when present, otherwise from age + capture date |
| D. DOB with a precision flag (exact / year-only / approximate) | Most correct, most fields, most entry time |
| E. Birth year only | Halfway house; loses newborn granularity entirely |

**Converged: C.** The only option that stays truthful three years later without blocking a walk-in. Display rule: show computed age, marked approximate when derived. Age display must support sub-year units ("3 months", "11 days") or a newborn prints as "0".
**Data integrity:** closes a **Mutable history** exposure — a stored bare age silently changes meaning with the passage of time, the quietest form of record corruption in this product.

### D-4 — Prescription printing as a recorded event (Blocker C-27)

| Option | Trade-off |
|---|---|
| A. Print is a UI action only, nothing recorded | Simplest; no way to answer "what did I give this patient and when" if the paper is lost |
| B. Print records an issued-at timestamp on the visit | Cheap, answers the basic audit question |
| C. Print stores an immutable snapshot of exactly what was printed | Survives later edits; a reprint reproduces the original |
| D. C + reprint log (original / reprint / amended reissue, each dated) | Full traceability |
| E. Snapshot + a verification code printed on the paper | Lets a pharmacist or patient query a specific issue later — but that is an integration path, and integrations are out of scope |

**Converged: C, with reprints flagged (light D).** Not regulatory theatre: once a visit can be amended, "what the patient is holding" and "what the record now says" diverge, and only a snapshot reconciles them. Note the browser reality — **the app cannot know whether the print dialog completed or was cancelled**, so "issued" must mean "prescription generated", and a cancelled dialog must leave a state the doctor can reprint from without creating a second visit.
**Data integrity:** closes **Mutable history** on the most consequential artefact in the product, and prevents a **Duplicate** visit created by a doctor who reacts to a cancelled print dialog by starting over.

### D-5 — Appointment ↔ consultation relationship (Blocker C-18)

| Option | Trade-off |
|---|---|
| A. Fully coupled — a consultation requires an appointment | Clean model, wrong clinic: walk-ins are the majority in small practices |
| B. Fully decoupled — appointments and visits are unrelated | Daily list and history never reconcile; "Completed" becomes a manual assertion with nothing behind it |
| C. Optional link — a consultation may reference an appointment; finalizing auto-sets that appointment to Completed | Matches reality, **removes** a click, keeps the daily list honest |
| D. Auto-create an appointment for every walk-in | Daily list is complete — but fabricates appointments that never existed |
| E. C + a daily reconciliation view of Scheduled-but-no-visit | Useful end-of-day sweep; one more screen |

**Converged: C**, with E's reconciliation deferred to the parking lot as a display-only nicety (P-14). C removes a step: the doctor never has to remember to mark an appointment Completed.
**Data integrity:** closes an **Orphan** exposure — today a Completed appointment with no visit behind it is legal and indistinguishable from a mistake. D was rejected precisely because it creates **Duplicate** appointment records for events that never happened.

### D-6 — Export scope and safety (C-6)

| Option | Trade-off |
|---|---|
| A. Export everything, one button | Simplest; produces an unencrypted full patient database in the Downloads folder — the worst privacy outcome available |
| B. Export scoped to the current view only (this patient / this date range) | Small friction, dramatically smaller blast radius |
| C. B + a confirmation naming what is being exported and how many records | One extra click on a rare action |
| D. B + C + password-protected PDF / passphrase-protected CSV | Real protection, real friction, and the passphrase ends up on a sticky note |
| E. Print-to-PDF only, no CSV at all | Kills the CSV injection and mangling class entirely — and kills the legitimate "give me my data" use case |
| F. Export + an audit entry recording what left the system and when | Prevents nothing; makes it answerable afterwards |

**Converged: B + C + F.** Scope-limited by default, a confirmation stating the record count, and an audit entry. D goes to the parking lot (P-3). Separately and non-negotiably: CSV export must neutralise formula-injection prefixes (`=`, `+`, `-`, `@`) and correctly quote commas, quotes and newlines inside complaint and diagnosis text — the difference between a valid export and a silently mangled or actively dangerous one.
**Data integrity:** **Silent loss** in the exported artefact — an unescaped comma in a complaint splits the row and the export *looks* successful. Privacy exposure is covered in §8.9.

### D-7 — The vitals exception path (Blocker C-20)

| Option | Trade-off |
|---|---|
| A. Hard block (current BRD) | Guarantees a value in the field; guarantees fabricated values or abandoned records |
| B. Free-text override on any vital | Maximum flexibility; unusable in history and impossible to filter |
| C. Value **or** an explicit "not recorded" reason from a short doctor-defined list | Preserves the BRD's intent, one keystroke on the exception path, stays structured |
| D. Vitals optional with a warning at finalize | Simplest to build; erodes the BRD's clear intent that vitals matter |
| E. Vitals mandatory only when medication is prescribed | Clinically flavoured rule — out of bounds for me to propose; only the doctor can set it |

**Converged: C.** E is named for completeness but is a clinical decision: **the doctor defines the rule, the system enforces it.** The same applies to implausible-value bounds — the system warns against ranges the doctor configures and never asserts a clinical judgement of its own.
**Data integrity:** closes a fabrication vector. A recorded "not recorded — cuff unavailable" is a durable fact; a blank field is an ambiguity someone will later misread as a normal reading.

### D-8 — Deployment model (C-4 / C-33 / C-36; new in this pass)

Product-level options only. The architecture that follows from the choice is a build-team decision and hands off there.

| Option | Trade-off |
|---|---|
| A. Single-tenant hosted service | Backups and encryption at rest are operable and verifiable by someone whose job it is; requires clinic connectivity for every consultation, and patient data leaves the premises |
| B. Server in the clinic, browser access on the LAN | Data never leaves the premises; backup and disk encryption become the clinic's problem, and "regular automated backups" needs a named owner and an off-site copy |
| C. Single PC, browser pointed at localhost | Cheapest and simplest; one disk failure is the whole clinic, and "multi-device" and remote access disappear |
| D. Hosted, with a local read-only cache | Survives short outages — but caching patient data on the device reopens EC-73 and edges toward offline mode, which is explicitly out of scope |

**Converged: no recommendation — this is the owner's call (OQ-6),** and it is the highest-leverage unanswered question in the document because C-33 and C-36 cannot be specified without it. What I will say: whichever is chosen, the acceptance criteria in B-3 (recovery objective, backup frequency, rehearsed restore, visible failure signal) apply unchanged. D is the one to be wary of — it buys resilience by quietly importing an out-of-scope capability.
**Data integrity:** **Silent loss** — every backup and recovery guarantee in this review is downstream of this decision.

---

## 7. Sketches

Sketch altitude only — entity and field *names* and relationships, to make the decisions above concrete. No types, constraints, indexes or migrations; those are implementation decisions for the build team, and producing them here would hand the team choices I am not positioned to make.

### 7.1 Entity sketch

```
Patient
  patient_id · display_name · dob (optional) · age_at_registration (optional)
  age_captured_on · gender · phone_primary · phone_alt (optional)
  notes · registered_on · record_status (active | archived) · archived_into_ref

Appointment
  appointment_id · patient_id -> Patient · scheduled_for · status
  status_changed_on · reason_note · created_on

Visit (Consultation)
  visit_id · patient_id -> Patient · appointment_id -> Appointment (optional)
  clinic_date · started_at · finalized_at · lifecycle_state (draft | finalized)
  complaints_text · diagnosis_text

Vitals                (one per Visit)
  visit_id -> Visit
  temperature_value · temperature_unit · temperature_not_recorded_reason
  bp_systolic · bp_diastolic · bp_not_recorded_reason
  pulse_value · pulse_not_recorded_reason

MedicationLine        (many per Visit, ordered)
  line_id · visit_id -> Visit · sequence
  drug_name · dosage · frequency · duration · instructions

PrescriptionIssue     (many per Visit: original, reprint, amended reissue)
  issue_id · visit_id -> Visit · generated_at · issue_kind · printed_snapshot

Amendment             (many per Visit, append-only)
  amendment_id · visit_id -> Visit · amended_at · field_changed · prior_value · reason

ClinicProfile         (single record — the entity missing from the BRD, C-25)
  clinic_name · doctor_name · qualifications · registration_number
  address · phone · footer_note · logo_ref

AuditEvent            (append-only)
  event_id · occurred_at · entity_kind · entity_id · action
```

Relationships: `Patient 1—* Visit` · `Visit 1—1 Vitals` · `Visit 1—* MedicationLine` · `Visit 1—* PrescriptionIssue` · `Visit 1—* Amendment` · `Appointment 0..1—0..1 Visit`.

**Data integrity:** four elements here exist purely to close integrity holes and **none of them appear in the BRD** — `PrescriptionIssue.printed_snapshot` closes **Mutable history** on the printed artefact; `Amendment` closes **Mutable history** on the visit; `Patient.record_status` closes the **Orphan** hole a hard delete would open; `Visit.lifecycle_state` closes **Silent loss**. `ClinicProfile` is the missing entity behind Blocker C-25.

### 7.2 Appointment state machine (BRD lists states, never transitions)

```
                              finalize visit
              +-----------+   (auto, D-5 C)   +-----------+
   create --> | Scheduled | ----------------> | Completed |
              +-----+-----+                   +-----------+
                 |     |                            ^
        cancel   |     |  mark no-show              | late arrival:
                 |     +-------------+              | allowed + audited
                 v                   v              |
           +-----------+       +-----------+        |
           | Cancelled |       |  No-show  | -------+
           +-----+-----+       +-----+-----+
                 |                   |
                 +---- reopen? ------+   <- NOT DRAWN IN THE BRD. Decision needed.
```

Transitions the BRD does not define, with my proposed answers:
- **Completed -> anything:** blocked. A finalized visit sits behind it; changing the status would orphan that visit's justification.
- **No-show -> Completed:** **allowed** (patient turned up late). High likelihood, and blocking it drives the doctor to create a duplicate appointment.
- **Cancelled -> Scheduled:** blocked. Book a new appointment instead — simpler and loses nothing.
- **Scheduled date passes, still Scheduled:** do **not** auto-transition. Display as "Overdue" in the daily list and let the doctor resolve it. Auto-marking No-show silently rewrites clinical-adjacent history.

**Data integrity:** the two blocked transitions prevent **Orphan** (a Completed status detached from its visit) and **Mutable history** (silent auto-rewriting of what happened on a past day).

### 7.3 Consultation lifecycle

```
  [open consultation]
        |  creates draft immediately; autosave from here on
        v
   +---------+  finalize & print   +-----------+   amend    +----------+
   |  Draft  |-------------------->| Finalized |----------->| Amended  |
   +----+----+                     +-----+-----+            +----------+
        |                                |
        | discard (explicit, confirmed)  | reprint -> new PrescriptionIssue
        v                                v            flagged as reprint
   [deleted, audited]
```

Rules this settles: a draft **is** visible in that patient's history, clearly marked, so it can never be silently lost. Finalized visits are read-only. Amendments append; they never overwrite.
**Data integrity:** closes **Silent loss** (draft persisted from the first keystroke, and visible) and **Mutable history** (finalized records immutable, corrections traceable).

---

## 8. Nine-category edge-case sweep

All nine categories were checked against the leading options. Only cases that genuinely apply are reported, ranked within each category by likelihood × impact. Rubric in §3. **Cases that corrupt data or lose a record lead each table; merely-ugly cases sit below them and never displace them.**

### 8.1 Empty / zero / first-run

**Data integrity:** mostly **None** — this category is about the dignity of the empty state, with one exception (EC-1, an unusable clinical document).

| ID | Scenario | Likelihood | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| EC-1 | First launch, clinic header never configured, doctor prints a prescription | High | Major | Force ClinicProfile setup on first login; block printing until complete. A prescription with a blank header is not a usable clinical document | 1 |
| EC-2 | Consultation with zero medications (advice-only visit) | High | Major | Explicitly allowed. Prescription prints "No medication prescribed" rather than an empty section | 1 |
| EC-3 | Diagnosis blank at print time | Med | Major | Warn once, allow override. Hard-blocking is clinical rule-setting; the doctor decides (OQ-14) | 1 |
| EC-4 | No patients yet — search, daily list and recent patients all empty | High | Minor | Purposeful empty states, each offering the primary action ("Register first patient") | 1 |
| EC-5 | No appointments today (all walk-ins) | High | Minor | Empty state offers "Start walk-in consultation" | 1 |
| EC-6 | Patient with zero prior visits | High | Minor | "First visit" state, not an empty table | 1 |
| EC-7 | Search returns nothing | High | Minor | Offer "Register [typed text] as a new patient" — this *is* the search-first path from D-2 | 1 |
| EC-8 | Export invoked with nothing in scope | Med | Minor | Disable export when the count is zero; never produce a zero-row file that looks like a successful export | 1 |

### 8.2 Boundary values and extremes

**Data integrity:** **Silent loss** dominates — text and layout that clip on print or export lose clinical content with no error shown.

| ID | Scenario | Likelihood | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| EC-9 | Dosage typo — 5 vs. 50 | Med | Critical | No clinical guard is appropriate from me. What is appropriate: a pre-finalize review screen showing the medication list exactly as it will print. Review, not validation | 1 |
| EC-10 | Prescription overflows one printed page | High | Major | Defined multi-page layout: repeating header, "Page 1 of 2", medication rows never split across pages | 1 |
| EC-11 | Complaint text pasted at 10,000+ characters | Med | Major | Stated max length with a visible counter; print must wrap, never clip. Clipping on print is silent clinical loss | 1 |
| EC-12 | Newborn — age in days or months | Med | Major | Age display supports "3 months" / "11 days". A newborn printed as "0" is misleading on a clinical document | 1 |
| EC-13 | Vitals at implausible extremes (temperature 450, pulse 4) | Med | Major | **The doctor defines the plausible range; the system enforces it as a soft warning** — never a hard block, never a clinical judgement of its own. Blank until the doctor configures it | 1 |
| EC-14 | DOB in the future, or today | Low | Major | Future DOB rejected. Today allowed (newborn) | 1 |
| EC-15 | 300-character patient name breaks the prescription header | Med | Minor | Field max length; header truncates with ellipsis, the stored record never does | 1 |
| EC-16 | 40 medications on one prescription | Low | Minor | No hard cap; the layout must simply cope (EC-10) | 1 |
| EC-17 | Age above ~120 | Low | Minor | Soft warning only; accept the value | 1 |
| EC-18 | 500 patients in one day's list | Low | Minor | Theoretical for one physician. Do not build pagination for volume that is not there | accepted |

### 8.3 Missing / partial / optional data

**Data integrity:** **Silent loss** (abandoned records) and fabricated values. EC-19 is the highest-value fix in this category.

| ID | Scenario | Likelihood | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| EC-19 | BP genuinely cannot be taken, but vitals are mandatory | High | Critical | Explicit "not recorded" reason per vital (B-2 / D-7). Without it the doctor fabricates a value — invisible corruption of a clinical record | 1 |
| EC-20 | Patient has no phone number | High | Major | Phone optional — and therefore cannot be the identity key (D-2). Name search must still find them | 1 |
| EC-21 | Patient does not know their DOB | High | Major | Age + capture-date path (D-3 option C) | 1 |
| EC-22 | Single-name patient, no surname | High | Major | One `display_name` field, not first/last. Splitting names is the more common bug and buys nothing here | 1 |
| EC-23 | Medication with dosage but no duration | Med | Major | Drug name required; the other four optional, with the pre-print review (EC-9) showing what is blank (OQ-14) | 1 |
| EC-24 | Prescription printed before diagnosis is entered | Med | Major | Same handling as EC-3 | 1 |
| EC-25 | Gender not stated / non-binary / patient declines | Med | Minor | The value list is a **policy call by the owner** (OQ-15) and must include an "unspecified" option. Do not hardcode two values | 1 |
| EC-26 | Contact details captured but now stale | Med | Minor | Show `updated_on` beside contact details. No verification workflow in Phase 1 | 1 |

### 8.4 Duplicates and identity

**Data integrity:** **Duplicate** throughout, with **Orphan** risk hiding inside any naive "just delete the extra one" fix.

| ID | Scenario | Likelihood | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| EC-27 | Same patient registered twice; history splits | High | Critical | Prevention via search-first registration + near-match warning (D-2). Highest-value prevention in this review: a split history means the doctor decides on half a record | 1 |
| EC-28 | Two different patients, same name and same age | Med | Critical | Every patient picker shows a disambiguator (phone tail + last visit date). Never show a bare name in a selection list | 1 |
| EC-29 | Duplicates discovered *after* both records have visit history | Med | Critical | Merge deferred (P-1). Interim: archive one so it leaves search, with a pointer note to the survivor. **Never delete** — that orphans visits | 1 |
| EC-30 | Consultation started against the wrong patient, noticed halfway | Med | Critical | Patient identity pinned and visible throughout the consultation screen (C-30). Re-assignment allowed while draft, and audited; once finalized, void-and-reissue with an amendment note | 1 |
| EC-31 | A whole family shares one phone number | High | Major | Phone must not be unique. Phone search returns *all* matches with name and age; never auto-selects the first | 1 |
| EC-32 | Two appointments for the same patient on the same day | Med | Minor | Allowed (morning review + evening follow-up) but warn on the second booking | 1 |

### 8.5 State transitions and lifecycle

**Data integrity:** **Silent loss** (EC-33) and **Mutable history** (EC-35, EC-36); EC-34 is a pure **Orphan** case.

| ID | Scenario | Likelihood | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| EC-33 | Consultation started, never finished (patient leaves, day ends) | High | Critical | Draft persists, visible in history and in an "Unfinished consultations" prompt at next login. The BRD is silent, and this is the most likely real data-loss path in the product | 1 |
| EC-34 | Attempt to delete a patient who has visits | Med | Critical | Hard-block delete; offer archive. Destroying clinical history must never be a one-click action | 1 |
| EC-35 | Consultation edited after the prescription was printed | High | Major | Amendments append; printed snapshot preserved; history shows both (D-1 D + D-4 C) | 1 |
| EC-36 | Backdating a visit (recording yesterday's paper consultation) | Med | Major | Allow, but record both `clinic_date` and `created_on` and label backdated entries. Undisclosed backdating is the problem, not backdating | 1 |
| EC-37 | Draft consultation discarded by mistake | Low | Major | Explicit confirmation naming the patient; audited | 1 |
| EC-38 | No-show marked, patient arrives 40 minutes late | High | Minor | Transition allowed and audited (§7.2) | 1 |
| EC-39 | Appointment date passes while still "Scheduled" | High | Minor | Display as Overdue; never auto-transition | 1 |
| EC-40 | Cancelled appointment, doctor sees the patient anyway | Med | Minor | Walk-in visit with no appointment link — already supported by D-5 C | 1 |
| EC-41 | Forward-dating an appointment months ahead | Med | Minor | Allowed. Reminders are out of scope, so accept that a distant appointment may simply be forgotten | accepted |

### 8.6 Concurrency, timing and sessions

**Data integrity:** **Silent loss** across the board. "Single user" never meant "single tab".

| ID | Scenario | Likelihood | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| EC-42 | Browser refresh or accidental tab close mid-consultation | High | Critical | Autosave (D-1) plus an unload warning while a draft is dirty | 1 |
| EC-43 | Session expires mid-consultation; doctor returns to a login screen holding unsaved text | High | Critical | Do not expire while a draft is actively being edited; restore the draft on re-login. A timeout that eats a consultation gets switched off entirely — the worse security outcome | 1 |
| EC-44 | Two browser tabs open on the same consultation | Med | Critical | Detect the second editing tab and make it read-only with a clear message. Last-write-wins silently discards the other tab's typing | 1 |
| EC-45 | Double-click on Finalize & Print creates two visits or two prescriptions | High | Major | Idempotent submit: disable on first click, plus server-side de-duplication | 1 |
| EC-46 | Back button mid-consultation | Med | Major | Draft is already saved; navigating forward restores it | 1 |
| EC-47 | Clock crosses midnight mid-consultation | Low | Major | `clinic_date` is fixed when the draft opens and does not move; `finalized_at` is the true instant. Store both, or the visit vanishes from "today" | 1 |
| EC-48 | DST shift or device timezone change affecting appointment times | Low | Major | Store instants; render in one clinic timezone configured once. Never render in browser-local time | 1 |
| EC-49 | Same doctor on two devices (clinic PC and laptop) | Low | Major | Server is authoritative; drafts sync on load. Not offline-capable (out of scope). Availability depends on OQ-6 | 1 |

### 8.7 Failure and recovery

**Data integrity:** **Silent loss** in its purest form. EC-51 is the single most dangerous item in this review, because nothing surfaces it until a restore is needed.

| ID | Scenario | Likelihood | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| EC-50 | Network drops on save during a consultation | High | Critical | Local buffering, retry, and an unmistakable "not saved" indicator. Never show success optimistically | 1 |
| EC-51 | **Automated backup fails silently** | Med | Critical | Visible backup status with a last-success timestamp on the home screen, and a warning after N missed cycles. A backup nobody checks is a backup that does not exist | 1 |
| EC-52 | Restore from backup — what is lost, and who verifies? | Med | Critical | Documented recovery objective (B-3) plus a **rehearsed restore before go-live**, owned by whoever OQ-6 names | 1 |
| EC-53 | Power cut mid-consultation | Med | Critical | Loss bounded by the autosave interval declared in B-3 | 1 |
| EC-54 | Storage full | Low | Critical | Free-space monitoring with early warning; a failing write must fail loudly, never silently truncate | 1 |
| EC-55 | Partially written or corrupted record | Low | Critical | Visit + vitals + medication lines written as one transactional unit | 1 |
| EC-56 | Print dialog cancelled — is the prescription "issued"? | High | Major | The app cannot detect this. Treat as "generated" and make reprint from the visit trivial, so the doctor never re-enters data (D-4) | 1 |
| EC-57 | PDF generation fails or produces a blank file | Med | Major | Explicit error; never leave the user unsure whether a file was written | 1 |
| EC-58 | Server error after the doctor clicked print | Med | Major | Write the snapshot before rendering, so the record survives even when rendering does not | 1 |

### 8.8 Input validation, encoding and misuse

**Data integrity:** **Silent loss** (EC-61 mangles rows, EC-62 renders names unreadable) and **Duplicate** (EC-65 whitespace variants).

| ID | Scenario | Likelihood | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| EC-59 | CSV formula injection — a field beginning `=`, `+`, `-`, `@` | Med | Critical | Neutralise the prefix on export. Real exposure: the file is opened in a spreadsheet on the clinic PC | 1 |
| EC-60 | Free text rendered into printed HTML (angle brackets, quotes) | Med | Critical | Escape on output everywhere, including the print view. Unescaped output is both an injection risk and a garbled clinical document | 1 |
| EC-61 | Complaint text containing commas or newlines breaks CSV rows | High | Major | Correct RFC-style quoting and escaping. A silently mangled export is worse than a failed one | 1 |
| EC-62 | Non-Latin and mixed-script names, combining characters | High | Major | Full Unicode storage, rendering and search without case/diacritic surprises. The print font must actually carry the script — a name printing as boxes on a prescription is a real failure | 1 |
| EC-63 | Phone numbers with country codes, spaces, dashes or letters | High | Major | Store as entered; normalise a digits-only form for search so "+91 98765 43210" and "9876543210" both match | 1 |
| EC-64 | Vitals typed with units or ranges ("120/80 mmHg", "98.6F") | Med | Major | BP as two numeric fields, temperature as value + unit selector. Free-text vitals make history incomparable across visits | 1 |
| EC-65 | Leading/trailing whitespace and smart quotes pasted into names | High | Minor | Trim and normalise on save — otherwise " Ramesh" and "Ramesh" become two patients, feeding EC-27 | 1 |
| EC-66 | Emoji or pasted rich formatting in complaints | Low | Minor | Accept and store the text; strip formatting on paste so the print layout survives | 1 |

### 8.9 Privacy, access and audit

**Data integrity:** **Mutable history** (EC-69, no trail) plus exposure risks that are not integrity failures but are the most consequential items for a clinic.

| ID | Scenario | Likelihood | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| EC-67 | Screen left unlocked between patients | High | Critical | App-level auto-lock, re-auth to unlock, draft preserved (EC-43). The most likely real-world breach in a small clinic | 1 |
| EC-68 | Exported CSV/PDF sits unencrypted in Downloads indefinitely | High | Critical | Scoped exports (D-6), confirmation naming the record count, audit entry, plus a stated operational instruction to the clinic. The app cannot control the filesystem — say so rather than pretending otherwise | 1 |
| EC-69 | No audit trail — "what was prescribed, when, and was it changed?" | Med | Critical | Minimal append-only audit log: finalize, amend, archive, delete, export, login | 1 |
| EC-70 | Single-user password lost — who resets it? | Low | Critical | There is no second user. A defined recovery path is required, or the clinic can be locked out of every record it owns | 1 |
| EC-71 | Right to be forgotten vs. required medical-record retention | Low | Critical | **The BRD is silent and I will not invent a jurisdiction.** The owner states a retention period and deletion policy before go-live; archive-not-delete (EC-34) is the safe interim | 1 (policy) |
| EC-72 | Browser autofill or cached form data on a shared clinic PC | Med | Major | Disable autocomplete on patient fields; ensure the login form does not offer to store credentials on a shared machine | 1 |
| EC-73 | Patient data in browser history, local storage or the back-button cache | Med | Major | Avoid patient identifiers in URLs; clear cached drafts on logout; no-store on sensitive views | 1 |
| EC-74 | Doctor lets a family member or assistant use the logged-in session | Med | Major | Out of technical scope for single-user Phase 1; the audit log at least records that something happened | accepted |
| EC-75 | Prescription printed to a shared or networked printer | Med | Major | Outside the app's control; flag as an operational risk the clinic owns | accepted |

---

## 9. Risk register

Ordered by **build-readiness first** (Blockers lead), then likelihood × impact within each band — because a Blocker stops work regardless of how elegantly the rest is written. Rubric in §3.

| ID | Risk | Underlying gap (build-readiness) | Likelihood | Impact | Data integrity | Mitigation |
|---|---|---|---|---|---|---|
| RISK-1 | In-progress consultation lost to refresh, crash or session expiry — directly violates "No data loss" | C-19 **Blocker** | High | Critical | Silent loss | R-1 |
| RISK-2 | Duplicate patient records split clinical history | C-13 **Blocker** | High | Critical | Duplicate | R-2 |
| RISK-3 | Mandatory vitals with no exception path drive fabricated values into clinical records | C-20 **Blocker** | High | Critical | Fabrication / silent loss | R-3 |
| RISK-4 | Deleting a patient destroys or detaches their visit history | C-14 **Blocker** | Med | Critical | Orphan | R-4 |
| RISK-5 | Two-tab editing silently discards work | C-19 **Blocker** | Med | Critical | Silent loss | R-1 |
| RISK-6 | No record of what was printed; paper and record diverge after an edit | C-27 **Blocker** | High | Major | Mutable history | R-5 |
| RISK-7 | Prescription cannot be rendered at all — no clinic-profile entity exists | C-25 **Blocker** | High | Major | None | R-6 |
| RISK-8 | Appointment status has no link to the visit that justifies it; daily list and history never reconcile | C-18 **Blocker** | High | Major | Orphan | R-7 |
| RISK-9 | Single-user credential loss locks the clinic out of all records, permanently | C-35 **Blocker** | Low | Critical | Silent loss (total) | R-8 |
| RISK-10 | Deployment model unstated, so backup, encryption-at-rest and outage behaviour are each built on an unexamined assumption | C-4 / C-36 Needs decision | High | Critical | Silent loss | R-9 |
| RISK-11 | Silent backup failure discovered only when a restore is needed | C-33 Needs decision | Med | Critical | Silent loss | R-10 |
| RISK-12 | Unattended unlocked screen exposes every patient record | C-34 Needs decision | High | Critical | Exposure | R-11 |
| RISK-13 | Unencrypted broad export left on the clinic PC; CSV silently mangled or weaponised | C-6 Needs decision | Med | Critical | Silent loss + exposure | R-12 |
| RISK-14 | No audit trail; cannot evidence what was prescribed or changed | C-37 Needs decision | Med | Critical | Mutable history | R-13 |
| RISK-15 | No retention or deletion policy; the clinic cannot answer a deletion request or a retention obligation | C-38 Needs decision | Low | Critical | Mutable history / Orphan | R-14 |
| RISK-16 | "Open Questions: None" causes every decision above to be made implicitly, in code | C-41 Needs decision | High | Major | All four | R-15 |
| RISK-17 | 2–3 minute target treated as a pass/fail gate against an unrealistic workflow | C-8 Needs decision | High | Major | Silent loss (rushed abandonment) | R-16 |
| RISK-18 | Undefined appointment transitions built inconsistently | C-17 Needs decision | Med | Major | Mutable history | R-7 |
| RISK-19 | Prescription print layout breaks on overflow or across the three supported browsers | C-40 Ready | High | Major | Silent loss (clipped content) | R-17 |
| RISK-20 | Search too slow or too coarse to be used before registration, defeating duplicate prevention | C-9 Ready | Med | Major | Duplicate | R-18 |
| RISK-21 | Age stored without a capture date silently misrepresents the patient years later | C-12 Needs decision | High | Minor | Mutable history | R-19 |
| RISK-22 | Untestable success criteria ("smooth", "minimal training", "80% paper") signed off charitably, hiding real usability failures | C-10 / C-11 Needs decision | Med | Minor | None | R-16 |

---

## 10. Prioritized recommendations

**Build-readiness drives this order, not clarity.** Blocker-clearing work first — nothing downstream can be built cleanly ahead of it. Then owner decisions, then Ready improvements sequenced by risk reduced per unit of effort.

### Band A — Clears a Blocker

| ID | Recommendation | Effort | Clears | Addresses | Data integrity |
|---|---|---|---|---|---|
| R-1 | Adopt the autosave-draft → finalize → append-only-amendment consultation lifecycle (D-1 D), including two-tab detection and an unload guard | M | C-19 | RISK-1, RISK-5; EC-33, EC-42, EC-43, EC-44 | Closes Silent loss + Mutable history |
| R-2 | Make registration reachable only from a search that returned no match, plus a near-match warning on save (D-2 D+C) | M | C-13 | RISK-2; EC-27–EC-31, EC-65 | Closes Duplicate |
| R-3 | Replace hard-blocking mandatory vitals with "value **or** explicit not-recorded reason"; structure BP as two numeric fields and temperature as value + unit (D-7 C) | S | C-20 | RISK-3; EC-19, EC-64 | Removes fabrication vector |
| R-4 | Define the patient record lifecycle: archive, never hard-delete, for any patient with visits | S | C-14 | RISK-4; EC-29, EC-34 | Closes Orphan |
| R-5 | Record every prescription issue with an immutable printed snapshot; flag reprints and amended reissues (D-4 C) | M | C-27 | RISK-6; EC-35, EC-56, EC-58 | Closes Mutable history on the printed artefact |
| R-6 | Add the ClinicProfile entity and force its setup on first run; block printing until complete | S | C-25 | RISK-7; EC-1, EC-15 | None (unblocks the deliverable) |
| R-7 | Add the optional appointment ↔ visit link that auto-completes the appointment on finalize, and adopt the §7.2 state machine including No-show → Completed and the Overdue display state | M | C-18 | RISK-8, RISK-18; EC-38, EC-39, EC-40 | Closes Orphan |
| R-8 | Define a credential recovery path for the single user before go-live | S | C-35 | RISK-9; EC-70 | Prevents total Silent loss |

### Band B — Needs an owner decision, then build

| ID | Recommendation | Effort | Addresses | Data integrity |
|---|---|---|---|---|
| R-9 | Owner names the deployment model and the backup owner (D-8 / OQ-6); C-33 and C-36 are then rewritten against it, including whether exports and generated PDFs are covered by "at rest" | S (owner) + M | RISK-10; EC-49, EC-52, EC-68 | Silent loss |
| R-10 | Make backup status visible with a last-success timestamp; rehearse a full restore before go-live; state a recovery objective in place of "No data loss" (B-3) | M | RISK-11; EC-51, EC-53, EC-54 | Closes Silent loss |
| R-11 | App-level auto-lock that preserves the in-progress draft; disable autofill on patient fields; set session policy from the device answer (OQ-12) | S | RISK-12; EC-67, EC-72, EC-73 | Exposure |
| R-12 | Scope exports to the current view, confirm with a record count, write an audit entry, escape CSV correctly and neutralise formula prefixes (D-6 B+C+F) | M | RISK-13; EC-59, EC-61, EC-68 | Closes Silent loss in the export artefact |
| R-13 | Add a minimal append-only audit log: finalize, amend, archive, delete, export, login | M | RISK-14; EC-69, EC-30, EC-36, EC-37 | Closes Mutable history |
| R-14 | Owner states a retention period and deletion policy; archive-not-delete stands as the interim default | S (+ owner) | RISK-15; EC-71 | Mutable history / Orphan |
| R-15 | Replace "Open Questions: None" in the BRD with the §12 table | S (owner) | RISK-16; B-4 | All four |
| R-16 | Restate the 2–3 minute criterion as a median for a 1–2 medication visit; add an explicit keyboard-only completion path requirement; replace "80% paper", "smooth" and "minimal training" with countable proxies (B-1, B-5) | S (owner) | RISK-17, RISK-22 | Silent loss (rushed abandonment) |
| R-19 | Store age-at-registration with its capture date alongside optional DOB; support sub-year age display (D-3 C) | S | RISK-21; EC-12, EC-14, EC-21 | Closes Mutable history |

### Band C — Ready to build now; sequence by risk reduced per unit of effort

| ID | Recommendation | Effort | Addresses | Data integrity |
|---|---|---|---|---|
| R-17 | Specify the print layout: multi-page rules, repeating header, page numbering, long-text wrapping, script-capable fonts, and verified print output on Chrome, Edge and Safari | M | RISK-19; EC-10, EC-11, EC-15, EC-62 | Closes Silent loss (clipped content) |
| R-18 | Type-ahead search under 300ms with digits-normalised phone matching and diacritic-insensitive name matching; visit-history load under 2s (B-6) | M | RISK-20; EC-7, EC-31, EC-63 | Supports Duplicate prevention |
| R-20 | Add a pre-finalize review screen showing the medication list exactly as it will print | S | EC-9, EC-23 | None (review, not validation) |
| R-21 | Purposeful empty states everywhere, each offering the next action | S | EC-4, EC-5, EC-6, EC-7, EC-8 | None |
| R-22 | Escape all free text on output including the print view; trim and normalise whitespace on save; strip formatting on paste; state max lengths with counters | S | EC-11, EC-60, EC-65, EC-66 | Closes Duplicate (whitespace variants) |
| R-23 | Fix `clinic_date` at draft creation, store instants, render in one configured clinic timezone | S | EC-47, EC-48 | Prevents misdated records |
| R-24 | Pin patient identity on screen throughout the consultation, and show a disambiguator (phone tail + last visit) in every patient picker | S | EC-28, EC-30; C-30 | Reduces wrong-patient records |

---

## 11. Phase 2+ / parking lot

**Everything deferred lives here and only here** — including the items marked `accepted` in §8, which point into this table rather than forming a second list.

| Item | Why it's deferred | Pull-forward condition | Accepted risk while deferred |
|---|---|---|---|
| P-1 Duplicate merge tooling (re-parent visit history) | Real design effort; prevention (R-2) removes most of the need | A duplicate pair with visits on *both* records occurs more than twice | A split history exists until manually reconciled; the doctor may decide on an incomplete record. Archive + pointer note is the interim (EC-29) |
| P-2 Follow-up alerts and reminders | Explicitly out of scope in the BRD | Owner reprioritises | A forward-dated appointment may simply be forgotten (EC-41) |
| P-3 Password-protected PDF / passphrase-protected CSV | Friction now, and the passphrase ends up on a sticky note | Exports routinely leave the clinic premises | Exported files are readable by anyone with access to the machine (EC-68) |
| P-4 Receptionist / multi-user access | Explicitly out of scope | Clinic hires front-desk staff | Session sharing is invisible to the system (EC-74) |
| P-5 Structured diagnosis coding (ICD or similar) | Free text meets the Phase 1 need; coding costs entry time against the 2–3 minute target | Reporting or referral requirements appear | History searchable only as free text; no aggregation |
| P-6 Medication master list / drug-name autocomplete | Third-party data dependency, and it edges toward clinical-advice territory | Doctor reports repeatedly typing the same drug names | Spelling variants make history search unreliable |
| P-7 Advanced analytics and reporting | Explicitly out of scope | Owner asks for volume or trend reporting | No visibility into clinic patterns; scoped CSV export is the manual workaround |
| P-8 Offline mode | Explicitly out of scope | Clinic connectivity proves unreliable in practice | A network outage stops consultations; EC-50 buffers only briefly. Severity depends on OQ-6 |
| P-9 Mobile app | Explicitly out of scope | Doctor consults away from the clinic desk | Responsive browser use untested on small screens |
| P-10 Lab / pharmacy integration, billing, insurance | Explicitly out of scope | Not before Phase 2 | Those workflows stay on paper or in other tools |
| P-11 Patient-facing access to their own records | Never in scope; large privacy surface | A regulatory requirement appears | Patients hold only the printed prescription |
| P-12 Pagination and virtualised lists | Volume does not justify it for one physician | A daily list or history routinely exceeds a few hundred rows | Rendering slows on unexpectedly large lists (EC-18) |
| P-13 Vitals trend charting across visits | Display-only; adds no data | Doctor asks to see BP over time | Trends must be read visit-by-visit |
| P-14 End-of-day reconciliation view (Scheduled with no visit) | A nicety once R-7 keeps statuses honest | Doctor reports stale Scheduled rows accumulating | Overdue rows accumulate in the daily list until resolved manually |
| P-15 Configurable prescription templates / multiple print formats | One well-tested layout beats several fragile ones | Doctor needs a second document type | Print layout fixed; changes require a code change |
| P-16 Full field-level change history beyond the R-13 event log | Disproportionate for one user | An external audit or a dispute occurs | Audit answers "what happened" but not always "exactly what changed" |
| P-17 Shared-printer and unattended-print controls | Outside the application's control entirely | Clinic layout changes so the printer is not within the doctor's sight | A printed prescription may be collected by the wrong person (EC-75) |

---

## 12. Open questions for the product owner

Priority combines severity **with cost to resolve** — a Critical gap that closes with one meeting decision outranks a Major one costing two weeks of design.

| ID | Question | Cost to resolve | Unblocks | Priority | Blocks build? |
|---|---|---|---|---|---|
| OQ-1 | When vitals genuinely cannot be taken, what should happen — hard block, "unable to record" reason, or free override? | Policy call | C-20 **Blocker** | 1 | Yes |
| OQ-2 | May a finalized consultation be edited after printing, and must the change be visibly marked as an amendment? | Policy call | C-19, C-27 **Blockers** | 2 | Yes |
| OQ-3 | Can a patient record ever be deleted, or only archived? What happens to their visits? | Policy call | C-14 **Blocker** | 3 | Yes |
| OQ-4 | What exactly goes in the prescription header and footer — registration number, qualifications, logo, signature block — and which patient fields print? | Policy call | C-25 **Blocker**, C-26 | 4 | Yes |
| OQ-5 | Can a consultation exist without an appointment, and should finalizing one auto-complete its appointment? | Policy call | C-18 **Blocker** | 5 | Yes |
| OQ-6 | What is the deployment model — hosted, clinic server, or single PC — and who operates backups? | Policy call (with ops input) | C-4, C-33, C-36 | 6 | Yes |
| OQ-7 | What is the acceptable loss window for an interrupted consultation — the number that replaces "No data loss"? | Policy call | C-33, C-19 | 7 | Yes |
| OQ-8 | How is the single user's password recovered if lost? | Design effort | C-35 **Blocker** | 8 | Yes |
| OQ-9 | Is DOB required, optional, or replaced by age? If age, is age-at-registration + capture date acceptable? | Policy call | C-12 | 9 | Yes |
| OQ-10 | Is appointment scheduling a time-slot calendar or a simple dated list? Are overlaps and same-day repeats allowed? | Policy call | C-16, C-17 | 10 | No |
| OQ-11 | What retention period applies to patient records, and is deletion ever permitted? | Policy call (may need legal input) | C-38 | 11 | No |
| OQ-12 | Is the app used on a shared clinic PC or a private device? (Drives auto-lock, autofill, cache policy.) | Policy call | C-2, C-34 | 12 | No |
| OQ-13 | Is export limited to the current view, or is a full-database export required — and who may use it, for what? | Policy call | C-6 | 13 | No |
| OQ-14 | Which of the five medication fields are required, and is diagnosis mandatory before printing? | Policy call | C-23, C-24 | 14 | No |
| OQ-15 | Is an audit trail in Phase 1 scope, and which events must it cover? | Policy call | C-37 | 15 | No |
| OQ-16 | What is the exact gender value list, and does it include "unspecified"? | Policy call | C-12 | 16 | No |
| OQ-17 | What baseline does "80% paper reduction" measure against, and what replaces "smooth" and "minimal training" as testable criteria? | Policy call | C-10, C-11 | 17 | No |
| OQ-18 | Does "recent patients" mean recently *viewed* or recently *consulted*, and how many are shown? | Policy call | C-29 | 18 | No |
| OQ-19 | Should duplicate *merge* exist in Phase 1, or is archive-and-re-enter acceptable? | Design effort | C-13 (partial) | 19 | No |

**Five policy calls — OQ-1, OQ-2, OQ-3, OQ-4, OQ-5 — clear six of the eight Blockers (C-14, C-18, C-19, C-20, C-25, C-27) and are answerable in a single meeting.** The remaining two Blockers need design work, not just a decision: C-13 patient identity (R-2, plus OQ-19) and C-35 credential recovery (OQ-8). That meeting is the highest-leverage hour available to this project.

---

## 13. Cross-reference index

The same gap surfaces once as a question for the owner, once as a change for the team, and once as a risk. This maps them so nobody counts a single decision twice.

| Open question | Coverage row(s) | Recommendation | Risk | Key edge cases |
|---|---|---|---|---|
| OQ-1 vitals exception | C-20 **Blocker** | R-3 | RISK-3 | EC-19, EC-64 |
| OQ-2 edit after print | C-19, C-27 **Blockers** | R-1, R-5 | RISK-1, RISK-5, RISK-6 | EC-33, EC-35, EC-56 |
| OQ-3 delete vs. archive | C-14 **Blocker** | R-4 | RISK-4 | EC-29, EC-34 |
| OQ-4 header/footer + printed patient fields | C-25 **Blocker**, C-26 | R-6 | RISK-7 | EC-1, EC-15 |
| OQ-5 appointment ↔ visit link | C-18 **Blocker** | R-7 | RISK-8, RISK-18 | EC-38, EC-39, EC-40 |
| OQ-6 deployment model + backup owner | C-4, C-33, C-36 | R-9, R-10 | RISK-10, RISK-11 | EC-49, EC-51, EC-52, EC-68 |
| OQ-7 acceptable loss window | C-33, C-19 | R-1, R-10 | RISK-1, RISK-11 | EC-42, EC-50, EC-53 |
| OQ-8 password recovery | C-35 **Blocker** | R-8 | RISK-9 | EC-70 |
| OQ-9 DOB vs. age | C-12 | R-19 | RISK-21 | EC-12, EC-14, EC-21 |
| OQ-10 scheduling model | C-16, C-17 | R-7 | RISK-18 | EC-32, EC-38, EC-39, EC-41 |
| OQ-11 retention / deletion | C-38 | R-14 | RISK-15 | EC-71 |
| OQ-12 shared vs. private device | C-2, C-34 | R-11 | RISK-12 | EC-67, EC-72, EC-73 |
| OQ-13 export scope | C-6 | R-12 | RISK-13 | EC-59, EC-61, EC-68 |
| OQ-14 required med fields / diagnosis | C-23, C-24 | R-20 | — | EC-3, EC-23, EC-24 |
| OQ-15 audit trail scope | C-37 | R-13 | RISK-14 | EC-30, EC-36, EC-69 |
| OQ-16 gender value list | C-12 | — (build follows the decision) | — | EC-25 |
| OQ-17 testable success criteria | C-10, C-11, C-8 | R-16 | RISK-17, RISK-22 | — |
| OQ-18 "recent patients" meaning | C-29 | — (build follows the decision) | — | EC-4, EC-6 |
| OQ-19 merge in Phase 1 | C-13 **Blocker** | R-2 (prevention), P-1 (cure) | RISK-2 | EC-27, EC-28, EC-29 |
| — no owner decision needed | C-41 | R-15 | RISK-16 | — |
| — no owner decision needed | C-40 | R-17 | RISK-19 | EC-10, EC-11, EC-62 |
| — no owner decision needed | C-5, C-9, C-15 | R-18 | RISK-20 | EC-7, EC-31, EC-63 |
| — no owner decision needed | C-22 | R-22 | — | EC-60, EC-65, EC-66 |
| — no owner decision needed | C-28 | R-23 | — | EC-47, EC-48 |
| — no owner decision needed | C-30 | R-24 | — | EC-28, EC-30 |
| — no owner decision needed | C-21, C-31 | R-3, R-16 | RISK-3, RISK-17 | EC-13, EC-64 |
| — no change needed | C-1, C-3, C-7, C-32, C-39 | — | — | — |

---

## 14. Recommendation

**Build R-1 first.** The autosave-draft → finalize → append-only-amendment lifecycle is the single change that turns "no data loss" into a testable property rather than a slogan, and it costs the doctor nothing against the 2–3 minute target because it adds no clicks to the normal path. It also clears C-19, the Blocker that six other findings depend on.

**Top unresolved edge case:** OQ-2 — whether a finalized consultation may be edited after the prescription is printed. Until the owner answers, the boundary between "draft" and "permanent record" is undefined, and audit, reprint, amendment display and history rendering are all blocked behind it.

**Data integrity:** R-1 closes **Silent loss** (the draft is persisted from the first keystroke and the loss window is bounded and stated) and **Mutable history** (finalized records are immutable; corrections append with a trail). It does not touch **Duplicate** — that is R-2's job — and it introduces one new obligation: drafts must always be visible in history, or an abandoned draft becomes a record nobody knows exists.

**Honest trade-off:** the amendment model makes history rendering more complex and requires the doctor to understand that corrections append rather than overwrite. A doctor who expects "edit" to mean "change it" will find the first amendment surprising and needs one sentence of onboarding. The cheaper alternative (D-1 option C — autosave and finalize, with post-finalize edits simply overwriting) is defensible for a single-user clinic where the doctor is also the only auditor — but it must then be recorded explicitly as an accepted risk with no audit answer, not left unstated.

**Second honest trade-off, at the document level:** this review adds work to a BRD whose greatest virtue is restraint. The Out of Scope section (C-7) is the best-written part of the document and should not be touched. Nothing recommended here adds a Phase 1 feature the doctor asked for; all of it is lifecycle, identity and recovery machinery that the listed features silently assume.

---

## 15. Consciously not handling in Phase 1

Everything deliberately left out lives in the parking lot (§11) with its reason, its pull-forward condition and the risk accepted in the meantime. That table is the single home for deferred items — the `accepted` markers in §8 (EC-18, EC-41, EC-74, EC-75) point into it rather than forming a second list, because they carry no build work and are risks the clinic absorbs operationally rather than work that was scheduled and dropped.
