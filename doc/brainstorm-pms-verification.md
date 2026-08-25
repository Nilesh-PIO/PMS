# Patient Management Application — Whole-Document BRD Brainstorm + Edge-Case Verification

- **Document under review:** `BRD/Doc_BRD.md` (198 lines, reviewed in full)
- **Review type:** Large — whole-document brainstorm + nine-category edge-case verification. Refresh: re-derived from the current BRD, not carried over from the previous version of this file.
- **Date:** 2026-08-20
- **Scope of review:** Phase 1 only (one general physician, one clinic)
- **Status:** Pre-build readiness review. This is brainstorming and analysis — not implementation design, not a spec, and not authorisation to build.

---

## 1. Headline

**The BRD describes a product but never describes a record.** It lists what the doctor can enter, and almost never says when that entry becomes permanent, what it is attached to, or what happens when the entry is interrupted. The document then closes with "Open Questions: None," which shuts the door on exactly the conversation that must happen before code is written.

**Biggest single gap: the consultation has no lifecycle and no commit model.** There is no draft state, no defined moment when a visit becomes a permanent clinical record, no rule for editing after a prescription has been printed and handed to a patient, and no named entity ("visit") linking vitals, complaints, diagnosis, medications and the appointment together. This sits directly beneath the non-functional requirement **"No data loss,"** which the BRD as written cannot satisfy or test.

**Close behind:** patients have **no identity rule** (duplicates are a matter of when, not if); the **prescription header has no home** (nothing in the BRD creates clinic/doctor settings, yet the printed output requires them); and **"web-based access" never names a deployment model**, which leaves "encryption at rest," "automated backups," and the whole offline/uptime question unbuildable as written.

**Top pick — REC-1:** define the consultation as a **continuously autosaved draft that is explicitly finalized at print**, with finalized visits immutable and later corrections appended as dated amendments. It turns "no data loss" into a testable property, gives the appointment state machine something real to hang "Completed" on, and costs the doctor **zero extra clicks** against the 2–3 minute target.

**Data integrity (headline level):** the BRD as written is exposed to all four failure modes at once — **Duplicate** (no patient identity rule, no merge concept), **Orphan** (no visit entity; appointments and clinical data have no defined parent; patient deletion undefined), **Mutable history** (no amendment or audit concept, so a prescription can silently change meaning after the patient has walked out with the paper copy), and **Silent loss** (no save model, no recovery objective, no backup-failure signal). Every finding below is scored against these four.

**Counts:** 49 coverage rows — **10 Blockers · 17 Needs decision · 22 Ready**. 66 edge cases swept, of which **22 are "corrupts data or loses a record"** and are separated from the merely ugly in §8 and §14.

---

## 2. Frame

The decision this review supports: **is `BRD/Doc_BRD.md` complete enough to hand to a development team, and if not, exactly which gaps must close first — and at what cost to close each?**

I read this as a build-authorisation document, not a vision statement. No clarifying question is needed: under the vision reading the finding list is identical and only the urgency drops, so proceeding is safe either way.

---

## 3. Rubric — stated once, used by every rated table below

**Clarity verdict** — could a developer build this as written?
| Value | Meaning |
|---|---|
| **Clear** | Unambiguous. Two developers build the same thing. |
| **Ambiguous** | Buildable, but two developers would reasonably build it differently. |
| **Missing detail** | The BRD does not state a decision the build requires. |
| **Contradiction** | Conflicts with another BRD statement, or with how a real clinic operates. |

**Build-readiness** — does the gap actually stop work? *(Independent of clarity.)*
| Value | Meaning |
|---|---|
| **Ready** | Build as-is, or with an obvious low-risk default that this review names. |
| **Needs decision** | Buildable only after the owner makes one explicit, nameable call. |
| **Blocker** | Cannot be built at all: missing entity, safety issue, or a contradiction that breaks another requirement. |

An item can be `Ambiguous` and still `Ready` (name a sane default, record the assumption). An item can be perfectly `Clear` and still a `Blocker` — see **C-32**, a well-written prescription requirement with no settings entity to hang its header on. **Build-readiness, not clarity, drives priority** in §9 and §10.

**Data-integrity exposure** — the four failure modes, applied to every idea and every finding:
| Value | Meaning |
|---|---|
| **Duplicate** | Two records can describe the same real-world fact. |
| **Orphan** | A record can lose its parent, or point at something that no longer exists. |
| **Mutable history** | A recorded fact can silently change meaning later, with no trail. |
| **Silent loss** | A write can be lost between "the doctor typed it" and "it's on disk," with no signal. |
| **None** | No integrity exposure. |

**Likelihood** — **High:** expected in normal single-clinic use within the first month (roughly weekly or more) · **Med:** plausible within the first year · **Low:** needs unusual circumstances; may never occur in this clinic's lifetime.

**Impact** — **Critical:** silent data loss or corruption; clinical data or a prescription attached to the wrong patient; an unrecoverable record; or patient health data exposed outside the clinic · **Major:** blocks or stalls a live consultation, forces re-entry, or produces a record the doctor cannot trust without a second source · **Minor:** cosmetic or trivially recoverable; no record affected, seconds of time at most.

**Effort** (sequencing only — not an estimate) — **S:** under a day · **M:** two to five days · **L:** over a week, or a product decision plus a build.

**Phase** — **1:** build now · **later:** parking lot (§11) · **accepted:** knowingly unhandled, risk absorbed.

**Cost to resolve** (open questions, §12) — **Policy call:** the owner can settle it in a meeting · **Design + build:** requires real design and engineering work, not just a decision.

---

## 4. Coverage map — every BRD section, both labels

### 4.1 Framing sections (lines 1–82)

| ID | BRD line/section | Clarity | Build-ready | Integrity | Finding |
|---|---|---|---|---|---|
| C-1 | Product Goal (L5–7) | Clear | Ready | None | Good framing. "Efficiently" and "accurate" are not testable, but Success Criteria carry that load. |
| C-2 | "General Physician (Single User)" (L14) | Ambiguous | Needs decision | Mutable history | "Single user" is doing three different jobs: one login, one person, one concurrent session. Only the first is stated. Two browser tabs, and a locum covering a sick day, are both unaddressed. |
| C-3 | Secondary users: None (L17) | Clear | Ready | None | Consistent with scope boundary. |
| C-4 | Stakeholders (L19–22) | Clear | Ready | None | No gap. |
| C-5 | Problem Statement (L26–34) | Clear | Ready | None | Well-argued. "Risk of lost or incomplete records" is the paper problem the BRD must not reproduce digitally — see C-42. |
| C-6 | Scope: "Web-based access (browser-based system)" (L42) | Missing detail | **Blocker** | Silent loss | **No deployment model.** Clinic PC? LAN server? Public cloud? This single unstated decision determines encryption-at-rest (C-45), backup destination (C-43), session security (C-44), whether an ISP outage stops the clinic (C-9), and whether "two tabs" is even possible. Nothing downstream can be designed without it. |
| C-7 | Scope: "Data export (CSV/PDF)" (L52) | Ambiguous | Needs decision | None (privacy risk) | No granularity (one patient? one visit? whole database?), no trigger, no destination. Every export is an unencrypted PHI copy leaving the app's control. |
| C-8 | Out of Scope list (L58–69) | Clear | Ready | None | Well-drawn and respected throughout this review. |
| C-9 | Out of scope "Offline functionality" (L65) vs NFR "No data loss" (L181) | **Contradiction** | Needs decision | Silent loss | If deployed off-site, a dropped connection mid-consultation means either a stalled clinic or lost typing. "No offline" is defensible only under a local deployment (C-6), or by accepting that the clinic stops when the network does. |
| C-10 | Out of scope "Follow-up alerts/reminders" (L69) | Clear | Ready | None | Consistent — but the prescription's own **Duration** field creates a follow-up expectation the product will not meet. Accepted risk, named in §11. |
| C-11 | Success: consultation record in 2–3 min (L75) | Ambiguous | Needs decision | None | Clock start and stop points undefined, and the content of a "typical" consultation is unspecified. Untestable as written. See §5.1. |
| C-12 | Success: search/history in 2–5 s (L76) | Ambiguous | Ready | None | No percentile, no dataset size. Default worth naming: p95 ≤ 2 s at 5,000 patients / 25,000 visits, measured from keystroke to rendered result. |
| C-13 | Success: 80% reduction in paper (L77) | Ambiguous | Ready | None | No baseline is recorded anywhere, and the product's flagship output is a **printed** prescription. Measures the wrong thing — see §5.5. Not a build gate. |
| C-14 | Success: "Smooth generation and printing" (L78) | Ambiguous | Ready | None | "Smooth" is unmeasurable. Default: prescription preview renders ≤ 1 s and fits one page for a defined typical visit. |
| C-15 | Success: successful CSV/PDF export (L79) | Clear | Ready | None | Testable once C-7 defines what "data" means. |
| C-16 | Success: "minimal training required" (L80) | Ambiguous | Ready | None | Fine as intent. Default acceptance: the doctor completes an unassisted consultation on first use after a ≤10-minute walkthrough. |

### 4.2 Functional Requirements — Patient Management (lines 86–93)

| ID | BRD line/section | Clarity | Build-ready | Integrity | Finding |
|---|---|---|---|---|---|
| C-17 | "Add, edit, and view patient details" (L87) | Missing detail | **Blocker** | Mutable history, Orphan | **No delete or deactivate**, and no rule about editing. Can a patient registered in error be removed? What happens to their visits? Can a name or DOB be corrected after a prescription was printed with the old value — and does the printed record then disagree with the stored record? |
| C-18 | Field: Name (L89) | Missing detail | Needs decision | Duplicate | One field or first/last? Mononyms (no surname) are common and a required-surname design rejects real patients. No length or script rule. Name is currently the primary human identifier, which makes this a duplicate-risk field, not a cosmetic one. |
| C-19 | Field: "Age / DOB" (L90) | Ambiguous | Needs decision | **Mutable history** | The slash hides an unmade decision. Storing **age** is storing a fact that silently becomes wrong — a record reading "34" gives no way to know if that meant 2026 or 2019. Storing **DOB** is correct but often unknown in this setting. Recommended: DOB when known, else `approx_age` + `age_recorded_on`, never a bare mutable age. |
| C-20 | Field: Gender (L91) | Missing detail | Needs decision | None | No value list. Free text guarantees "M"/"Male"/"male" in the same column and breaks any later filtering; a fixed list needs the owner to choose it. Pure policy call. |
| C-21 | Field: "Contact details" (L92) | Missing detail | **Needs decision** | Duplicate | Undefined shape (phone? multiple? address? email?), and **not stated as mandatory** — yet C-22 makes phone a primary search key. A patient with no phone is invisible to half the search. Also: one phone shared across a family is normal, so phone is an identifier for a *household*, not a person. |
| C-22 | "Search patients by name or phone number" (L93) | Ambiguous | Needs decision | None | Prefix or substring? Fuzzy? Last-4-digits match (which is what a doctor actually remembers)? Result ranking undefined. Interacts with C-21: the search key is optional at registration. |
| C-23 | *Absent:* patient uniqueness / identity rule | Missing detail | **Blocker** | **Duplicate** | The BRD never says what makes two patients the same person. With no duplicate warning at registration and no merge path, one patient's history will split across two records — and the doctor will make decisions from half a history without knowing it. This is the second-most consequential gap in the document. |

### 4.3 Functional Requirements — Appointment Management (lines 98–104)

| ID | BRD line/section | Clarity | Build-ready | Integrity | Finding |
|---|---|---|---|---|---|
| C-24 | "Schedule appointments" (L98) | Missing detail | **Blocker** | Orphan | No time model at all: date only or date+time? Fixed slots or free times? Default duration? **Are walk-ins supported** — i.e. can a consultation exist without an appointment? In a small GP clinic most patients walk in, so this is the common case, not the exception. |
| C-25 | "View daily appointment list" (L99) | Missing detail | Ready | None | Default day, sort order, and empty state unspecified. Low-risk defaults: today, sorted by time then creation, with an explicit empty state that offers "start a walk-in consultation." |
| C-26 | Status: Scheduled / Completed / Cancelled / No-show (L100–104) | Missing detail | **Blocker** | **Mutable history** | Four states, **zero transitions defined.** Cancelled → Completed? No-show → Completed when the patient turns up 40 minutes late? Completed → Scheduled (an undo that silently detaches a real consultation)? Nothing says what happens to yesterday's still-Scheduled rows at midnight, or whether status changes leave any trace. |
| C-27 | *Absent:* the **visit / consultation** entity and its link to the appointment | Missing detail | **Blocker** | **Orphan** | Vitals, complaints, diagnosis, medications, prescription and history all implicitly belong to something the BRD never names. Without it, "Completed" is a label nobody can verify, patient history has nothing to list, and clinical rows have no parent. This is the structural root of C-17, C-26, C-33 and C-42. |

### 4.4 Functional Requirements — Consultation Workflow (lines 108–142)

| ID | BRD line/section | Clarity | Build-ready | Integrity | Finding |
|---|---|---|---|---|---|
| C-28 | Vitals "(Mandatory)": Temperature, BP, Pulse (L110–114) | **Contradiction** | **Blocker** | Silent loss | **Mandatory with no escape hatch will be defeated, not obeyed.** Broken cuff, uncooperative toddler, patient who refuses, a two-minute repeat-prescription visit. A hard block sends the doctor to paper or to junk values (`0/0`, `999`) that pollute history forever. Also missing: **units** (°C/°F, kPa/mmHg), format for BP (systolic/diastolic as one field or two), and whether any plausibility range exists. See §5.2. |
| C-29 | Complaints, free text (L119) | Missing detail | Ready | None | No length cap, no paste handling, no structure. Defaults: soft cap ~2,000 chars with a visible counter, hard cap ~10,000, trim whitespace, preserve line breaks. |
| C-30 | "Record diagnosis notes" (L124) | Missing detail | Needs decision | None | Mandatory or optional is unstated — and it directly governs whether a prescription may be printed with a blank diagnosis, which is a records-quality question the owner must answer, not the developer. |
| C-31 | Medication: Name, Dosage, Frequency, Duration, Instructions (L129–134) | Missing detail | Needs decision | Duplicate | All free text with no medicine master, so "Amoxicillin", "amoxycillin" and "AMOX 500" become three unrelated strings and history search is unreliable. Field formats undefined (is Dosage "500 mg" or number+unit?). Zero-medication consultations (advice only) are not addressed. A typo guard (5 vs 50 mg) is worth raising, but **any range rule is the doctor's to define — the system enforces it, this review does not author it.** |
| C-32 | Printable prescription: header, patient details, vitals, diagnosis, medications, footer/signature (L136–142) | **Clear** | **Blocker** | Mutable history | A well-written requirement with **nothing to hang it on**: no BRD section creates clinic name, address, doctor name, registration number, or signature image. There is also no prescription identity (number/date stamp), no reprint rule, and no page-overflow behaviour. Textbook `Clear` + `Blocker`. |

### 4.5 Functional Requirements — History, Search, Export (lines 146–168)

| ID | BRD line/section | Clarity | Build-ready | Integrity | Finding |
|---|---|---|---|---|---|
| C-33 | Patient History: previous visits, vitals, complaints, diagnosis, prescriptions (L147–152) | Ambiguous | Ready | Orphan | Ordering unspecified (newest-first is the obvious default). Unstated: do **unfinished drafts** appear in history? They must be visibly distinct from finalized visits, or the doctor reads an abandoned draft as a real record. |
| C-34 | History: "Filter by date" (L153) | Ambiguous | Ready | None | Single date, range, or preset? Inclusive bounds? What renders when the filter matches nothing? Default: inclusive range picker with presets, plus an explicit empty state. |
| C-35 | "Quick patient search" (L158) | Ambiguous | Ready | None | Overlaps C-22; needs one search definition, not two. Debounce and minimum query length unspecified. |
| C-36 | "View recent patients" (L159) | Missing detail | Ready | None (privacy risk) | "Recent" by what — viewed, or consulted? How many? Persisted across sessions? On a screen left unlocked between patients, this list is a standing PHI display. |
| C-37 | "Easy navigation between patient profile and visits" (L160) | Ambiguous | Ready | Silent loss | Navigating away mid-consultation is the highest-frequency data-loss path in the product, and the BRD does not mention unsaved-work protection anywhere. |
| C-38 | Export as CSV (L165–167) | Missing detail | Needs decision | None (privacy + integrity of output) | Unaddressed: commas/newlines/quotes inside complaint text splitting rows; **formula injection** where a field beginning `=`, `+`, `-` or `@` executes when opened in Excel; encoding for non-Latin names; and the file landing unencrypted in Downloads. |
| C-39 | Export as PDF (L168) | Ambiguous | Ready | None | Same content question as C-7. Failure path (generation fails after the doctor clicked Export) unspecified. |

### 4.6 Non-Functional Requirements + closing (lines 171–198)

| ID | BRD line/section | Clarity | Build-ready | Integrity | Finding |
|---|---|---|---|---|---|
| C-40 | Usability: "Simple, minimal UI optimized for fast data entry" (L173–174) | Ambiguous | Needs decision | None | The BRD never says **keyboard-driven**, never sets a field-count or click budget, and never names a target device (laptop? desktop with a real keyboard? tablet?). Without those, "optimized for fast entry" cannot be tested against C-11. |
| C-41 | Performance: page load < 2 s; "fast search" (L176–178) | Ambiguous | Ready | None | No percentile, no network assumption, no data volume. "Fast" duplicates C-12 loosely; pick one number and use it in both places. |
| C-42 | Reliability: **"No data loss"** (L180–181) | Missing detail | **Blocker** | **Silent loss** | An absolute with no mechanism behind it. No autosave, no save model, no recovery point objective, no crash-recovery behaviour. As written it is unbuildable and untestable — it is a wish, and it is the wish this whole product depends on. |
| C-43 | Reliability: "Regular automated backups" (L182) | Missing detail | Needs decision | Silent loss | Frequency, destination, retention, encryption, and **whether anyone is told when a backup fails** are all unstated. A silently failing backup is worse than no backup, because it buys false confidence. Restore has never been mentioned at all — an untested restore is not a backup. |
| C-44 | Security: "Secure login (single user authentication)" (L184–185) | Missing detail | Needs decision | None | With exactly one user there is **no one to reset the password** and no one to unlock a locked-out account. Session timeout is unaddressed and directly collides with a consultation in progress (E-41). Password policy, lockout, and recovery all need a named answer. |
| C-45 | Security: "Data encryption (at rest and in transit)" (L186) | Ambiguous | Needs decision | None | Meaningless until C-6 fixes deployment: on a clinic PC "at rest" is disk encryption plus key custody; in cloud it is storage encryption plus TLS. Backups (C-43) and exports (C-38) are the two copies most likely to escape whatever is chosen. |
| C-46 | Scalability: "single clinic, moderate patient volume" (L188–189) | Ambiguous | Ready | None | "Moderate" is unbounded. Name a number so nobody over-engineers: e.g. ≤40 consultations/day, ≤6,000 patients and ≤30,000 visits over five years. That sizing explicitly rules out distributed architecture. |
| C-47 | Compatibility: Chrome, Edge, Safari (L191–192) | Ambiguous | Ready | None | No minimum versions, and **print rendering differs materially between Safari and Chrome** — which matters because the printed prescription is the product's main physical output. Firefox is excluded without comment. |
| C-48 | *Absent:* audit trail, data retention, patient consent | Missing detail | Needs decision | **Mutable history** | Nothing records what was changed and when. For prescriptions this is the difference between "the record says X" and "the record can be shown to have always said X." Retention period and any right-to-erasure stance are also absent, and they conflict by nature with medical-record retention obligations. |
| C-49 | "Open Questions: None (all major product decisions defined)" (L196–198) | **Contradiction** | **Blocker** (process) | — | This review found 10 build Blockers and 17 decisions the owner still owes the team. The line is not merely inaccurate; it actively discourages the pre-build conversation that would surface them. Replace with the §12 list. |

> **Note:** IDs are renumbered relative to the previous version of this file; when comparing across versions, cite by BRD line number, not by C-ID.

---

## 5. Challenging the BRD

Each item states what the BRD says, why it may not hold, and what I would put in its place. These are findings for the owner to accept or reject — not substitutions I have made.

### 5.1 Challenging the BRD: the 2–3 minute consultation target
**BRD says (L75):** "Doctor can complete a consultation record within 2–3 minutes."
**Why it may not hold:** the target measures the wrong thing. A visit with three vitals, a two-line complaint, a diagnosis and three medications is roughly 250–400 characters of typing; at a clinician's realistic keyboard speed that alone approaches or exceeds two minutes, before the software does anything at all. A team can hit 2–3 minutes and still ship a slow app, or miss it while shipping a fast one, because the number is dominated by typing, not by the product.
**Instead:** split it. **(a) System overhead ≤ 30 s** — the total of navigation, form transitions, saves, and prescription render for one visit, measured by instrumentation; this is the number the team controls and can regress-test. **(b) End-to-end ≤ 3 min** for a defined "typical visit" fixture (3 vitals, ≤200-char complaint, ≤100-char diagnosis, 2 medications), used as an acceptance test, not a build target.

### 5.2 Challenging the BRD: "Mandatory" vitals
**BRD says (L110–114):** temperature, BP and pulse are mandatory for every consultation.
**Why it may not hold:** mandatory fields with no legitimate escape are not obeyed — they are defeated. A cuff fails, a two-year-old will not sit still, a patient refuses, a 90-second repeat-prescription visit does not warrant a full vitals set. The doctor's options under a hard block are to abandon the software for that visit (paper returns, the BRD's core goal fails) or to type `0`, `120/80` from memory, or `999`. The second is worse: it puts fabricated clinical values into a permanent record that looks exactly like a real one, forever.
**Instead:** **mandatory-or-reason.** Vitals are required to finalize a visit, but each may be marked "not recorded" with a one-tap reason from a short doctor-defined list (equipment unavailable / patient declined / not clinically indicated / other). The value is stored as genuinely absent — never as a sentinel number — and the printed prescription shows "not recorded" rather than a blank that reads as an omission. This preserves the BRD's intent (no accidental skipping) while removing the incentive to fabricate.

### 5.3 Challenging the BRD: "No data loss" as an absolute
**BRD says (L181):** "No data loss."
**Why it may not hold:** stated absolutely, it is unachievable and untestable — a power cut mid-keystroke loses *something*, always. An untestable reliability requirement is one nobody can fail, which means nobody builds for it.
**Instead:** state a **recovery point objective** the team can build and QA can verify: *no more than 5 seconds of typed consultation content is lost in a browser crash, tab close, or power cut; no committed (finalized) record is ever lost; and restore from backup loses at most 24 hours.* Add the two behaviours that make it real: continuous draft autosave (REC-1) and a **visible backup-status indicator** so a silent failure cannot masquerade as success (REC-8).

### 5.4 Challenging the BRD: "Open Questions: None"
**BRD says (L198):** "None (all major product decisions defined for Phase 1)."
**Why it may not hold:** §4 lists 10 Blockers and 17 owner decisions. More importantly, the line is a *process* claim, and it is the one that does damage: it tells the development team there is nothing to ask about, so the questions surface mid-sprint as rework instead of pre-build as decisions.
**Instead:** replace with the §12 table. A BRD with 16 honest open questions is more build-ready than one asserting zero.

### 5.5 Challenging the BRD: "80% reduction in paper usage"
**BRD says (L77).**
**Why it may not hold:** no baseline paper count exists anywhere, so the percentage has no denominator; and the product's flagship deliverable is a **printed prescription**, so a busy clinic could digitise perfectly and print *more* paper than before.
**Instead:** measure the thing the product actually changes: **% of consultations with a complete digital record** (target ≥95% within 30 days of go-live) and **% of patient lookups performed without touching a paper file** (≥90%). Both are countable inside the app; neither is gamed by the prescription printer.

### 5.6 Challenging the BRD: "Offline functionality — out of scope"
**BRD says (L65)**, alongside "No data loss" (L181) and browser-based access (L42).
**Why it may not hold:** these three coexist only under a deployment model the BRD never names (C-6). If the app is hosted off-site, an ISP outage stops the clinic entirely and any in-progress typing is at risk — which is precisely the "lost or incomplete records" problem the Problem Statement exists to solve.
**Instead:** I am **not** proposing offline sync (correctly out of scope, and expensive). I am proposing the owner make the deployment call explicitly, and if it is off-site, accept two consequences in writing: (a) network down = clinic on paper for the duration, and (b) an in-page "connection lost — your typing is held locally" state so the doctor is never misled into thinking a save succeeded. That is not offline mode; it is honest failure signalling.

### 5.7 Challenging the BRD: "Single user" is used to justify having no audit trail
**BRD implies it (L14, L185); C-48 shows the consequence.**
**Why it may not hold:** single-user answers *who*, and nothing else. It does not answer *what was changed, when, and from what value* — which is exactly the question asked years later about a prescription, and the one asked by anyone auditing the record. One user makes the trail cheaper to build, not less necessary.
**Instead:** a minimal append-only trail on the events that matter — visit finalized, visit amended, prescription printed/reprinted, patient demographics edited, patient deactivated, export generated. Not a general-purpose audit framework; roughly six event types.

---

## 6. Diverge — how the consultation is captured and committed

Eight genuinely distinct approaches to the document's root gap (C-27, C-42). Each is judged on the same axes in §7.

**A — Stepped wizard.** Four screens: Vitals → Complaints → Diagnosis → Medications → Print. Each Next saves. The obvious reading of the BRD's own ordering.

**B — Single-page consultation.** One scrollable page with all four sections visible at once and a single Save. Minimal build, minimal navigation.

**C — Autosaved draft + explicit finalize at print.** One page (as B), continuously autosaved as a **draft**; "Finalize & Print" is the single commit point that makes the visit a permanent record. Finalized visits are immutable; corrections are appended as dated amendments. Draft visits are visibly marked everywhere they appear.

**D — Remove the step: consultation-first, appointment implied.** Drop scheduling from the critical path for walk-ins. The doctor searches a patient and clicks "Start consultation"; the system creates the appointment record itself, already marked Completed on finalize. Scheduling remains available for booked patients but is never a prerequisite. *This removes a step rather than adding a feature.*

**E — Keyboard-first command surface.** No new fields; a different input model. `/` focuses search, `Alt+1..4` jumps between consultation sections, `Ctrl+Enter` finalizes and prints, Tab order fixed and tested. Mouse becomes optional throughout a live consultation.

**F — Challenge-the-BRD: vitals as mandatory-or-reason.** The §5.2 change, treated as a build option: the vitals gate stays but gains a structured "not recorded + reason" path, so the flow can never dead-end.

**G — Paper-parity freeform.** One large text area per visit; structured fields optional. Fastest possible entry and the smallest build — and it destroys the structured history, filtering and export value that justifies the product. Included as the honest low-end anchor.

**H — Prefill from last visit.** For a returning patient, "Copy from last visit" pre-loads previous medications and complaint text for editing. Large time saving on repeat/chronic patients; carries a real clinical-safety-shaped risk of a stale line being carried forward unnoticed.

### 6.1 Sketch — consultation lifecycle (option C), at sketch altitude

```
   [ none ]
      | doctor clicks "Start consultation" (from appointment OR walk-in, option D)
      v
  ( DRAFT ) --- autosave every few seconds; visible as "Draft" in history
      |  \
      |   \-- abandoned (no finalize) --> stays DRAFT, listed but flagged; never
      |                                    counted as a completed visit
      | "Finalize & Print"  (vitals gate: value OR not-recorded+reason, option F)
      v
 ( FINALIZED ) -- immutable; prescription may be reprinted (each reprint logged)
      |
      | doctor edits later
      v
 ( FINALIZED + AMENDMENT ) -- original preserved, amendment appended with its
                              own timestamp and note. Nothing is overwritten.
```

### 6.2 Sketch — entities and relationships only (no types, no DDL)

```
ClinicProfile (clinic_name, address, doctor_name, registration_no, signature_image,
               prescription_footer)          <-- C-32: currently has no home in the BRD

Patient (name, dob?, approx_age?, age_recorded_on?, gender, phone?, alt_contact?,
         registered_on, status:active|inactive, merged_into?)
   |
   |--< Appointment (scheduled_for, status, source: booked|walk-in, visit?)
   |
   |--< Visit (state: draft|finalized, started_at, finalized_at, appointment?)
            |--  Vitals (temperature, temperature_unit, bp_systolic, bp_diastolic,
            |            pulse, each with not_recorded_reason?)
            |--  Complaint (text)
            |--  Diagnosis (text)
            |--< Medication (name, dosage, frequency, duration, instructions)
            |--< Prescription (issued_at, printed_count)
            |--< Amendment (text, created_at)
```

Two relationships carry most of the integrity weight: **Appointment ↔ Visit** (optional in both directions — a cancelled appointment has no visit, a walk-in visit has no booked appointment) and **Patient → Visit** (a patient with visits must never be hard-deleted; `status` and `merged_into` exist so identity can be corrected without destroying history).

*If the team needs field types, constraints, indexes or a migration plan, that is implementation design and I should hand off rather than produce it here.*

---

## 7. Converge

| Opt | What it is | Why it helps this doctor | Effort | Effect on 2–3 min | Integrity effect | Key risk |
|---|---|---|---|---|---|---|
| **A** Wizard | 4 sequential screens | Enforces BRD order; hard to skip a section | M | **Worse** — 3 extra transitions, and back-navigation to fix a typo is costly | Neutral; partial completion still undefined | Rigid: real consultations jump around (meds discussed before diagnosis is typed) |
| **B** Single page | Everything on one page, one Save | Fewest transitions; whole visit visible at once | S | **Good** — near-zero system overhead | **Bad** — one Save means one interruption loses the lot | Directly reproduces the BRD's silent-loss gap |
| **C** Autosaved draft + finalize | B, plus draft state and an explicit commit at print | Doctor never thinks about saving; "printed" and "permanent" become the same well-understood moment | M | **Neutral to good** — no extra clicks; finalize is the print click the doctor already makes | **Best** — closes Silent loss and Mutable history; gives Orphan a parent | Needs a clear draft/finalized visual distinction or history becomes confusing |
| **D** Consultation-first | Walk-in starts a consultation directly; appointment auto-created | Matches how a small GP clinic actually runs; removes a whole screen from the common path | S–M | **Best** — removes a step | **Good** — every visit gets an appointment parent automatically, no orphans | Daily-list semantics must cover auto-created rows; scope-adjacent to C-24, needs the owner's call |
| **E** Keyboard-first | Shortcuts and fixed tab order | Hands stay on keys with a patient in the room | S | **Good** — seconds per section | None | Shortcut collisions with browser/Safari; needs a discoverable hint row |
| **F** Vitals mandatory-or-reason | Structured escape from the vitals gate | Flow never dead-ends; no fabricated values enter history | S | **Neutral** — one tap only in the exception case | **Good** — prevents sentinel values corrupting clinical history | Formally a BRD change (§5.2); owner must accept |
| **G** Freeform | One text area per visit | Fastest entry, smallest build | S | **Best** raw speed | **Bad** — no structure to filter, export or trust later | Guts history/export value; contradicts the Problem Statement |
| **H** Prefill from last visit | Copy previous meds/complaint forward | Large saving on chronic and repeat patients | M | **Best** on repeat visits | **Watch** — copied content can be mistaken for freshly entered content | Stale medication carried forward unnoticed. Mitigation is UI-level (copied lines visually marked and requiring explicit confirmation) — **the clinical rule about what may be repeated is the doctor's to define, never the system's suggestion** |

**Surviving set: C + D + F, with E as a low-cost multiplier.** B is C without the safety. A costs time the BRD does not have. G is rejected on principle: it solves the speed criterion by discarding the reason the product exists. **H is Phase 1-eligible but sequenced last** — it is the only option whose failure mode is a wrong medication on a real prescription, so it should not ship in the same increment as the lifecycle work.

---

## 8. Nine-category edge-case sweep

Rated with the §3 rubric, ranked within each category by likelihood × impact. **Data-integrity cases are marked `[DI]`** — these are the "corrupts data or loses a record" set, consolidated in §14.

### 8.1 Empty / zero / first-run

| ID | Scenario | Lik. | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| E-1 | **First launch: no clinic header configured**, doctor prints a prescription `[DI]` | High | Major | Block first print until ClinicProfile (C-32) is completed; first-run setup screen before any consultation is possible | 1 |
| E-2 | No patients yet — search and recent-patients are both empty | High | Minor | Empty state that offers "Register first patient" as the only action, not a blank panel | 1 |
| E-3 | No appointments today | High | Minor | Empty state offering "Start walk-in consultation" (option D) | 1 |
| E-4 | Patient with zero prior visits opens History | High | Minor | "No previous visits" + direct start-consultation action; never an empty grid | 1 |
| E-5 | Consultation finalized with **no medications** (advice-only visit) | High | Major | Explicitly allowed; prescription prints with "No medication prescribed" so a blank section is never read as a printing failure | 1 |
| E-6 | Export triggered with nothing in range | Med | Minor | Warn before generating; do not emit a zero-row file that looks like data loss | 1 |
| E-7 | Search returns no match | High | Minor | "No patient found" + inline "Register [typed text] as new patient" — the moment a typo turns into a duplicate if unhandled | 1 |
| E-8 | Patient registered with **every** optional field blank (name only) | Med | Major | Allowed if name present; flag profile as incomplete so it is visible rather than silently thin | 1 |

### 8.2 Boundary values & extremes

| ID | Scenario | Lik. | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| E-9 | **Age stored as a number, read years later** `[DI]` | High | Critical | Never store bare age. DOB, or `approx_age` + `age_recorded_on` (C-19). A record that silently ages is corrupted history | 1 |
| E-10 | **Prescription overflows one page** (10+ medications, long instructions) `[DI]` | Med | Major | Paginate with patient name, date and "Page n of m" repeated on every page; never silently truncate — a truncated prescription is a clinical document with missing content | 1 |
| E-11 | Newborn: age in days/weeks; DOB = today | Med | Major | Accept DOB = today; display age in days < 1 month, months < 2 years. Reject DOB in the future | 1 |
| E-12 | Vitals at implausible extremes (temp 45 °C, pulse 300, BP 400/0) | Med | Major | **Soft** warning with confirm, never a hard block. Thresholds are configuration the **doctor defines**; the system only enforces what it is given | 1 |
| E-13 | Single-name patient (no surname) | High | Major | One free-text name field. A required-surname design rejects real patients (C-18) | 1 |
| E-14 | 300-character name, or 10,000-character pasted complaint | Med | Minor | Soft cap + counter, hard cap, graceful truncation in list views with full text on the record | 1 |
| E-15 | Patient aged 100+, or DOB implying age > 120 | Low | Minor | Warn, allow; typo-catching only | 1 |
| E-16 | 500 rows in the daily list / thousands of visits in history | Low | Minor | Paginate history at ~50; daily list is bounded by reality (C-46) | 1 |
| E-17 | Dosage typed as `50` instead of `5` | Med | Critical | Out-of-band for the software to judge. Options are (a) nothing, (b) a confirm step on finalize showing the medication list in large type for visual check. **(b) recommended — it is a legibility aid, not clinical advice** | 1 |

### 8.3 Missing / partial / optional data

| ID | Scenario | Lik. | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| E-18 | **BP genuinely cannot be taken** but vitals are mandatory `[DI]` | High | Critical | Mandatory-or-reason (§5.2). Absent must be stored as absent — a sentinel `0/0` is permanent fabricated clinical data | 1 |
| E-19 | **Prescription printed before diagnosis entered** `[DI]` | Med | Major | Owner decides (C-30) whether diagnosis blocks finalize. Whatever is chosen, the printed sheet must not show an ambiguous blank | 1 |
| E-20 | Patient has no phone number | High | Major | Allow; phone optional but strongly prompted, since it is a search key (C-21/C-22). Show "no contact recorded" explicitly on the profile | 1 |
| E-21 | DOB unknown, only "about 40" | High | Major | `approx_age` + `age_recorded_on`, displayed as "~40 (recorded 2026)" so it never masquerades as exact | 1 |
| E-22 | Medication with dosage but no duration | Med | Major | Owner decides required subset. Default: Name + Dosage required; Frequency/Duration/Instructions optional and printed only when present | 1 |
| E-23 | Gender unknown or not stated | Med | Minor | Include an explicit "Not stated" option; do not force a guess (C-20) | 1 |
| E-24 | Temperature recorded but unit ambiguous (37 vs 98.6) | Med | Major | Unit is part of the stored value and is fixed per clinic in ClinicProfile; display and print always show the unit | 1 |

### 8.4 Duplicates & identity

| ID | Scenario | Lik. | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| E-25 | **Same patient registered twice; history splits** `[DI]` | High | Critical | Duplicate warning at registration on (name similarity + phone) or (name + DOB) before the record is created. Prevention is Phase 1; **merge tooling is Phase 2** (§11) | 1 (detect) |
| E-26 | **Merging two records — what happens to two visit histories?** `[DI]` | Med | Critical | Not solved in Phase 1. Interim: mark one record inactive with a `merged_into` pointer so both histories remain readable and neither is deleted. **Never destructive** | 1 (pointer) / later (merge) |
| E-27 | One phone number for a whole family | High | Major | Phone is a household identifier, not a person identifier. Search on phone must return **all** matches as a list, never auto-select the first | 1 |
| E-28 | Two patients, same name and same age | Med | Critical | Disambiguation is mandatory in every picker: show phone tail + DOB/age + last visit date in results. **Never show name alone in a selection list** — this is the wrong-patient-record path | 1 |
| E-29 | Two appointments for the same patient on the same day | Med | Minor | Allowed (morning and evening happen); warn on creation of the second | 1 |
| E-30 | Returning patient re-registered because search failed to find them (typo in stored name) | High | Major | Mitigated by E-7 and E-25; fuzzy search on name is the real fix (C-22) | 1 |

### 8.5 State transitions & lifecycle

| ID | Scenario | Lik. | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| E-31 | **Consultation started but never finished** `[DI]` | High | Critical | Draft state (option C), visibly flagged in history and the daily list, never silently counted as a completed visit and never silently discarded | 1 |
| E-32 | **Editing a consultation after the prescription is printed** `[DI]` | High | Critical | Finalized visits immutable; corrections appended as dated amendments; the original text is never overwritten. The patient holds a paper copy — the stored record must still match what was handed over | 1 |
| E-33 | **Deleting a patient who has visits** `[DI]` | Med | Critical | No hard delete. Deactivate only; visits preserved and reachable. Hard delete is a retention/erasure question (C-48), not a UI button | 1 |
| E-34 | **Completed → Scheduled (undo) detaching a real consultation** `[DI]` | Med | Major | Disallow. Once a visit is finalized against an appointment, the appointment stays Completed; mistakes are handled by amendment, not reversal | 1 |
| E-35 | No-show → Completed (patient turns up 40 minutes late) | High | Major | **Must be allowed** — this is normal clinic life, not an edge case. Transition permitted and logged | 1 |
| E-36 | Cancelled → Completed | Med | Major | Allow with confirm (patient came anyway), or require a fresh walk-in visit. Owner's call; either way the previous status must remain visible | 1 |
| E-37 | Yesterday's appointments still say Scheduled at midnight | High | Minor | Do **not** auto-mark No-show — that writes clinical-adjacent facts nobody asserted. Show them as "Past — needs status" and prompt at end of day | 1 |
| E-38 | Appointment backdated or forward-dated | Med | Minor | Allow both (retrospective entry is real); warn beyond a sensible window | 1 |
| E-39 | Vitals entered, visit closed without medications, then reopened the same day | Med | Major | Reopening a draft is normal; reopening a **finalized** visit produces an amendment (E-32), not an edit | 1 |

### 8.6 Concurrency, timing & sessions

| ID | Scenario | Lik. | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| E-40 | **Two browser tabs on the same consultation; last write wins silently** `[DI]` | Med | Critical | Single-user does not mean single-tab. Detect a second tab on the same visit and make it read-only with an explicit banner. Silent last-write-wins is unacceptable for clinical content | 1 |
| E-41 | **Session expires mid-consultation with unsaved typing** `[DI]` | High | Critical | Never discard on expiry: re-authenticate in place and keep the draft. Idle timeout must be long enough for a real consultation (C-44) | 1 |
| E-42 | **Accidental tab close / back button / refresh during entry** `[DI]` | High | Critical | Autosave (REC-1) plus a browser beforeunload warning. This is the highest-frequency loss path in the product (C-37) | 1 |
| E-43 | **Double-click on Save/Finalize creates two visits or two prescriptions** `[DI]` | High | Major | Disable on submit + idempotent commit. Cheap to build, ugly and confusing if missed | 1 |
| E-44 | Clock crosses midnight mid-consultation — which date owns the visit? | Low | Major | Visit date = `started_at`, fixed at draft creation and never recomputed at finalize. State it explicitly, or two reports will disagree | 1 |
| E-45 | Daylight-saving shift moves a scheduled appointment time | Low | Minor | Store instants; render in clinic-local time. Single clinic, so exposure is small | 1 |
| E-46 | Two tabs registering the same new patient simultaneously | Low | Major | Covered by the E-25 duplicate check plus idempotent create | 1 |

### 8.7 Failure & recovery

| ID | Scenario | Lik. | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| E-47 | **Network or server error on save; doctor believes it saved** `[DI]` | High | Critical | Explicit save-state indicator ("Saved 10:42" / "Not saved — retrying"). Never show a success state for an unconfirmed write | 1 |
| E-48 | **Backup fails silently for weeks** `[DI]` | Med | Critical | Visible last-successful-backup status in the UI, and a loud warning past a threshold. An unmonitored backup is not a backup (C-43) | 1 |
| E-49 | **Power cut mid-consultation** `[DI]` | Med | Critical | Draft autosave bounds the loss to the RPO in §5.3; on relaunch, offer to resume the draft rather than starting fresh | 1 |
| E-50 | **Restore from backup — what was lost, and does anyone know?** `[DI]` | Low | Critical | Define RPO, and **test the restore before go-live**. An untested restore is an assumption, not a control | 1 |
| E-51 | Server error *after* the doctor clicked Print | Med | Major | Finalize commits before rendering; print is a downstream, retryable step against an already-permanent record | 1 |
| E-52 | Print dialog cancelled — is the prescription "issued"? | High | Major | Yes: the visit is finalized at commit; print/reprint count is tracked separately. Reprint must be available and logged | 1 |
| E-53 | PDF generation fails | Med | Minor | Error with retry; never leave a half-written file or an ambiguous success | 1 |
| E-54 | Disk/storage full | Low | Critical | Fail loudly on write; never degrade into silent partial saves | 1 |

### 8.8 Input validation, encoding & misuse

| ID | Scenario | Lik. | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| E-55 | **Complaint containing a comma or newline splits the CSV row** `[DI]` | High | Major | Correct RFC-4180 quoting and escaping. This corrupts the export silently — the file opens fine and the columns are wrong | 1 |
| E-56 | **CSV formula injection** (`=`, `+`, `-`, `@` at field start executes in Excel) `[DI]` | Med | Critical | Prefix-escape on export. Low likelihood via a doctor's own typing, **Critical because the file carries PHI and executes on someone else's machine** | 1 |
| E-57 | Non-Latin / mixed-script names; emoji or smart quotes pasted into complaints | High | Major | Unicode end to end: storage, search, print, and CSV with a BOM so Excel does not mangle it | 1 |
| E-58 | Free text rendered into printed HTML (`<`, `&`, script-like content) | Med | Major | Escape on output. Injection matters less with one user; **a garbled prescription is a clinical document defect** regardless | 1 |
| E-59 | Phone with country code, spaces, dashes, or letters | High | Major | Store as entered, index a normalised digits-only form for search; do not reject formats the doctor uses | 1 |
| E-60 | Leading/trailing whitespace producing near-duplicate names | High | Major | Trim and collapse internal whitespace on save — a cheap and large contributor to E-25 | 1 |

### 8.9 Privacy, access & audit

| ID | Scenario | Lik. | Impact | Proposed handling | Phase |
|---|---|---|---|---|---|
| E-61 | **Exported CSV/PDF sitting unencrypted in Downloads** | High | Critical | Cannot be prevented by the app once the file exists. Warn at export, log every export (C-48), and default export scope to the narrowest useful selection rather than the whole database | 1 |
| E-62 | **Screen left unlocked between patients, full history on display** | High | Critical | Short idle screen-lock that blurs PHI but preserves the in-progress draft (E-41). Also governs the recent-patients list (C-36) | 1 |
| E-63 | **No record of what was prescribed, when, or what changed** `[DI]` | High | Critical | Minimal append-only trail on ~6 event types (§5.7). This is the record that answers questions asked years later | 1 |
| E-64 | Prescription printed to a shared or networked printer | Med | Major | Out of the app's control; name it as an operational risk and note it in go-live guidance | accepted |
| E-65 | Browser autofill / cached form data on the clinic machine | Med | Major | Disable autocomplete on patient fields; do not persist PHI in local storage beyond the active draft | 1 |
| E-66 | Right to erasure vs. required medical-record retention | Low | Major | Genuine legal conflict; the owner must state a retention period and an erasure stance (C-48). Deactivate-not-delete (E-33) keeps the option open | later |

**Consciously not handling in Phase 1:** see the parking-lot table in §11. That table is the complete list — nothing deferred appears anywhere else in this document, and its rows are not restated as a separate closing list.

---

## 9. Risk register

Ordered by **build-readiness first**, then likelihood × impact. Every row is a Blocker or a Critical-impact item; the rest live in §4 and §8.

| ID | Risk | Source | Lik. | Impact | Integrity | Build-ready | Mitigation |
|---|---|---|---|---|---|---|---|
| RSK-1 | No consultation lifecycle or commit model — "no data loss" is unbuildable and untestable | C-27, C-42 | High | Critical | Silent loss, Orphan | Blocker | REC-1 |
| RSK-2 | No patient identity rule — histories split across duplicate records, decisions made on half a history | C-23 | High | Critical | Duplicate | Blocker | REC-2 |
| RSK-3 | Mandatory vitals with no escape drives paper workarounds or fabricated values into permanent records | C-28 | High | Critical | Silent loss | Blocker | REC-3 |
| RSK-4 | Prescription header has no source entity; the product's main output cannot be produced | C-32 | High | Critical | — | Blocker | REC-4 |
| RSK-5 | Deployment model unstated — encryption, backups, sessions and outage behaviour all undecidable | C-6, C-9, C-45 | High | Critical | Silent loss | Blocker | REC-5 |
| RSK-6 | Appointment state machine undefined; illegal transitions can detach real consultations | C-24, C-26 | High | Major | Mutable history | Blocker | REC-6 |
| RSK-7 | Patient edit/delete undefined — history can be orphaned or silently rewritten | C-17 | Med | Critical | Orphan, Mutable history | Blocker | REC-7 |
| RSK-8 | Backups unmonitored, restore never tested | C-43 | Med | Critical | Silent loss | Needs decision | REC-8 |
| RSK-9 | No audit trail on prescriptions, amendments or exports | C-48 | High | Critical | Mutable history | Needs decision | REC-9 |
| RSK-10 | Export carries unencrypted PHI out of the app, with injection and quoting defects | C-7, C-38, E-56, E-61 | Med | Critical | — | Needs decision | REC-10 |
| RSK-11 | Session expiry / tab close destroys in-progress consultation content | C-44, E-41, E-42 | High | Critical | Silent loss | Needs decision | REC-1, REC-11 |
| RSK-12 | Wrong-patient selection from a name-only picker | E-28 | Med | Critical | — | Ready | REC-12 |
| RSK-13 | Contact optional while phone is a primary search key | C-21, C-22 | High | Major | Duplicate | Needs decision | REC-2, REC-13 |
| RSK-14 | 2–3 minute target unmeasurable; team optimises the wrong thing or declares success arbitrarily | C-11 | High | Major | — | Needs decision | REC-14 |
| RSK-15 | "Open Questions: None" suppresses the pre-build decision conversation | C-49 | High | Major | — | Blocker (process) | REC-15 |

---

## 10. Recommendations — prioritized by build-readiness

### Tier 1 — must close before build starts (all Blockers)

| ID | Recommendation | Closes | Effort | Trade-off, stated honestly |
|---|---|---|---|---|
| **REC-1** | **Define the Visit entity and its lifecycle: autosaved draft → finalize at print → immutable, corrections as dated amendments.** Adopt options C + F. | RSK-1, RSK-11 | L | Amendments make history longer and slightly harder to read than simple edits. A doctor who wants to "just fix a typo" will find append-only mildly annoying. That friction is the price of a record that can be trusted years later — and I would pay it. |
| **REC-2** | **Set a patient identity rule and a duplicate check at registration** (name similarity + phone, or name + DOB), with `merged_into` pointers instead of deletion. | RSK-2, RSK-13 | M | Fuzzy matching produces false positives, so a warn-don't-block design will occasionally slow registration by one click. Blocking would be worse. Full merge is deferred (§11) — the accepted risk is that duplicates accumulate until the tooling exists. |
| **REC-3** | **Change mandatory vitals to mandatory-or-reason.** (§5.2 — this is a BRD change and needs the owner's explicit acceptance.) | RSK-3 | S | Weakens the BRD's stated guarantee that every consultation has vitals. In exchange the data that *is* there is real. A guarantee that produces fabricated values is worth less than an honest gap. |
| **REC-4** | **Add a ClinicProfile / settings entity and a first-run setup gate** before any prescription can print. | RSK-4 | S | Adds a screen the BRD never mentioned — a small scope addition, and unavoidable: without it the flagship output cannot be produced. |
| **REC-5** | **Make the deployment model an explicit written decision** (clinic PC / LAN / cloud) and derive encryption, backup destination, session policy and outage behaviour from it. | RSK-5 | S (decision) / M (consequences) | This is a one-meeting decision that unblocks four other items. Deferring it means building three security requirements on a guess. |
| **REC-6** | **Draw the appointment state machine, including Appointment ↔ Visit linkage, walk-ins (option D) and the No-show → Completed transition.** | RSK-6 | M | Option D adds auto-created appointment rows the daily list must handle. It also removes a whole screen from the most common path, which is a net win against the time target. |
| **REC-7** | **Define patient edit and deactivate semantics: no hard delete while visits exist; demographic edits recorded, not silently overwritten.** | RSK-7 | M | The doctor loses a "delete" button they may expect. Deactivate plus a visible reason covers every legitimate use except legal erasure, which is deferred. |
| **REC-15** | **Replace "Open Questions: None" with the §12 table.** | RSK-15 | S | Costs the BRD its appearance of completeness. That appearance is the risk. |

### Tier 2 — needs an owner decision, then build

| ID | Recommendation | Closes | Effort |
|---|---|---|---|
| **REC-8** | Visible last-successful-backup status, alert on failure, and a **rehearsed restore before go-live**. | RSK-8 | M |
| **REC-9** | Minimal append-only audit trail on ~6 events (finalize, amend, print/reprint, demographic edit, deactivate, export). | RSK-9 | M |
| **REC-10** | Export hardening: RFC-4180 quoting, formula-injection escaping, UTF-8 BOM, narrowest-scope default, export warning, export logged. | RSK-10 | M |
| **REC-11** | Session policy sized for a real consultation: in-place re-auth that preserves the draft; idle **screen lock** separated from session **expiry**. | RSK-11 | M |
| **REC-13** | Decide whether phone is required at registration; if optional, make the search consequence visible on the profile. | RSK-13 | S |
| **REC-14** | Split the 2–3 minute target into system overhead (≤30 s, instrumented) and end-to-end against a defined typical-visit fixture. (§5.1 — BRD change.) | RSK-14 | S |

### Tier 3 — Ready now, cheap, disproportionately valuable

| ID | Recommendation | Closes | Effort |
|---|---|---|---|
| **REC-12** | Never show name alone in any patient picker — always name + phone tail + age/DOB + last visit date. | RSK-12 | S |
| **REC-16** | Keyboard-first input (option E): `/` to search, section jumps, `Ctrl+Enter` to finalize, tested tab order. | C-40, RSK-14 | S |
| **REC-17** | Save-state indicator ("Saved 10:42" / "Not saved — retrying") plus beforeunload guard and double-submit protection. | E-42, E-43, E-47 | S |
| **REC-18** | Trim and normalise whitespace and phone formats on save. | E-59, E-60 | S |
| **REC-19** | Name the concrete numbers the BRD leaves vague: p95 latency at a stated data volume, and a scalability ceiling (C-46) so nobody over-engineers. | C-12, C-41, C-46 | S |

**Sequencing note:** REC-5 first (it unblocks four others in one meeting), then REC-1 and REC-4 (nothing ships without them), then REC-2, REC-3, REC-6, REC-7. Tier 3 can run in parallel throughout — REC-12 and REC-17 are each under a day and each remove a Critical-impact failure.

**Top pick: REC-1.** It is the only recommendation that converts the BRD's most important promise ("No data loss") from a slogan into something QA can fail a build over.
**Its top unresolved edge case: E-31** — the abandoned draft. If the doctor starts consultations for three patients and finalizes two, the third draft must be visible and clearly *not* a completed visit, forever. Get that display wrong and the safety mechanism becomes a source of ambiguous records — which is the exact problem it was built to prevent.

---

## 11. Parking lot — Phase 2+ (the single home for everything deferred)

**This table is this review's complete "Consciously not handling in Phase 1" statement.** Nothing deferred is listed anywhere else in this document, and these rows are deliberately not restated as a separate closing list — one item, one home.

| Item | Why it's deferred | Pull-forward condition | Accepted risk while deferred |
|---|---|---|---|
| **Duplicate merge tooling** (combining two patients' visit histories) | Genuinely hard: merge semantics, undo, and audit. Detection (REC-2) captures most of the value at a fraction of the cost | More than ~5 confirmed duplicate pairs in the first 3 months | Duplicates accumulate. Mitigated by `merged_into` pointers so no history is destroyed and merge stays possible later |
| **Receptionist / multi-user access** | BRD out of scope (L60) | Doctor hires front-desk staff | Doctor performs all data entry, competing with the 2–3 minute target during busy periods |
| **Follow-up alerts / reminders** | BRD out of scope (L69) | Doctor reports missed follow-ups after go-live | The prescription's own Duration field creates an expectation the product will not meet |
| **Billing / invoicing, insurance, lab & pharmacy integration** | BRD out of scope (L61–63) | A separate business case | Billing stays on paper or a separate tool; no single view of a patient encounter |
| **AI-based diagnosis or recommendations** | BRD out of scope (L64). Also outside this review's remit — no clinical advice | Not in the foreseeable roadmap | None. Correct exclusion |
| **Offline functionality** | BRD out of scope (L65) | Deployment is off-site **and** the clinic experiences repeated outages | Under a cloud deployment, a network outage stops the clinic (§5.6). Requires the owner's explicit written acceptance |
| **Mobile app** | BRD out of scope (L66) | Doctor does home visits or ward rounds | Consultation entry is tied to the clinic machine |
| **Advanced analytics / reporting** | BRD out of scope (L67) | Doctor asks a question CSV export cannot answer | CSV export is the only analysis path; ad-hoc questions need manual spreadsheet work |
| **Multi-doctor / multi-clinic** | BRD out of scope (L68) | A second clinician joins | Data model should avoid actively blocking it, but must not be built for it (C-46) |
| **Medicine master list / autocomplete** | Needs a curated data source and maintenance; free text ships now | Medication history search proves unreliable in practice, or the doctor asks for it | Spelling variants fragment medication history (C-31). Real but tolerable at one-clinic scale |
| **Prefill from last visit (option H)** | Buildable in Phase 1 but its failure mode is a wrong medication on a real prescription; should not ship alongside the lifecycle work | Lifecycle (REC-1) is shipped and stable, and repeat patients are a measured majority | Repeat consultations stay slower than they need to be |
| **Structured complaints / coded diagnosis (ICD-style)** | Free text is faster in a live consultation and matches the BRD | A reporting or referral requirement appears | Complaints and diagnoses are not aggregable or reliably searchable |
| **Right-to-erasure workflow** | Conflicts with medical-record retention; needs a legal answer first (C-48, E-66) | A patient makes a formal request, or a retention policy is set | No defined response to an erasure request. Deactivate-not-delete (E-33) keeps the option open |
| **Leap-year DOB edge (29 Feb age display)** | Theoretical; affects display only, never the stored fact | Never, realistically | A displayed age may be off by a day for a tiny cohort |
| **Timezone / DST handling beyond clinic-local** | Single clinic, single locale (E-45) | Clinic operates across timezones — i.e. never, under Phase 1 scope | Appointment times shift by an hour twice a year in edge displays |
| **Shared-printer exposure** (E-64) | Not solvable in software | Clinic moves to a shared-office printer | A printed prescription may be collected by the wrong person. Operational guidance only |
| **Firefox support** | Not listed in C-47 | Doctor's machine uses it | Unsupported, untested browser; print rendering unverified |

---

## 12. Open questions for the product owner

Priority = severity **plus** cost to resolve. A Critical gap that closes with one meeting decision outranks a Major one that costs two weeks of design.

| ID | Question | Severity | Cost to resolve | Blocks |
|---|---|---|---|---|
| **Q-1** | Where does this run — clinic PC, LAN server, or cloud? | Critical | **Policy call** | REC-5, and the whole of security, backup and outage design |
| **Q-2** | May a consultation be finalized without complete vitals, using a recorded reason? | Critical | **Policy call** | REC-3 |
| **Q-3** | Are finalized visits immutable with append-only amendments, or freely editable? | Critical | **Policy call** | REC-1 |
| **Q-4** | What are the clinic header, footer and signature contents, and who supplies the signature image? | Critical | **Policy call** | REC-4 |
| **Q-5** | Can a consultation exist without an appointment (walk-ins)? | Critical | **Policy call** | REC-6 |
| **Q-6** | May a patient record ever be hard-deleted, and what is the retention period? | Critical | **Policy call** (with legal input) | REC-7, E-66 |
| **Q-7** | Is phone number required at registration? | Major | **Policy call** | REC-13, REC-2 |
| **Q-8** | Is diagnosis required before a prescription may print? | Major | **Policy call** | C-30, E-19 |
| **Q-9** | Which gender values are offered? | Minor | **Policy call** | C-20 |
| **Q-10** | Which vitals units (°C/°F), and does the doctor want plausibility warnings — and at what thresholds? | Major | **Policy call** (thresholds are the doctor's to define) | E-12, E-24 |
| **Q-11** | What exactly is exportable — one visit, one patient, a date range, everything? | Major | **Policy call** | REC-10 |
| **Q-12** | What is the acceptable data-loss window (RPO), and who is told when a backup fails? | Critical | **Policy call**, then **design + build** | REC-8, §5.3 |
| **Q-13** | What is the identity rule that makes two patients the same person — and who resolves a flagged duplicate? | Critical | **Design + build** | REC-2 |
| **Q-14** | Which appointment status transitions are legal, and what happens to yesterday's Scheduled rows? | Major | **Policy call**, then **design + build** | REC-6, E-35, E-37 |
| **Q-15** | How is the 2–3 minute target measured — clock start, clock stop, and against what content? | Major | **Policy call** | REC-14 |
| **Q-16** | Is DOB, approximate age, or either acceptable at registration? | Major | **Policy call** | C-19, E-21 |

**Thirteen of sixteen are one-meeting policy calls, and six of those are Critical.** The BRD is far closer to build-ready than the Blocker count alone suggests — most of the distance is decisions, not engineering. Only Q-13 requires real design work before it can be answered at all.

---

## 13. Cross-reference index

The same gap appears three times by design — once as a question for the owner, once as a change for the team, once as a risk on the register. This index makes the overlap explicit so nobody counts one decision as three.

| Open question | Recommendation | Risk | Coverage rows | Key edge cases |
|---|---|---|---|---|
| Q-1 deployment model | REC-5 | RSK-5 | C-6, C-9, C-45 | E-47, E-49 |
| Q-2 vitals escape | REC-3 | RSK-3 | C-28 | E-18, E-24 |
| Q-3 immutability / amendments | REC-1 | RSK-1 | C-27, C-42 | E-31, E-32, E-39 |
| Q-4 clinic header | REC-4 | RSK-4 | C-32 | E-1, E-10 |
| Q-5 walk-ins | REC-6 (option D) | RSK-6 | C-24, C-27 | E-3, E-29 |
| Q-6 delete / retention | REC-7 | RSK-7 | C-17, C-48 | E-33, E-66 |
| Q-7 phone required | REC-13, REC-2 | RSK-13 | C-21, C-22 | E-20, E-27 |
| Q-8 diagnosis required | — (owner call, then build) | — | C-30 | E-19 |
| Q-9 gender values | — (owner call) | — | C-20 | E-23 |
| Q-10 units & plausibility | — (owner call, then build) | — | C-28 | E-12, E-24 |
| Q-11 export scope | REC-10 | RSK-10 | C-7, C-38, C-39 | E-6, E-55, E-56, E-61 |
| Q-12 RPO & backup alerting | REC-8 | RSK-8 | C-42, C-43 | E-48, E-49, E-50 |
| Q-13 patient identity | REC-2, REC-12 | RSK-2, RSK-12 | C-18, C-23 | E-7, E-25, E-26, E-28, E-30, E-60 |
| Q-14 status transitions | REC-6 | RSK-6 | C-26 | E-34, E-35, E-36, E-37 |
| Q-15 time-target measurement | REC-14, REC-16 | RSK-14 | C-11, C-40 | — |
| Q-16 DOB vs age | — (owner call) | — | C-19 | E-9, E-11, E-21 |
| *(no owner question — build hygiene)* | REC-11, REC-17, REC-18, REC-19 | RSK-11 | C-12, C-37, C-41, C-44, C-46 | E-40, E-41, E-42, E-43, E-57, E-59, E-62 |
| *(process)* | REC-15 | RSK-15 | C-49 | — |

---

## 14. Data integrity — consolidated

**Data integrity:** the four failure modes, and where this review leaves each.

- **Duplicate** — *Real and high-likelihood today.* No identity rule (C-23), a shared household phone (E-27), and whitespace-variant names (E-60) all create split histories. REC-2 + REC-12 + REC-18 prevent most; merge tooling is knowingly deferred (§11) with the accepted risk that duplicates accumulate — never that they are destroyed.
- **Orphan** — *Structural.* The visit entity does not exist in the BRD (C-27), so vitals, complaints, diagnosis and medications have no defined parent, and "Completed" points at nothing verifiable. REC-1 and REC-6 create the parent; REC-7 stops patients being deleted out from under their own visits.
- **Mutable history** — *Real and unaddressed.* A finalized prescription can silently change after the patient has walked out with paper in hand (C-17, C-26, C-32, C-48), with no trail. REC-1 (immutability + amendments) and REC-9 (audit trail) close it. Storing bare age (C-19) is the quiet member of this class: a fact that corrupts itself with time.
- **Silent loss** — *The document's largest single exposure.* No save model, no RPO, no crash recovery, no backup monitoring (C-6, C-9, C-42, C-43), plus session expiry and tab close as everyday triggers (E-41, E-42). REC-1, REC-8, REC-11 and REC-17 together turn "No data loss" into a property a test can fail.

**The 22 `[DI]`-marked cases are the "corrupts data or loses a record" set:** E-1, E-9, E-10, E-18, E-19, E-25, E-26, E-31, E-32, E-33, E-34, E-40, E-41, E-42, E-43, E-47, E-48, E-49, E-50, E-55, E-56, E-63. Everything else in §8 is recoverable or cosmetic. **If Phase 1 must be cut, cut from the cosmetic set — never from this one.**

---

## 15. Handoff

This document is brainstorming and analysis. It deliberately stops at entity names and relationships (§6.2) and does not specify field types, constraints, indexes, validation implementations or migrations — those are decisions for implementation design, made by the team that will own them.

**Suggested next step:** run a one-hour decision session against §12. The thirteen policy calls close six Critical items in a single meeting, after which the remaining work is design and build rather than blocked guesswork. Once Q-1 through Q-6 are answered, the brainstorm is done and this should hand off to implementation planning.
