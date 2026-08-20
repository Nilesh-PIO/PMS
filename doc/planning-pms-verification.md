# Patient Management Application — Phase 1 Implementation Plan

- **Source of truth (what to build):** `BRD/Doc_BRD.md`
- **Source of truth (readiness, converged decisions, risks, open questions):** `doc/brainstorm-pms-verification.md`
- **Date:** 2026-08-18
- **Scope:** Phase 1 only (single general physician, single clinic)
- **Status:** Implementation plan. Concrete enough to open an editor and create files. Not authorisation to build the items marked `Blocked`.

---

## 1. Headline

**Ready to build today:** the solution skeleton, app shell, patient search, search-first registration with near-match warning, patient archive lifecycle, the appointment state machine, the consultation draft/autosave lifecycle, finalize with appointment auto-completion, prescription snapshot + print, and visit history. That is roughly two-thirds of Phase 1 and it is enough work to keep a developer busy for six weeks without touching an undecided item.

**Gated behind a decision:** authentication session policy (OQ-11), audit scope (OQ-12), clinic header content (OQ-5), patient demographics rules (OQ-7, OQ-14), appointment scheduling model (OQ-9), the vitals exception path (OQ-1 — the brainstorm converged on D-7 C, it needs ratification only), medication/diagnosis required-field rules (OQ-13), amendment policy (OQ-2), export scope (OQ-10), and the recovery objective behind backups (OQ-3). Each is planned below behind a single labelled `Assumption:` line.

**Genuinely blocked, no concrete steps written:** **F-4 credential recovery** (OQ-6 — no converged option exists anywhere; a design decision, not a default) and **F-22 retention & deletion policy** (OQ-8 — jurisdiction-specific, the brainstorm explicitly refuses to invent one). Both are `L`.

**Single highest-leverage next step:** hold the one-hour owner meeting that answers OQ-1 through OQ-8. Per §12 of the brainstorm doc that hour clears six of the eight Blockers, converts eleven feature sections below from `Needs decision` to `Ready`, and is the only thing that can unblock F-4 and F-22 at all. Do this before F-13 starts, because F-13 is the critical path's longest link.

**Critical path** (longest chain of blocking dependencies from the §5 map — this, not the sum of all efforts, sets the earliest finish):

```
F-1 Solution skeleton (M)
  -> F-3 Auth & session (M)
    -> F-7 Patient entity & registration (M)
      -> F-13 Consultation draft lifecycle + autosave (L)
        -> F-15 Complaints/diagnosis/medications + pre-finalize review (M)
          -> F-16 Finalize + appointment auto-complete (M)
            -> F-17 Prescription snapshot, print layout, reprint (L)
              -> F-18 Amendments (M)
```

Eight links, two of them `L`. Everything else in Phase 1 (search, archive, appointments, history, export, backup) hangs off this spine and can be built in parallel by a second developer without extending the finish date. **F-13 is where schedule risk actually lives**; it is also R-1, the brainstorm's top recommendation.

---

## 2. Architecture overview (stated once; every feature section references it)

| Concern | Convention |
|---|---|
| Backend runtime | .NET 10 (LTS), ASP.NET Core Web API |
| Projects | `PMS.Api` (controllers, middleware, composition root) · `PMS.Application` (services, DTOs, validators, interfaces) · `PMS.Domain` (EF entities, enums, domain rules) · `PMS.Infrastructure` (`PmsDbContext`, EF Core configurations, migrations, repositories, PDF/CSV/backup adapters) |
| Dependency direction | `Api -> Application -> Domain`; `Infrastructure -> Application/Domain`. `Api` references `Infrastructure` **only** in `Program.cs` for DI registration |
| API shape | RESTful controllers, one per aggregate (`PatientsController`, `AppointmentsController`, `VisitsController`, `PrescriptionsController`, `ClinicProfileController`, `ExportController`, `AuthController`, `SettingsController`, `BackupStatusController`). Controllers depend on services, **never** on `PmsDbContext` |
| DTOs | Request/response DTOs in `PMS.Application/Dtos/<Aggregate>/`. EF entities are never serialised across the wire. Mapping is hand-written static mappers (`PatientMapper`) — no AutoMapper, the surface is too small to justify it |
| Data access | EF Core 10, Code-First. Entities in `PMS.Domain/Entities`, `IEntityTypeConfiguration<T>` classes in `PMS.Infrastructure/Persistence/Configurations`, migrations in `PMS.Infrastructure/Persistence/Migrations`. **Every schema change is a named migration** — each feature below names its own |
| Keys | `Guid` primary keys generated in-app via `Guid.CreateVersion7()` (sequential, index-friendly, opaque in URLs — see EC-71). No natural keys anywhere |
| Time | All instants stored as `DateTimeOffset` (UTC). A single clinic timezone is stored in `ClinicProfile.TimeZoneId` and is the **only** timezone used for rendering. `Visit.ClinicDate` is a `DateOnly`, fixed at draft creation and never recomputed (R-22, EC-47, EC-48) |
| Validation | FluentValidation in `PMS.Application/Validation`, executed by a filter in `PMS.Api`. Failures return RFC 7807 `ProblemDetails` with `errors` |
| Error handling | One `ExceptionHandlingMiddleware` in `PMS.Api/Middleware`; all errors return `ProblemDetails`. No raw exception text reaches the browser |
| Frontend | Angular 20 workspace, **standalone components throughout — no `NgModule`s** (stated once; every feature is consistent with it). Signals for component state, `HttpClient` with a typed service per feature, `provideRouter` with lazy `loadComponent` routes |
| Frontend layout | `frontend/src/app/features/<feature>/` each containing `*.component.ts`, `*.service.ts`, `models/*.model.ts` (TypeScript interfaces mirroring the API DTOs). Shared pieces in `frontend/src/app/shared/`, cross-cutting singletons in `frontend/src/app/core/` |
| Auth | **Decision (new — the BRD and brainstorm doc do not fix the mechanism; ratify in §9):** cookie-based auth via ASP.NET Core Identity with an `HttpOnly`, `Secure`, `SameSite=Strict` session cookie. **Not JWT** — a JWT in browser storage is readable by any script and survives in a shared-PC browser profile, which directly contradicts EC-68/EC-70/EC-71. Cookie auth also gives server-side session revocation, which app-level auto-lock needs |
| Config & secrets | `appsettings.json` for non-secrets; connection string and Identity keys via **user-secrets** locally and **environment variables** in the clinic deployment. Nothing secret is committed. Any feature touching configuration says so explicitly |
| Encryption | In transit: HTTPS enforced (`UseHsts`, `RequireHttpsMetadata`), `Encrypt=True;TrustServerCertificate=False` on the SQL connection string. At rest: SQL Server TDE enabled on the `PMS` database plus BitLocker on the host volume — this is a deployment step, tracked in §8 |
| Deviations | None planned. If a feature needs one, it says so in its own section |

---

## 3. Solution & repo structure (as it will look at the end of Phase 1)

```
Hospital-managment/
├── BRD/Doc_BRD.md
├── doc/
│   ├── brainstorm-pms-verification.md
│   └── planning-pms-verification.md          <- this file
├── backend/
│   ├── PMS.sln
│   ├── src/
│   │   ├── PMS.Api/
│   │   │   ├── Controllers/
│   │   │   │   ├── AuthController.cs
│   │   │   │   ├── ClinicProfileController.cs
│   │   │   │   ├── PatientsController.cs
│   │   │   │   ├── AppointmentsController.cs
│   │   │   │   ├── VisitsController.cs
│   │   │   │   ├── PrescriptionsController.cs
│   │   │   │   ├── ExportController.cs
│   │   │   │   ├── SettingsController.cs
│   │   │   │   └── BackupStatusController.cs
│   │   │   ├── Middleware/
│   │   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   │   └── AuditEnrichmentMiddleware.cs
│   │   │   ├── Filters/ValidationFilter.cs
│   │   │   ├── appsettings.json
│   │   │   └── Program.cs
│   │   ├── PMS.Application/
│   │   │   ├── Abstractions/       (IPatientRepository, IVisitRepository, IClock, IAuditWriter, IPdfRenderer, ICsvWriter, ...)
│   │   │   ├── Dtos/<Aggregate>/
│   │   │   ├── Services/           (PatientService, AppointmentService, VisitService, PrescriptionService, ExportService, ClinicProfileService, AuditService)
│   │   │   ├── Mapping/
│   │   │   └── Validation/
│   │   ├── PMS.Domain/
│   │   │   ├── Entities/           (Patient, Appointment, Visit, Vitals, MedicationLine, PrescriptionIssue, VisitAmendment, ClinicProfile, AuditEvent, VitalRangeSetting, NotRecordedReason, BackupStatus)
│   │   │   └── Enums/              (PatientRecordStatus, AppointmentStatus, VisitLifecycleState, PrescriptionIssueKind, AuditAction)
│   │   └── PMS.Infrastructure/
│   │       ├── Persistence/
│   │       │   ├── PmsDbContext.cs
│   │       │   ├── Configurations/
│   │       │   └── Migrations/
│   │       ├── Repositories/
│   │       ├── Printing/QuestPdfRenderer.cs
│   │       ├── Export/CsvWriter.cs
│   │       └── Backup/BackupStatusProbe.cs
│   └── tests/
│       ├── PMS.Application.Tests/Services/
│       ├── PMS.Api.IntegrationTests/Controllers/
│       └── PMS.Infrastructure.Tests/
└── frontend/
    ├── angular.json  package.json  playwright.config.ts
    ├── e2e/
    └── src/app/
        ├── core/            (auth.interceptor.ts, auth.guard.ts, clinic-setup.guard.ts, error.interceptor.ts, idle-lock.service.ts, clinic-clock.service.ts)
        ├── shared/          (patient-picker/, empty-state/, save-indicator/, confirm-dialog/, date-range-filter/)
        └── features/
            ├── auth/  clinic-setup/  patients/  appointments/  consultation/  prescription/  history/  export/  settings/  home/
```

---

## 4. Data model overview (Phase 1, EF types)

One level more concrete than the brainstorm's §7.1 sketch. Column-level constraint tuning and index shape are migration-review tasks, not planning ones — only load-bearing constraints are stated.

| Entity | Key properties (EF types) | Relationships |
|---|---|---|
| `Patient` | `Id: Guid` · `DisplayName: string(200)` · `NormalizedName: string(200)` (accent/case-folded, for search) · `DateOfBirth: DateOnly?` · `AgeAtRegistrationYears: int?` · `AgeAtRegistrationMonths: int?` · `AgeCapturedOn: DateOnly?` · `Gender: string(40)` · `PhonePrimary: string(30)?` · `PhonePrimaryDigits: string(20)?` · `PhoneAlt: string(30)?` · `PhoneAltDigits: string(20)?` · `Notes: string(2000)?` · `RegisteredOn: DateTimeOffset` · `ContactUpdatedOn: DateTimeOffset?` · `RecordStatus: PatientRecordStatus` · `ArchivedIntoPatientId: Guid?` · `ArchivedOn: DateTimeOffset?` · `ArchiveNote: string(500)?` | `1—* Visit`, `1—* Appointment`, self-ref `ArchivedIntoPatientId -> Patient` |
| `Appointment` | `Id: Guid` · `PatientId: Guid` · `ScheduledFor: DateTimeOffset` · `ClinicDate: DateOnly` · `Status: AppointmentStatus` · `StatusChangedOn: DateTimeOffset` · `ReasonNote: string(300)?` · `CreatedOn: DateTimeOffset` | `*—1 Patient`, `0..1—0..1 Visit` |
| `Visit` | `Id: Guid` · `PatientId: Guid` · `AppointmentId: Guid?` · `ClinicDate: DateOnly` (fixed at draft creation) · `StartedAt: DateTimeOffset` · `FinalizedAt: DateTimeOffset?` · `LifecycleState: VisitLifecycleState` (Draft/Finalized) · `ComplaintsText: string(4000)?` · `DiagnosisText: string(4000)?` · `IsBackdated: bool` · `CreatedOn: DateTimeOffset` · `RowVersion: byte[]` (concurrency token) · `EditingSessionId: Guid?` + `EditingHeartbeatAt: DateTimeOffset?` (two-tab guard) | `*—1 Patient`, `0..1—0..1 Appointment`, `1—1 Vitals`, `1—* MedicationLine`, `1—* PrescriptionIssue`, `1—* VisitAmendment` |
| `Vitals` | `VisitId: Guid` (PK+FK) · `TemperatureValue: decimal(4,1)?` · `TemperatureUnit: string(1)?` (C/F) · `TemperatureNotRecordedReasonId: Guid?` · `BpSystolic: int?` · `BpDiastolic: int?` · `BpNotRecordedReasonId: Guid?` · `PulseValue: int?` · `PulseNotRecordedReasonId: Guid?` | `1—1 Visit`, `*—0..1 NotRecordedReason` (x3) |
| `MedicationLine` | `Id: Guid` · `VisitId: Guid` · `Sequence: int` · `DrugName: string(200)` · `Dosage: string(100)?` · `Frequency: string(100)?` · `Duration: string(100)?` · `Instructions: string(500)?` | `*—1 Visit`; unique `(VisitId, Sequence)` |
| `PrescriptionIssue` | `Id: Guid` · `VisitId: Guid` · `GeneratedAt: DateTimeOffset` · `IssueKind: PrescriptionIssueKind` (Original/Reprint/AmendedReissue) · `SnapshotJson: string(max)` · `SnapshotHash: string(64)` · `RenderedPdf: byte[]?` | `*—1 Visit`; **append-only, never updated or deleted** |
| `VisitAmendment` | `Id: Guid` · `VisitId: Guid` · `AmendedAt: DateTimeOffset` · `FieldChanged: string(100)` · `PriorValue: string(max)?` · `NewValue: string(max)?` · `Reason: string(500)` | `*—1 Visit`; **append-only** |
| `ClinicProfile` | `Id: Guid` · `ClinicName: string(200)` · `DoctorName: string(200)` · `Qualifications: string(300)?` · `RegistrationNumber: string(100)?` · `AddressLines: string(500)?` · `Phone: string(50)?` · `FooterNote: string(1000)?` · `LogoBytes: byte[]?` · `LogoContentType: string(100)?` · `TimeZoneId: string(100)` · `IsSetupComplete: bool` | Singleton row enforced by a check constraint / seeded fixed `Id` |
| `AuditEvent` | `Id: Guid` · `OccurredAt: DateTimeOffset` · `EntityKind: string(60)` · `EntityId: Guid?` · `Action: AuditAction` · `Detail: string(1000)?` · `CorrelationId: string(50)?` | Standalone, **append-only, insert-only mapping** |
| `NotRecordedReason` | `Id: Guid` · `VitalKind: string(20)` (Temperature/BloodPressure/Pulse/Any) · `Label: string(100)` · `IsActive: bool` · `Sequence: int` | Doctor-defined lookup (D-7 C) |
| `VitalRangeSetting` | `Id: Guid` · `VitalKind: string(20)` · `Unit: string(10)?` · `WarnBelow: decimal?` · `WarnAbove: decimal?` | Doctor-configured; **blank until the doctor sets it** — the system never asserts a clinical range of its own (EC-13) |
| `BackupStatus` | `Id: Guid` · `LastSuccessAt: DateTimeOffset?` · `LastAttemptAt: DateTimeOffset?` · `LastResult: string(40)` · `LastMessage: string(500)?` | Singleton row |
| Identity tables | `AspNetUsers`, `AspNetUserClaims`, … (ASP.NET Core Identity, single row in `AspNetUsers`) | — |

Relationship summary: `Patient 1—* Visit` · `Patient 1—* Appointment` · `Visit 1—1 Vitals` · `Visit 1—* MedicationLine` · `Visit 1—* PrescriptionIssue` · `Visit 1—* VisitAmendment` · `Appointment 0..1—0..1 Visit`.

**Integrity-motivated entities** (none appear in the BRD; all come from brainstorm §7.1): `PrescriptionIssue.SnapshotJson` closes Mutable history on the printed artefact · `VisitAmendment` closes Mutable history on the visit · `Patient.RecordStatus` + `ArchivedIntoPatientId` close the Orphan hole a hard delete would open · `ClinicProfile` is the missing entity behind Blocker C-22 · `AuditEvent` answers "who changed what, when".

---

## 5. Dependency map (build order)

Effort rubric matches brainstorm §3: **S** under a day · **M** two to five days · **L** over a week, *or* anything `Blocked` or resting on an unresolved `Needs decision`.

**Effort notation:** where the brainstorm doc already converged on a named option and the open question is a *ratification* rather than an open design space, the effort is written `M (L while OQ-n open)` — the build cost is M, but the true cost including the decision cycle is L until the owner confirms. Where no converged option exists (F-4, F-11, F-22), the effort is flatly `L`, because the design does not exist yet.

| ID | Feature | Depends on | Effort | Readiness |
|---|---|---|---|---|
| F-1 | Solution skeleton, config, error handling, clinic clock | — | M | Ready |
| F-2 | App shell, navigation, empty states, keyboard-first | F-1 | M | Ready |
| F-3 | Authentication & session (login, auto-lock) | F-1 | M (L while OQ-11 open) | Needs decision (OQ-11) |
| F-5 | Append-only audit log | F-1, F-3 | M (L while OQ-12 open) | Needs decision (OQ-12) |
| F-6 | ClinicProfile + first-run setup gate | F-1, F-3 | S (L while OQ-5 open) | Needs decision (OQ-5) |
| F-7 | Patient entity + registration form | F-1, F-3, F-5 | M (L while OQ-7/OQ-14 open) | Needs decision (OQ-7, OQ-14) |
| F-8 | Patient search + recent patients | F-7 | M (L while OQ-16 open) | Needs decision (OQ-16); search itself Ready |
| F-9 | Search-first registration + near-match warning | F-7, F-8 | M | Ready (D-2 converged) |
| F-10 | Patient archive lifecycle (no hard delete) | F-7, F-5 | S | Ready (D-2/R-4 converged) |
| F-11 | Appointment scheduling + daily list | F-7, F-8 | **L** | Needs decision (OQ-9 — no converged option) |
| F-12 | Appointment state machine + Overdue display | F-11, F-5 | M | Ready (brainstorm §7.2 converged) |
| F-13 | **Consultation draft lifecycle + autosave + concurrency guards** | F-7, F-6, F-5 | **L** | Ready (D-1 D converged; OQ-3 sets one constant) |
| F-14 | Vitals capture + not-recorded reasons + doctor ranges | F-13 | M (L while OQ-1 open) | Needs decision (OQ-1 — D-7 C converged) |
| F-15 | Complaints, diagnosis, medications + pre-finalize review | F-13 | M (L while OQ-13 open) | Needs decision (OQ-13) |
| F-16 | Finalize + appointment auto-complete + idempotency | F-13, F-14, F-15, F-12 | M | Ready (D-5 C converged) |
| F-17 | Prescription snapshot, print layout, reprint | F-16, F-6 | **L** | Ready (D-4 C converged) |
| F-18 | Amendments after finalize | F-16, F-17, F-5 | M (L while OQ-2 open) | Needs decision (OQ-2 — D-1 D converged) |
| F-19 | Patient history + visit detail + date filter | F-13, F-16, F-17, F-18 | M | Ready |
| F-20 | Export CSV/PDF (scoped, confirmed, audited) | F-8, F-19, F-5 | M (L while OQ-10 open) | Needs decision (OQ-10 — D-6 converged) |
| F-21 | Backup + visible backup status | F-1, F-2 | M (L while OQ-3 open) | Needs decision (OQ-3) |
| F-4 | Credential recovery for the single user | F-3, **OQ-6** | **L** | **Blocked** |
| F-22 | Retention & deletion policy enforcement | F-10, **OQ-8** | **L** | **Blocked** |

**Blocking reach:** F-4 blocks go-live but blocks no other feature's *code* — it is a release gate, not a build gate (RISK-8: a forgotten password locks the clinic out of every record permanently). F-22 blocks nothing downstream either; archive-not-delete (F-10) is the stated interim, so the schema does not change when OQ-8 lands — only a policy job is added. **Neither blocker sits on the critical path**, which is why the critical path in §1 is buildable today.

---

## 6. Feature plans

---

### F-1 — Solution skeleton, configuration, error handling, clinic clock

**1. Readiness — Ready.** No BRD ambiguity; conventions are fixed in §2.

**2. Data model.** No entities yet. Creates `PmsDbContext` in `PMS.Infrastructure/Persistence/`, EF design-time factory, and the `IClock` / `ClinicClock` abstraction (`UtcNow: DateTimeOffset`, `ClinicToday(): DateOnly`, `ToClinicTime(DateTimeOffset)`) implementing R-22. Migration: **none in this feature** — the first migration ships with F-3. Solution created with `dotnet new sln`, four `dotnet new classlib/webapi` projects, project references wired per §2.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/health` | — | `HealthResponse` | 200, 503 | Anonymous |
| GET | `/api/health/db` | — | `HealthResponse` | 200, 503 | Anonymous |

**4. Frontend design.** `ng new pms --standalone --routing --style=scss` into `frontend/`. Creates `frontend/src/app/core/error.interceptor.ts` (maps `ProblemDetails` to a toast), `frontend/src/app/core/api-base-url.token.ts`, `frontend/src/environments/environment*.ts` (API base URL only — no secrets ever reach the browser bundle), and `frontend/src/app/core/clinic-clock.service.ts` (`formatClinicDate(iso: string): string`, `formatClinicTime(iso: string): string`) so no component ever renders browser-local time (EC-48).

**5. Data integrity check.** No save path yet. Establishes the two mechanisms later features depend on: `IClock` (so `ClinicDate` never drifts across midnight — EC-47) and the `ProblemDetails` pipeline (so a failed write is never rendered as success — EC-51).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/ClinicClockTests.cs` — clinic-date rollover at the configured timezone boundary, DST shift (EC-48).
- Backend integration: `PMS.Api.IntegrationTests/HealthEndpointTests.cs` — `/api/health/db` returns 503 when the connection string is wrong.
- Frontend unit: `frontend/src/app/core/clinic-clock.service.spec.ts`.
- E2E: none (no user-facing flow yet).

**7. Acceptance criteria.**
- [ ] `dotnet build backend/PMS.sln` succeeds with zero warnings-as-errors from a clean clone.
- [ ] `dotnet ef migrations list` runs against `PMS.Infrastructure` without a design-time error.
- [ ] `GET /api/health/db` returns 200 with a reachable database and 503 with an unreachable one.
- [ ] No connection string, password or key appears in any file tracked by source control; `dotnet user-secrets list` shows the local connection string instead.
- [ ] `ng serve` renders the shell and an unhandled 500 from any endpoint surfaces a toast, never a raw stack trace.
- [ ] A date rendered by `ClinicClockService` is identical on a browser set to UTC and one set to UTC+9.

**8. Effort & dependencies.** **M.** Depends on nothing. **Blocks everything.**

---

### F-2 — App shell, navigation, empty states, keyboard-first

**1. Readiness — Ready.** Implements R-20 (purposeful empty states) and R-27/C-27 (keyboard-first, the actual lever on the 2–3 minute target per B-1).

**2. Data model.** None. No migration.

**3. API design.** None (consumes F-8 and F-11 endpoints once they exist; renders empty states until then).

**4. Frontend design.**
- `frontend/src/app/features/home/home.component.ts` — route `/today`. Panels: today's appointments (F-11), unfinished consultations (F-13), recent patients (F-8), backup status (F-21). Each panel renders `<pms-empty-state>` with its own primary action when its list is empty (EC-2, EC-4).
- `frontend/src/app/shared/empty-state/empty-state.component.ts` — inputs `message`, `actionLabel`, `actionRoute`.
- `frontend/src/app/core/shortcuts.service.ts` — `register(key: string, handler: () => void): void`. Global shortcuts: `/` focus search, `Alt+N` new patient, `Alt+C` new consultation, `Esc` close dialog.
- `frontend/src/app/app.routes.ts` — lazy `loadComponent` for every feature route; `/today` is the default redirect.
- Every form control in the app gets an explicit `tabindex` order and a visible focus ring; no action is mouse-only.

**5. Data integrity check.** No save path. Indirect contribution: EC-7's "Register [typed text] as a new patient" empty state is the entry point of search-first registration (F-9), which is the Duplicate prevention mechanism.

**6. Test strategy.**
- Backend unit / integration: not applicable.
- Frontend unit: `home.component.spec.ts` (each panel renders its empty state with the right action when given `[]`), `empty-state.component.spec.ts`, `shortcuts.service.spec.ts`.
- E2E: `frontend/e2e/shell-navigation.spec.ts` — first-run app with no data shows four purposeful empty states (EC-2, EC-4, EC-5); `keyboard-only.spec.ts` — reach and open a new consultation using only the keyboard.

**7. Acceptance criteria.**
- [ ] With an empty database, `/today` shows four panels, each with a message and a working primary action button — no blank boxes, no spinners left running (EC-2, EC-4).
- [ ] `/` focuses the patient search box from any route.
- [ ] Every route in `app.routes.ts` is reachable by keyboard alone from `/today`, verified by `keyboard-only.spec.ts`.
- [ ] Initial route render measured in Chrome DevTools (Fast 3G throttling off, local API) completes in < 2s per NFR Performance.

**8. Effort & dependencies.** **M.** Depends on F-1. Blocks nothing structurally, but every other feature's UI mounts inside it.

---

### F-3 — Authentication & session (login, auto-lock)

**1. Readiness — Needs decision (OQ-11).**

> **Assumption (OQ-11 — shared clinic PC vs. private device):** building for the **shared clinic PC**, the stricter of the two. That means app-level auto-lock after **10 minutes idle**, `autocomplete="off"` on patient fields and `autocomplete="new-password"` on the login form (EC-70), `Cache-Control: no-store` on all patient-data responses (EC-71), and **no session expiry while a consultation draft is dirty** (EC-43 — a timeout that eats a consultation gets switched off entirely, which is the worse security outcome). If the owner answers "private device", relax the idle timer to 60 minutes; nothing else changes.

**2. Data model.** ASP.NET Core Identity schema, single seeded user. No custom entity beyond Identity's. Migration: **`AddIdentitySchema`**.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| POST | `/api/auth/login` | `LoginRequest { UserName, Password }` | `SessionResponse { UserName, ExpiresAt, ClinicSetupComplete }` | 200, 400, 401, 423 (locked out) | Anonymous |
| POST | `/api/auth/logout` | — | — | 204 | Cookie |
| GET | `/api/auth/session` | — | `SessionResponse` | 200, 401 | Cookie |
| POST | `/api/auth/unlock` | `UnlockRequest { Password }` | `SessionResponse` | 200, 401, 423 | Cookie |
| POST | `/api/auth/change-password` | `ChangePasswordRequest { Current, New }` | — | 204, 400, 401 | Cookie |

Lockout: 5 failed attempts, 5-minute lockout (Identity defaults, configured explicitly in `Program.cs`). Backed by `AuthService` in `PMS.Application/Services/`.

**4. Frontend design.**
- `features/auth/login.component.ts` — route `/login`; calls `AuthService.login(req: LoginRequest): Observable<SessionResponse>`.
- `features/auth/lock-screen.component.ts` — route-less overlay; calls `AuthService.unlock(password: string)`. **Renders over the current route without unmounting it**, so a draft in the DOM survives the lock (EC-43, EC-68).
- `core/idle-lock.service.ts` — `start(): void`, `notifyActivity(): void`, `suppressWhile(isDirty: Signal<boolean>): void`. The consultation component registers its dirty signal here.
- `core/auth.guard.ts`, `core/auth.interceptor.ts` (adds `withCredentials`, redirects 401 to `/login`).
- `features/settings/change-password.component.ts` — route `/settings/security`.

**5. Data integrity check.** Risk: an idle lock or session expiry silently discarding an in-progress consultation (Silent loss, EC-43). Prevented by (a) auto-lock never unmounting the route, (b) the session not expiring while `isDirty` is true, and (c) F-13's autosave meaning the draft is already on the server regardless. Exposure risk (EC-68) is closed by the lock itself.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/AuthServiceTests.cs` — lockout after 5 failures, correct 423 on a locked account, password-change rejects a wrong current password.
- Backend integration: `PMS.Api.IntegrationTests/Controllers/AuthControllerTests.cs` — login sets an `HttpOnly` `Secure` `SameSite=Strict` cookie; a protected endpoint returns 401 without it; logout invalidates it; every patient-data response carries `Cache-Control: no-store` (EC-71).
- Frontend unit: `login.component.spec.ts`, `idle-lock.service.spec.ts` (timer suppressed while dirty — EC-43), `auth.interceptor.spec.ts`.
- E2E: `frontend/e2e/auth-lock.spec.ts` — golden path login; edge case **EC-68**: idle past the timer mid-consultation, unlock, confirm the typed complaint text is still on screen and still saved.

**7. Acceptance criteria.**
- [ ] Login with correct credentials reaches `/today`; with wrong credentials returns 401 and a message that does not reveal whether the username exists.
- [ ] The auth cookie is `HttpOnly`, `Secure`, `SameSite=Strict`; no token appears in `localStorage` or `sessionStorage` (checked in the E2E spec).
- [ ] After 10 minutes of no input, the lock overlay appears and every patient field is visually obscured (EC-68).
- [ ] Unlocking restores the exact route and form state that was on screen before the lock (EC-43).
- [ ] The session does **not** expire while a consultation draft is dirty, verified by an integration test advancing the clock past the timeout with a dirty draft.
- [ ] The login form does not offer to save credentials and patient inputs do not autofill on a second visit (EC-70).
- [ ] Six consecutive wrong passwords return 423 and the sixth attempt is recorded as an `AuditAction.LoginFailed` event once F-5 lands.

**8. Effort & dependencies.** **M (L while OQ-11 open).** Depends on F-1. Blocks F-4 (blocked), F-5, F-6, F-7 — i.e. the whole critical path.

---

### F-4 — Credential recovery for the single user

**1. Readiness — Blocked — needs decision first.**

**What must be resolved:** **OQ-6 — "How is the single user's password recovered if lost?"** (brainstorm §12; coverage row **C-31 Blocker**; **RISK-8**; **EC-74**). This is the one open question the brainstorm doc classifies as *design effort*, not a policy call, and it is the only Blocker for which **no converged option exists anywhere** in D-1..D-7. There are at least four materially different answers — a recovery email to a verified address, a sealed offline recovery code generated at setup, a documented DBA-level reset run in SSMS against the Identity tables, or a second break-glass local account — and they differ in threat model, deployment requirements (an SMTP dependency the clinic may not have) and operational cost. Picking one silently would be inventing a security decision, which §Rules forbids.

**Why no concrete steps:** the entity, the endpoint surface and the frontend route all differ per option (a recovery code needs a `RecoveryCode` entity and a setup-time reveal screen; an email path needs SMTP configuration, a token entity and a public reset route; a DBA reset needs no code at all, only a runbook). Writing file targets before the choice would be a guess.

**Where it must be resolved:** BRD *Non-Functional Requirements → Security* is silent on recovery; the decision belongs in `BRD/Doc_BRD.md` §Security plus the answer to **OQ-6** in `doc/brainstorm-pms-verification.md` §12.

**Interim risk while unresolved:** RISK-8 — Low likelihood, **Critical** impact, total and unrecoverable Silent loss. **This is a go-live gate, not a build gate**: F-3 and everything after it can be built and tested without it, but the clinic must not go live on real patient data until a recovery path exists.

**Effort & dependencies.** **L** (Blocked — the true cost includes the decision cycle plus a design pass). Depends on F-3 and on OQ-6. Blocks go-live only.

---

### F-5 — Append-only audit log

**1. Readiness — Needs decision (OQ-12).**

> **Assumption (OQ-12 — is an audit trail in Phase 1 scope, and which events?):** building **R-12's minimal append-only log** covering exactly: `Login`, `LoginFailed`, `VisitFinalized`, `VisitAmended`, `VisitDraftDiscarded`, `VisitPatientReassigned`, `PatientArchived`, `PatientDeleteBlocked`, `PrescriptionIssued`, `PrescriptionReprinted`, `DataExported`, `ClinicProfileUpdated`. Not full field-level change history — that is parking-lot **P-16**. If the owner declines audit entirely in Phase 1, F-18 (amendments) loses its trail and RISK-12 is accepted explicitly; say so in writing rather than dropping it quietly.

**2. Data model.** `AuditEvent` per §4. EF configuration maps it insert-only: no `Update`/`Delete` methods are exposed, and `PmsDbContext.SaveChanges` throws on a modified or deleted `AuditEvent` entry. Migration: **`AddAuditEvent`**.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/audit?from=&to=&entityKind=&action=&page=` | — | `PagedResult<AuditEventDto>` | 200, 401 | Cookie |

No POST — audit entries are written server-side by `IAuditWriter.WriteAsync(AuditAction, string entityKind, Guid? entityId, string? detail)` called from the services that perform the audited action, inside the same transaction as the action itself.

**4. Frontend design.** `features/settings/audit-log.component.ts` — route `/settings/audit`; read-only table with date-range and action filters. `AuditService.list(filter: AuditFilter): Observable<PagedResult<AuditEventDto>>`. No write path in the UI.

**5. Data integrity check.** This feature *is* the Mutable-history mitigation (RISK-12, EC-69): it answers "what was prescribed, when, and was it changed". Its own risk is a lost audit row when the audited action succeeds — prevented by writing the audit row in the **same `SaveChangesAsync` transaction** as the action, never in a fire-and-forget background call.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/AuditServiceTests.cs` — each `AuditAction` maps to the right `EntityKind`; detail text never contains free-text clinical content beyond an identifier.
- Backend integration: `PMS.Api.IntegrationTests/Controllers/AuditControllerTests.cs` — an `UPDATE`/`DELETE` attempt against `AuditEvent` via the context throws; a rolled-back finalize leaves **no** audit row (transactional coupling).
- Frontend unit: `audit-log.component.spec.ts` (filter wiring).
- E2E: `frontend/e2e/audit-trail.spec.ts` — finalize a visit, then confirm `/settings/audit` shows a `VisitFinalized` row for it (**EC-69**).

**7. Acceptance criteria.**
- [ ] Each of the twelve listed actions writes exactly one `AuditEvent` row with `OccurredAt`, `EntityKind`, `EntityId` and `Action` populated.
- [ ] Attempting to modify or delete an existing `AuditEvent` through the application throws and the transaction rolls back.
- [ ] An action that fails and rolls back leaves zero audit rows for that attempt.
- [ ] `/settings/audit` filtered to a single day returns only rows whose `OccurredAt` falls in that clinic-timezone day.
- [ ] Audit rows contain no complaint or diagnosis free text (identifiers only).

**8. Effort & dependencies.** **M (L while OQ-12 open).** Depends on F-1, F-3. Depended on by F-7, F-10, F-12, F-13, F-16, F-17, F-18, F-20.

---

### F-6 — ClinicProfile + first-run setup gate

**1. Readiness — Needs decision (OQ-5).** The Blocker C-22 was "no entity to hang the prescription header on"; brainstorm §7.1 and R-6 converge on adding `ClinicProfile`, so the **entity** is decided. What is not decided is its exact content.

> **Assumption (OQ-5 — what goes in the prescription header/footer):** building the §7.1 field set — clinic name, doctor name, qualifications, registration number, address, phone, footer note, logo — with **clinic name and doctor name mandatory** and everything else optional. The layout renders only populated fields. If the owner's answer adds a field (e.g. a scanned signature image), it is an additive migration plus one print-layout slot, not a redesign.

**2. Data model.** `ClinicProfile` per §4, singleton (fixed seed `Id`, check constraint ensuring a single row). Migration: **`AddClinicProfile`**. Logo stored as `varbinary(max)` with a 512 KB application-level cap.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/clinic-profile` | — | `ClinicProfileDto` | 200, 401 | Cookie |
| PUT | `/api/clinic-profile` | `UpdateClinicProfileRequest` | `ClinicProfileDto` | 200, 400, 401 | Cookie |
| PUT | `/api/clinic-profile/logo` | multipart file | `ClinicProfileDto` | 200, 400, 413, 401 | Cookie |
| DELETE | `/api/clinic-profile/logo` | — | 204 | 204, 401 | Cookie |
| GET | `/api/clinic-profile/setup-status` | — | `SetupStatusDto { IsSetupComplete, MissingFields[] }` | 200, 401 | Cookie |

**4. Frontend design.**
- `features/clinic-setup/clinic-setup.component.ts` — route `/setup`; `ClinicProfileService.get()`, `.update(req)`, `.uploadLogo(file)`, `.getSetupStatus()`.
- `core/clinic-setup.guard.ts` — redirects every route except `/setup`, `/login` and `/settings/security` to `/setup` while `IsSetupComplete === false` (**EC-1**).
- `features/settings/clinic-profile.component.ts` — route `/settings/clinic`, same form after first run.
- The print component (F-17) refuses to render and shows "Complete clinic setup first" while `IsSetupComplete` is false.

**5. Data integrity check.** No duplicate/orphan exposure (singleton row). The exposure it closes is a **deliverable** one (EC-1): a prescription printed with a blank header is not a usable clinical document, so printing is blocked until setup completes. Long clinic/doctor names truncate with an ellipsis **in the header only** — the stored value is never truncated (EC-11).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/ClinicProfileServiceTests.cs` — `IsSetupComplete` flips true only when clinic name and doctor name are both non-blank after trimming; oversized logo rejected.
- Backend integration: `PMS.Api.IntegrationTests/Controllers/ClinicProfileControllerTests.cs` — a second POST cannot create a second profile row; logo round-trips with content type.
- Frontend unit: `clinic-setup.component.spec.ts`, `clinic-setup.guard.spec.ts`.
- E2E: `frontend/e2e/first-run-setup.spec.ts` — **EC-1**: fresh database, log in, every navigation lands on `/setup`; complete it and printing becomes available.

**7. Acceptance criteria.**
- [ ] On a fresh database, any route other than `/setup`, `/login`, `/settings/security` redirects to `/setup` (EC-1).
- [ ] Saving with a blank clinic name or doctor name returns 400 and `IsSetupComplete` stays false.
- [ ] After a complete save, `/setup` is no longer forced and the prescription preview renders the header with the saved values.
- [ ] A 300-character clinic name is stored in full (queryable in SSMS) and renders truncated with an ellipsis in the printed header (EC-11).
- [ ] Uploading a 2 MB logo returns 413 with a `ProblemDetails` body.
- [ ] `TimeZoneId` set here is the timezone every date in the app renders in (F-1 clock).

**8. Effort & dependencies.** **S (L while OQ-5 open).** Depends on F-1, F-3. Depended on by F-13 (setup gate), F-17 (header render).

---

### F-7 — Patient entity + registration form

**1. Readiness — Needs decision (OQ-7, OQ-14).**

> **Assumption (OQ-7 — DOB vs. age):** building **D-3 option C** as converged — `DateOfBirth` optional, plus `AgeAtRegistrationYears`/`AgeAtRegistrationMonths` stored **with `AgeCapturedOn`**. Display derives age from DOB when present and from age + capture date otherwise, marked "approx." in the second case. Sub-year display supported ("3 months", "11 days") so a newborn never prints as "0" (EC-12).
>
> **Assumption (OQ-14 — gender value list):** building a **configurable lookup seeded with `Male`, `Female`, `Other`, `Unspecified`**, stored as a string on `Patient`, editable at `/settings/lookups`. The list is **not hardcoded to two values** (EC-24). If the owner's list differs, it is a seed-data change, not a schema change.

**2. Data model.** `Patient` per §4. Load-bearing constraints: **no unique constraint on phone** (EC-28 — families share a number; D-2 option B was rejected); `NormalizedName`, `PhonePrimaryDigits`, `PhoneAltDigits` are application-maintained derived columns written on every save (feeding F-8). `DisplayName` is **one field, not first/last** (EC-22). Migration: **`AddPatient`**.

Save-time normalisation (R-21): trim leading/trailing whitespace, collapse internal runs of whitespace, normalise smart quotes to straight, Unicode NFC-normalise, then derive `NormalizedName` (case-folded + diacritic-stripped) and `*Digits` (non-digits removed). Without this, `" Ramesh"` and `"Ramesh"` become two patients (EC-64 → EC-27).

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| POST | `/api/patients` | `CreatePatientRequest { DisplayName, DateOfBirth?, AgeYears?, AgeMonths?, Gender, PhonePrimary?, PhoneAlt?, Notes?, ConfirmedNotDuplicate }` | `PatientDto` | 201, 400, 409 (near-match unconfirmed — F-9), 401 | Cookie |
| GET | `/api/patients/{id}` | — | `PatientDto` | 200, 404, 401 | Cookie |
| PUT | `/api/patients/{id}` | `UpdatePatientRequest` | `PatientDto` | 200, 400, 404, 409, 401 | Cookie |
| GET | `/api/settings/gender-options` | — | `LookupOptionDto[]` | 200, 401 | Cookie |

Backed by `PatientService.CreateAsync`, `.GetAsync`, `.UpdateAsync` calling `IPatientRepository`.

**4. Frontend design.**
- `features/patients/patient-form.component.ts` — routes `/patients/new` and `/patients/:id/edit`. Reactive form; DOB and age fields are mutually informative (entering DOB disables the age fields and vice versa). `autocomplete="off"` on every control (EC-70).
- `features/patients/patient-detail.component.ts` — route `/patients/:id`; shows derived age with an "approx." marker, and `ContactUpdatedOn` beside the phone numbers (EC-26).
- `features/patients/patient.service.ts` — `create(req: CreatePatientRequest): Observable<PatientDto>`, `get(id: string)`, `update(id: string, req: UpdatePatientRequest)`, `genderOptions()`.
- `features/patients/models/patient.model.ts` — interfaces mirroring the DTOs.
- `shared/age-display/age-display.component.ts` — renders years / months / days per EC-12.

**5. Data integrity check.** Two exposures. **Mutable history** (C-10): a bare age silently rots — closed by storing `AgeCapturedOn` alongside it and always deriving display from the pair. **Duplicate** (EC-64): whitespace and smart-quote variants of the same name — closed by the normalisation step above, which also feeds F-9's near-match check. Patient edits are in-place (demographics are current facts, not clinical history); the prescription snapshot (F-17) is what protects past prescriptions from a later demographic edit.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/PatientServiceTests.cs` — normalisation (EC-64), digits extraction from `+91 98765 43210` (EC-63), future DOB rejected / today's DOB accepted (**EC-15**), age display for a 3-month-old (**EC-12**), age > 120 accepted with a warning flag (EC-17), single-name patient accepted (**EC-22**), patient with no phone accepted (**EC-20**).
- Backend integration: `PMS.Api.IntegrationTests/Controllers/PatientsControllerTests.cs` — POST then GET round-trip preserves a Devanagari name byte-for-byte (**EC-62**); two patients may share a phone number (**EC-28**).
- Frontend unit: `patient-form.component.spec.ts` (DOB/age mutual exclusion, validation messages), `age-display.component.spec.ts` (EC-12), `patient.service.spec.ts`.
- E2E: `frontend/e2e/patient-registration.spec.ts` — golden path register-and-view; edge case **EC-21** (patient does not know DOB → age path) and **EC-22** (single-name patient).

**7. Acceptance criteria.**
- [ ] A patient can be saved with a name and gender only — no phone, no DOB (EC-20, BRD *Patient Management*).
- [ ] A patient saved with age 3 months displays as "3 months (approx.)", never "0" (EC-12).
- [ ] A DOB in the future is rejected with 400; today's date is accepted (EC-15).
- [ ] `"  Ramesh  "` and `"Ramesh"` produce identical `DisplayName` and `NormalizedName` values in the database (EC-64).
- [ ] `"+91 98765 43210"` stores as entered and yields `PhonePrimaryDigits = "919876543210"` (EC-63).
- [ ] A name in Devanagari or Arabic script saves, reloads and renders unchanged (EC-62).
- [ ] The gender dropdown includes an "Unspecified" option and is driven by `/api/settings/gender-options`, not a hardcoded array (EC-24).
- [ ] The patient detail page shows the date contact details were last updated (EC-26).

**8. Effort & dependencies.** **M (L while OQ-7/OQ-14 open).** Depends on F-1, F-3, F-5. Depended on by F-8, F-9, F-10, F-11, F-13 — the widest fan-out in the plan.

---

### F-8 — Patient search + recent patients

**1. Readiness — Ready** for search; **Needs decision (OQ-16)** for the "recent patients" definition only.

> **Assumption (OQ-16 — recently viewed or recently consulted, and how many):** building **last 10 recently consulted** (brainstorm C-26 default), derived from `Visit.StartedAt` — not a viewed-history table. Switching to "recently viewed" later would require a new tracking table, so this is the assumption most worth confirming early.

Implements **R-17** and the B-6 budgets: **type-ahead < 300 ms**, full history load < 2 s. The 2–5 second figure from BRD *Success Criteria* is retired per C-9 — this is the tighter of two conflicting BRD numbers, adopted as the brainstorm converged.

**2. Data model.** No new entity. Migration: **`AddPatientSearchIndexes`** — non-clustered indexes on `Patient.NormalizedName`, `Patient.PhonePrimaryDigits`, `Patient.PhoneAltDigits`, and a filtered index on `RecordStatus = Active`.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/patients/search?query=&includeArchived=false&limit=20` | — | `PatientSearchResultDto[]` | 200, 400, 401 | Cookie |
| GET | `/api/patients/recent?limit=10` | — | `RecentPatientDto[]` | 200, 401 | Cookie |

`PatientSearchResultDto { Id, DisplayName, AgeDisplay, Gender, PhoneTail, LastVisitDate, RecordStatus }` — **the disambiguator fields are mandatory in the DTO**, because no picker anywhere may show a bare name (EC-29). Backed by `PatientService.SearchAsync(string query, bool includeArchived, int limit)`: if the query is digits-heavy it matches `PhonePrimaryDigits`/`PhoneAltDigits` by `Contains`; otherwise it matches `NormalizedName` by `Contains` on the accent-folded, case-folded form (C-4 default). Archived patients are excluded by default (F-10).

**4. Frontend design.**
- `features/patients/patient-search.component.ts` — route `/patients`; input debounced 200 ms with `switchMap` so in-flight requests are cancelled, keeping the perceived budget under 300 ms.
- `shared/patient-picker/patient-picker.component.ts` — reusable type-ahead used by the consultation and appointment screens; **always renders name + age + phone tail + last visit date** (EC-29) and **never auto-selects a single result** (EC-28).
- `patient.service.ts` gains `search(query: string, opts?): Observable<PatientSearchResultDto[]>` and `recent(): Observable<RecentPatientDto[]>`.
- Empty result renders `<pms-empty-state>` with "Register '<typed text>' as a new patient" — this is the search-first entry point for F-9 (**EC-7**).
- Routes carry the opaque `Guid`, never a name or phone (EC-71).

**5. Data integrity check.** Search *is* a duplicate-prevention mechanism (C-4, RISK-19): slow or coarse search pushes the doctor straight to "add new patient". Prevented by the 300 ms budget, digits-normalised phone matching (EC-63), diacritic-insensitive name matching (EC-62) and returning **all** phone matches rather than auto-selecting (EC-28).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/PatientSearchTests.cs` — `"ramesh"` matches `"Rameśh"` (EC-62); `"9876543210"` matches `"+91 98765 43210"` (EC-63); a family of three sharing one number returns three rows (**EC-28**); archived patients excluded by default.
- Backend integration: `PatientsControllerTests.SearchTests` — a seeded 5,000-patient set returns in < 300 ms server-side (asserted with a generous CI margin and recorded); empty query returns 400, not the whole table.
- Frontend unit: `patient-search.component.spec.ts` (debounce + cancellation), `patient-picker.component.spec.ts` (never auto-selects; always shows disambiguators — **EC-29**).
- E2E: `frontend/e2e/patient-search.spec.ts` — golden path find-by-partial-name; **EC-7** (no results → register CTA prefilled with the typed text); **EC-29** (two same-name same-age patients are distinguishable in the list).

**7. Acceptance criteria.**
- [ ] Typing three characters returns results in under 300 ms measured server-side on a 5,000-patient database (B-6).
- [ ] Every row in every patient list shows name, age, phone tail and last visit date — no bare-name rows anywhere in the app (EC-29).
- [ ] Searching a phone number shared by three family members returns all three and selects none automatically (EC-28).
- [ ] `"Ramesh"`, `"ramesh"` and `"Rameśh"` each return the same patient (EC-62).
- [ ] A search with no results shows the "Register '<typed>' as a new patient" action (EC-7).
- [ ] Archived patients do not appear unless `includeArchived=true` is explicitly set (F-10).
- [ ] `/today` shows the last 10 consulted patients; with no visits, it shows an empty state (EC-2).
- [ ] No route URL in the app contains a patient name or phone number (EC-71).

**8. Effort & dependencies.** **M (L while OQ-16 open).** Depends on F-7. Depended on by F-9, F-11, F-13, F-20.

---

### F-9 — Search-first registration + near-match duplicate warning

**1. Readiness — Ready.** D-2 converged on **D + C together** (search-first as prevention, near-match warning as the safety net). Merge tooling is parking-lot **P-1** and gets no section here.

**2. Data model.** No new entity or column. Migration: **none**. `CreatePatientRequest.ConfirmedNotDuplicate: bool` (F-7) is the mechanism.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/patients/near-matches?name=&phone=&ageYears=` | — | `PatientSearchResultDto[]` | 200, 401 | Cookie |
| POST | `/api/patients` (F-7) | `CreatePatientRequest` | `PatientDto` | **409 `ProblemDetails` with `nearMatches[]` when matches exist and `ConfirmedNotDuplicate` is false**; 201 otherwise | Cookie |

`PatientService.FindNearMatchesAsync(string name, string? phone, int? ageYears)` — matches on (a) exact `PhonePrimaryDigits`/`PhoneAltDigits`, or (b) `NormalizedName` similarity within an age tolerance of ±2 years. The 409 is server-enforced, so the rule cannot be bypassed by calling the API directly.

**4. Frontend design.**
- `features/patients/patient-form.component.ts` is **reachable only from** `/patients?query=<text>` when the result list is empty, or from the picker's "not found" action. The route `/patients/new` requires a `fromSearch` query parameter; a direct hit without it redirects to `/patients` with a message (this is the "removes a step" property of D-2 D — the doctor is searching anyway).
- `shared/confirm-dialog/duplicate-warning.component.ts` — rendered on a 409; lists the near-matches with full disambiguators and offers "Open existing patient" (primary) or "This is a different person — register anyway" (secondary, sets `ConfirmedNotDuplicate = true`).

**5. Data integrity check.** This feature is the **Duplicate** mitigation (RISK-2, EC-27). Search-first prevents; the near-match 409 catches what prevention misses; the archive-not-delete interim (F-10) ensures a duplicate discovered later is never "fixed" by a delete that would orphan visits (EC-30). Full merge is **P-1**, deferred with the risk stated there.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/PatientDuplicateTests.cs` — same phone triggers a near-match; same name + age within tolerance triggers one; `ConfirmedNotDuplicate = true` bypasses; whitespace/case variants trigger (EC-64).
- Backend integration: `PatientsControllerTests.DuplicateTests` — POST with an existing phone returns **409** with a populated `nearMatches` array; the same POST with the confirm flag returns 201.
- Frontend unit: `duplicate-warning.component.spec.ts`, `patient-form.component.spec.ts` (direct `/patients/new` without `fromSearch` redirects).
- E2E: `frontend/e2e/duplicate-prevention.spec.ts` — **EC-27** golden path: attempt to register an existing patient, get the warning, open the existing record instead; and the override path for **EC-29** (two genuinely different people, same name and age).

**7. Acceptance criteria.**
- [ ] Navigating directly to `/patients/new` without coming from a search redirects to `/patients` (D-2 D).
- [ ] `POST /api/patients` with a phone number already on file returns 409 listing the matching patients, even when called outside the UI.
- [ ] The warning dialog shows each near-match with name, age, phone tail and last visit date (EC-29).
- [ ] Choosing "Open existing patient" navigates to that patient and creates **no** new record.
- [ ] Choosing "register anyway" creates the patient and writes an audit entry recording that a duplicate warning was overridden.
- [ ] A patient whose name differs only by leading whitespace or a smart quote is detected as a near-match (EC-64).

**8. Effort & dependencies.** **M.** Depends on F-7, F-8. Depended on by nothing structurally; it is a guard on F-7's write path.

---

### F-10 — Patient archive lifecycle (no hard delete)

**1. Readiness — Ready.** R-4 converged: archive, never hard-delete, for any patient with visits.

**2. Data model.** Uses `Patient.RecordStatus`, `ArchivedIntoPatientId`, `ArchivedOn`, `ArchiveNote` (added in `AddPatient`). Migration: **`AddPatientArchiveIndexes`** (filtered index supporting the active-only default in F-8).

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| POST | `/api/patients/{id}/archive` | `ArchivePatientRequest { Reason, ArchivedIntoPatientId? }` | `PatientDto` | 200, 400, 404, 401 | Cookie |
| POST | `/api/patients/{id}/restore` | — | `PatientDto` | 200, 404, 409 (has an active duplicate pointer), 401 | Cookie |
| DELETE | `/api/patients/{id}` | — | 204 when the patient has **zero** visits and zero appointments; **409 `ProblemDetails`** otherwise, with the visit count and a link to archive | 204, 409, 404, 401 | Cookie |

**4. Frontend design.**
- `features/patients/patient-detail.component.ts` gains an "Archive patient" action; the delete action is present only when the patient has no visits and no appointments, and always routes through `shared/confirm-dialog` naming the patient.
- `patient.service.ts` gains `archive(id, req)`, `restore(id)`, `delete(id)`.
- An archived patient's detail page shows a banner: "Archived on <date> — see <survivor name>" when `ArchivedIntoPatientId` is set (EC-30 interim pointer).

**5. Data integrity check.** This feature closes the **Orphan** exposure (RISK-4, EC-37): a patient with visits can never be deleted, only archived, so clinical records never lose their parent. Archiving a duplicate with a pointer to the survivor is the stated interim until merge (**P-1**, EC-30) — never a delete.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/PatientArchiveTests.cs` — delete with ≥1 visit throws the domain exception; archive sets status, timestamp and pointer; restore clears them.
- Backend integration: `PatientsControllerTests.ArchiveTests` — `DELETE` on a patient with visits returns 409 and the row still exists in SQL afterwards (**EC-37**); an archived patient disappears from `/api/patients/search` but is still reachable by id.
- Frontend unit: `patient-detail.component.spec.ts` (delete button hidden when visits exist; archive banner rendered).
- E2E: `frontend/e2e/patient-archive.spec.ts` — **EC-37** golden path: try to delete a patient with history, be offered archive, archive, confirm they leave search but their visits remain viewable.

**7. Acceptance criteria.**
- [ ] `DELETE /api/patients/{id}` on a patient with any visit or appointment returns 409 and the row is unchanged in the database (EC-37).
- [ ] An archived patient no longer appears in default search results but their visits remain fully viewable from history (EC-30).
- [ ] Archiving with `ArchivedIntoPatientId` set renders a pointer banner on the archived record naming the survivor.
- [ ] Archive and blocked-delete both write an `AuditEvent` (F-5).
- [ ] No code path in the solution issues a `DELETE` against `Visit`, `Vitals`, `MedicationLine`, `PrescriptionIssue` or `VisitAmendment` — verified by an architecture test in `PMS.Infrastructure.Tests/NoHardDeleteTests.cs`.

**8. Effort & dependencies.** **S.** Depends on F-7, F-5. Depended on by F-22 (blocked) as its interim.

---

### F-11 — Appointment scheduling + daily list

**1. Readiness — Needs decision (OQ-9).** This is the one `Needs decision` with **no converged option** in the brainstorm doc — D-5 settles the appointment↔visit *link*, not the scheduling *model*. Hence a flat **L**.

> **Assumption (OQ-9 — time-slot calendar or simple dated list; are overlaps allowed?):** building a **simple dated list with an optional time**, not a slot calendar. No slot duration, no working-hours model, no capacity rule. Overlapping and same-day repeat bookings are **allowed with a warning** on the second booking for the same patient on the same day (EC-31). This is the smaller of the two builds and the larger one is additive: a slot calendar can be layered on `ScheduledFor` later without a data migration. **If the owner wants a slot calendar, this feature is a different, larger build — re-plan it rather than stretching this one.**

**2. Data model.** `Appointment` per §4. `ClinicDate` is derived from `ScheduledFor` in the clinic timezone at write time and stored, so the daily list never depends on the browser's timezone (EC-48). Migration: **`AddAppointment`**.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| POST | `/api/appointments` | `CreateAppointmentRequest { PatientId, ScheduledFor, ReasonNote?, AcknowledgedSameDay }` | `AppointmentDto` | 201, 400, 409 (same-day unacknowledged), 401 | Cookie |
| GET | `/api/appointments?date=` | — | `AppointmentListItemDto[]` | 200, 400, 401 | Cookie |
| GET | `/api/appointments/{id}` | — | `AppointmentDto` | 200, 404, 401 | Cookie |
| PUT | `/api/appointments/{id}` | `UpdateAppointmentRequest { ScheduledFor, ReasonNote? }` | `AppointmentDto` | 200, 400, 404, 409 (not Scheduled), 401 | Cookie |

`AppointmentListItemDto` carries `PatientDisplayName`, `AgeDisplay`, `PhoneTail`, `Status`, `IsOverdue`, `VisitId?` — again, never a bare name (EC-29).

**4. Frontend design.**
- `features/appointments/appointment-list.component.ts` — route `/appointments?date=YYYY-MM-DD`, defaults to today; also embedded as the "Today" panel on `/today`.
- `features/appointments/appointment-form.component.ts` — route `/appointments/new`; uses `shared/patient-picker`.
- `features/appointments/appointment.service.ts` — `create(req)`, `listByDate(date: string)`, `get(id)`, `update(id, req)`.
- Same-day duplicate returns 409 → inline warning with "Book anyway" (EC-31).
- Forward-dating months ahead is permitted with no reminder promise, per EC-40 (`accepted`, parking-lot P-2).

**5. Data integrity check.** **Duplicate** exposure (EC-31): two appointments for the same patient on the same day — allowed by design (morning review + evening follow-up) but warned on, so it is a deliberate act rather than an accident. **Orphan** exposure (appointment status vs. the visit behind it) is F-12 and F-16's job, not this feature's.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/AppointmentServiceTests.cs` — `ClinicDate` derived in clinic timezone for a 23:45 UTC booking (**EC-48**); second same-day booking without acknowledgement throws (**EC-31**); a booking six months ahead is accepted (EC-40).
- Backend integration: `PMS.Api.IntegrationTests/Controllers/AppointmentsControllerTests.cs` — `GET /api/appointments?date=` returns exactly that clinic day's rows across a DST boundary.
- Frontend unit: `appointment-list.component.spec.ts` (empty state offers "Start walk-in consultation" — **EC-4**), `appointment-form.component.spec.ts`, `appointment.service.spec.ts`.
- E2E: `frontend/e2e/appointment-booking.spec.ts` — golden path book-and-see-in-today's-list; **EC-31** second same-day booking warning.

**7. Acceptance criteria.**
- [ ] An appointment booked for today appears in `/today`'s list immediately after save.
- [ ] Booking a second appointment for the same patient on the same day returns 409 first and succeeds only after explicit acknowledgement (EC-31).
- [ ] The daily list for a given date is identical regardless of the browser's timezone (EC-48).
- [ ] With no appointments today, the list shows an empty state whose action starts a walk-in consultation (EC-4).
- [ ] Every appointment row shows the patient's age and phone tail alongside the name (EC-29).
- [ ] An appointment can be booked for a date months in the future without error (EC-40).

**8. Effort & dependencies.** **L** (no converged option for OQ-9). Depends on F-7, F-8. Depended on by F-12, F-16.

---

### F-12 — Appointment state machine + Overdue display

**1. Readiness — Ready.** Brainstorm §7.2 converged on the full transition table including the two blocked transitions and the no-auto-transition rule.

**2. Data model.** No new entity — `Status` and `StatusChangedOn` already exist. `IsOverdue` is **computed at read time** (`Status == Scheduled && ScheduledFor < now`) and never persisted, so nothing silently rewrites a past day (EC-36). Migration: **`AddAppointmentStatusIndex`** (index on `(ClinicDate, Status)` for the daily list).

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| POST | `/api/appointments/{id}/status` | `ChangeAppointmentStatusRequest { NewStatus, Note? }` | `AppointmentDto` | 200, 400, 404, **409 (illegal transition)**, 401 | Cookie |

Transition table enforced in `AppointmentStatusPolicy` (`PMS.Domain`), exactly as §7.2 converged:

| From → To | Allowed? | Rule |
|---|---|---|
| Scheduled → Completed | Yes | Normally set automatically by F-16 finalize, not by hand |
| Scheduled → Cancelled | Yes | — |
| Scheduled → No-show | Yes | Manual only; never automatic (EC-36) |
| No-show → Completed | **Yes** | Late arrival; audited (EC-35) |
| Completed → anything | **No** | A finalized visit sits behind it; changing it would orphan that visit's justification |
| Cancelled → Scheduled | **No** | Book a new appointment instead |
| Scheduled past its date | **No auto-transition** | Rendered as `Overdue`; the doctor resolves it (EC-36) |

**4. Frontend design.**
- `features/appointments/appointment-status.component.ts` — inline status control in the list row; **only legal transitions are rendered** (illegal ones are absent, not disabled-with-a-tooltip).
- `appointment.service.ts` gains `changeStatus(id: string, req: ChangeAppointmentStatusRequest)`.
- Overdue rows render with a distinct badge and sort to the top of the daily list.

**5. Data integrity check.** Closes the **Orphan** and **Mutable history** exposures in C-15/RISK-17: `Completed → anything` is blocked so a status can never detach from the visit that justifies it, and no background job ever rewrites a past day's status. `No-show → Completed` is allowed precisely to stop the doctor creating a **duplicate** appointment to work around a block (EC-35).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/AppointmentStatusPolicyTests.cs` — one assertion per cell of the table above, including **EC-35** (No-show → Completed allowed) and Completed → Scheduled rejected.
- Backend integration: `AppointmentsControllerTests.StatusTests` — illegal transition returns 409 and the stored status is unchanged; a legal one writes an `AuditEvent`.
- Frontend unit: `appointment-status.component.spec.ts` — a Completed row exposes no status actions; a Scheduled row past its date renders the Overdue badge (**EC-36**).
- E2E: `frontend/e2e/appointment-status.spec.ts` — **EC-35** mark no-show, patient arrives late, move to Completed; **EC-36** yesterday's Scheduled appointment shows as Overdue rather than auto-marked No-show.

**7. Acceptance criteria.**
- [ ] Each allowed transition in the table succeeds; each blocked one returns 409 and leaves the stored status unchanged.
- [ ] A Scheduled appointment whose date has passed displays as "Overdue" and its stored `Status` is still `Scheduled` when queried in SSMS (EC-36).
- [ ] No background job, timer or startup task modifies an appointment status anywhere in the codebase.
- [ ] `No-show → Completed` succeeds and writes an audit entry (EC-35).
- [ ] Every status change updates `StatusChangedOn`.

**8. Effort & dependencies.** **M.** Depends on F-11, F-5. Depended on by F-16 (which drives Scheduled → Completed automatically).

---

### F-13 — Consultation draft lifecycle + autosave + concurrency guards

**This is R-1, the brainstorm's top recommendation, and the longest link on the critical path.**

**1. Readiness — Ready.** D-1 converged on **option D** (autosave draft → explicit finalize → append-only amendments), with E's "unfinished consultations" prompt borrowed as a login-time nudge rather than a blocking ritual. Only one constant is undecided:

> **Assumption (OQ-3 — acceptable loss window):** autosave debounced at **5 seconds** of typing pause, plus an immediate save on field blur, on section change and on `visibilitychange`. The stated recovery objective this implements: **no finalized visit is ever lost; an in-progress consultation loses at most 5 seconds of typing** (B-3). If the owner picks 10 seconds, it is a configuration constant, not a redesign.

**2. Data model.** `Visit` per §4, including `RowVersion` (optimistic concurrency), `EditingSessionId` + `EditingHeartbeatAt` (two-tab guard, EC-45), `IsBackdated` (EC-39) and `ClinicDate` fixed at draft creation (EC-47). Migration: **`AddVisitDraft`**.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| POST | `/api/visits` | `StartVisitRequest { PatientId, AppointmentId?, ClinicDate? }` | `VisitDraftDto` | 201, 400, 404, 401 | Cookie |
| GET | `/api/visits/{id}` | — | `VisitDetailDto` | 200, 404, 401 | Cookie |
| PATCH | `/api/visits/{id}/draft` | `SaveVisitDraftRequest { ComplaintsText?, DiagnosisText?, Vitals?, Medications[]?, RowVersion, EditingSessionId }` | `VisitDraftDto { …, RowVersion, SavedAt }` | 200, 400, 404, **409 (stale `RowVersion`)**, **423 (another tab holds the edit lock)**, 401 | Cookie |
| POST | `/api/visits/{id}/claim-edit` | `ClaimEditRequest { EditingSessionId }` | `EditClaimDto { Granted, HeldSince }` | 200, 409, 401 | Cookie |
| POST | `/api/visits/{id}/reassign-patient` | `ReassignPatientRequest { NewPatientId, Reason }` | `VisitDetailDto` | 200, 400, **409 (visit finalized)**, 401 | Cookie |
| DELETE | `/api/visits/{id}/draft` | `DiscardDraftRequest { Reason }` | — | 204, 404, 409 (finalized), 401 | Cookie |
| GET | `/api/visits/unfinished` | — | `UnfinishedVisitDto[]` | 200, 401 | Cookie |

`PATCH .../draft` is the single autosave endpoint and writes `Visit` + `Vitals` + `MedicationLine` rows **in one transaction** (EC-58). `POST /api/visits` is rejected while `ClinicProfile.IsSetupComplete` is false (F-6).

**4. Frontend design.**
- `features/consultation/consultation.component.ts` — route `/visits/:id`. Creates nothing itself; `features/consultation/start-consultation.component.ts` (route `/patients/:id/consult`) calls `POST /api/visits` and navigates. The draft therefore exists on the server **before the first keystroke**.
- `features/consultation/consultation-autosave.service.ts` — `register(form: FormGroup, visitId: string): void`, `saveNow(): Promise<void>`, `readonly state: Signal<'idle'|'saving'|'saved'|'error'>`. Debounce 5 s; saves on blur, section change and `visibilitychange`.
- `shared/save-indicator/save-indicator.component.ts` — binds to that signal. **Shows "Not saved" on failure and never shows success optimistically** (EC-51).
- `core/unsaved-changes.guard.ts` + a `beforeunload` handler active while dirty (EC-42).
- `features/consultation/edit-claim.service.ts` — claims the edit lock on load, heartbeats every 15 s; a second tab receives 409 and the component renders **read-only with an explicit message** (EC-45).
- The patient name, age and phone tail are **pinned in a sticky header for the whole consultation** (EC-32), with a "Wrong patient?" action that calls `reassign-patient` while the visit is a draft.
- `features/home/unfinished-consultations.component.ts` — panel on `/today` listing drafts, shown at login as the D-1/E nudge (**EC-33**).
- Discard requires a confirmation naming the patient and is audited (**EC-41**).

**5. Data integrity check.** This feature is the plan's principal **Silent loss** mitigation (RISK-1, RISK-14). Draft row created on open, autosaved within a bounded 5-second window, visible in history from the moment it exists so an abandoned draft is never a record nobody knows about (EC-33). **Mutable history** is handled downstream by F-16/F-18 (finalized visits become immutable). Two-tab last-write-wins is closed by the edit claim (EC-45); network failure is surfaced, never swallowed (EC-51); the visit + vitals + medication write is one transaction (EC-58); `ClinicDate` is fixed at creation so a midnight crossing cannot move the visit out of "today" (EC-47).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/VisitDraftServiceTests.cs` — draft creation fixes `ClinicDate` from the clock and never recomputes it (**EC-47**); a stale `RowVersion` throws (**EC-45**); reassign-patient allowed while draft, rejected once finalized (**EC-32**); a backdated `ClinicDate` sets `IsBackdated` while `CreatedOn` stays real (**EC-39**); discard writes an audit entry (**EC-41**).
- Backend integration: `PMS.Api.IntegrationTests/Controllers/VisitsDraftTests.cs` — two concurrent PATCHes with the same `RowVersion`: one 200, one 409, no lost update; a PATCH that fails mid-transaction leaves **no** partial vitals row (**EC-58**); a second `claim-edit` returns 409 (**EC-45**).
- Frontend unit: `consultation.component.spec.ts` (sticky patient header always rendered — EC-32), `consultation-autosave.service.spec.ts` (debounce timing, save-on-blur, error state never shows "Saved" — **EC-51**), `edit-claim.service.spec.ts`, `unsaved-changes.guard.spec.ts` (**EC-42**).
- E2E: `frontend/e2e/consultation-draft.spec.ts` — golden path open → type → observe "Saved"; **EC-42** reload mid-consultation and confirm the text returns; **EC-33** close the tab mid-consultation, log in again, find it under "Unfinished consultations"; **EC-45** open a second tab and confirm it is read-only; **EC-51** simulate an offline PATCH and confirm the "Not saved" indicator appears.

**7. Acceptance criteria.**
- [ ] Opening a consultation creates a persisted `Visit` row with `LifecycleState = Draft` before any field is typed into (query it in SSMS to confirm).
- [ ] Typing, pausing 5 seconds and killing the browser process loses no more than the last 5 seconds of typing (B-3, EC-53).
- [ ] Reloading the page mid-consultation restores every entered field (EC-42, EC-46).
- [ ] A draft appears in "Unfinished consultations" on `/today` and in that patient's history, clearly marked as a draft (EC-33).
- [ ] A second browser tab on the same visit is read-only and says so; the first tab keeps saving normally (EC-45).
- [ ] With the network disconnected, the indicator reads "Not saved" and never "Saved" (EC-51).
- [ ] A consultation opened at 23:59 and finalized at 00:03 keeps the opening day as its `ClinicDate` and stores the true `FinalizedAt` instant (EC-47).
- [ ] Reassigning a draft to a different patient succeeds, writes an audit entry, and the same call on a finalized visit returns 409 (EC-32).
- [ ] Discarding a draft requires a confirmation naming the patient and writes an audit entry (EC-41).
- [ ] `POST /api/visits` returns 409 while clinic setup is incomplete (EC-1).

**8. Effort & dependencies.** **L** (genuine build cost: autosave, optimistic concurrency, edit-claim protocol, unload guard, transactional multi-entity write). Depends on F-7, F-6, F-5. **Depended on by F-14, F-15, F-16, F-17, F-18, F-19** — the widest blocking reach in the plan and the reason it leads the critical path.

---

### F-14 — Vitals capture + not-recorded reasons + doctor-configured ranges

**1. Readiness — Needs decision (OQ-1).** D-7 converged on **option C** (value **or** an explicit "not recorded" reason from a short doctor-defined list). This is a ratification, not an open design space.

> **Assumption (OQ-1 — what happens when a vital genuinely cannot be taken):** building D-7 C. Each of temperature, BP and pulse requires **either** a value **or** a selected reason from `NotRecordedReason`; the reason list is doctor-editable and seeded empty except for a single "Not recorded" fallback so the app is usable on day one. The printed prescription and history render "BP: not recorded — <reason>", never a blank. Cost on the normal path: zero keystrokes.
>
> **Clinical boundary (non-negotiable, per brainstorm D-7 and EC-13):** the application **never asserts a plausible range of its own**. `VitalRangeSetting` is blank until the doctor configures it, and a configured range produces a **soft warning only** — never a hard block, never a clinical judgement in code.

**2. Data model.** `Vitals` (PK = `VisitId`), `NotRecordedReason`, `VitalRangeSetting` per §4. BP is **two integer fields**, temperature is **value + unit selector** — never free text (EC-66). Migration: **`AddVitalsAndVitalRanges`**.

**3. API design.** Vitals are saved through `PATCH /api/visits/{id}/draft` (F-13) — no separate write endpoint, so vitals can never be saved outside the visit transaction. Settings endpoints:

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/settings/vital-reasons` | — | `NotRecordedReasonDto[]` | 200, 401 | Cookie |
| POST | `/api/settings/vital-reasons` | `CreateReasonRequest { VitalKind, Label }` | `NotRecordedReasonDto` | 201, 400, 401 | Cookie |
| DELETE | `/api/settings/vital-reasons/{id}` | — | 204 (deactivates; never hard-deletes a reason already referenced) | 204, 404, 401 | Cookie |
| GET | `/api/settings/vital-ranges` | — | `VitalRangeSettingDto[]` | 200, 401 | Cookie |
| PUT | `/api/settings/vital-ranges` | `UpdateVitalRangesRequest` | `VitalRangeSettingDto[]` | 200, 400, 401 | Cookie |

`VisitValidator` (FluentValidation) enforces at **finalize** (F-16), not on every autosave: each vital has a value or a reason.

**4. Frontend design.**
- `features/consultation/vitals-section.component.ts` — three rows; each has numeric input(s) and a "Not recorded" toggle that reveals a reason select. Temperature has a C/F unit selector; BP has two numeric inputs.
- `features/settings/vital-settings.component.ts` — route `/settings/vitals`; manages reasons and ranges. `SettingsService.vitalReasons()`, `.addReason(req)`, `.deactivateReason(id)`, `.vitalRanges()`, `.updateRanges(req)`.
- A configured range breach renders an inline amber warning next to the field with the doctor's own threshold quoted; the value still saves (**EC-13**).
- Typing "120/80" into the systolic box is parsed into both fields on blur rather than rejected — a convenience that keeps the structure (EC-66).

**5. Data integrity check.** Closes the fabrication vector behind RISK-3 / EC-19: with no exception path the doctor either abandons the record (Silent loss) or invents a number (invisible corruption). A recorded "not recorded — cuff unavailable" is a durable fact; a blank is an ambiguity someone misreads years later. Structured BP and unit-tagged temperature keep history comparable across visits (EC-66) — free-text vitals would make **Mutable history** of the whole vitals series.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/VitalsValidationTests.cs` — value-or-reason enforced per vital at finalize (**EC-19**); a value plus a reason is rejected as contradictory; range warnings are returned as warnings, never as validation errors (**EC-13**); with no `VitalRangeSetting` configured, temperature 450 saves with no warning at all.
- Backend integration: `PMS.Api.IntegrationTests/Controllers/VitalsTests.cs` — finalize blocked when BP has neither value nor reason; deactivating a referenced reason keeps historic visits rendering it.
- Frontend unit: `vitals-section.component.spec.ts` (toggle reveals the reason select; "120/80" paste splits into two fields — **EC-66**), `vital-settings.component.spec.ts`.
- E2E: `frontend/e2e/vitals-exception.spec.ts` — **EC-19** golden path: BP cannot be taken, select a reason, finalize succeeds, and the printed prescription reads "BP: not recorded — cuff unavailable".

**7. Acceptance criteria.**
- [ ] Finalize is rejected when any of temperature, BP or pulse has neither a value nor a reason (EC-19).
- [ ] Selecting a reason for BP allows finalize and prints "BP: not recorded — <reason>", not a blank (EC-19).
- [ ] BP is stored as two integers and temperature as value + unit; no vitals value is stored as free text (EC-66).
- [ ] With no ranges configured, a temperature of 450 saves with no warning (the system asserts no clinical range of its own — EC-13).
- [ ] With a doctor-configured range, an out-of-range value shows an amber warning and still saves — there is no code path that blocks it.
- [ ] The reason list is editable at `/settings/vitals` and changes appear in the consultation form without a redeploy.

**8. Effort & dependencies.** **M (L while OQ-1 open).** Depends on F-13. Depended on by F-16 (finalize validation), F-17 (print rendering).

---

### F-15 — Complaints, diagnosis, medications + pre-finalize review

**1. Readiness — Needs decision (OQ-13).**

> **Assumption (OQ-13 — which medication fields are required, and is diagnosis mandatory before printing?):** building the §8.3/§8.1 proposed handling — **`DrugName` required; dosage, frequency, duration and instructions optional** (EC-23); **diagnosis not mandatory** but a **one-time warning at finalize** when it is blank, overridable (EC-8). Hard-blocking on diagnosis is clinical rule-setting and is the doctor's call, not the plan's. **Zero medications is explicitly allowed** — an advice-only visit prints "No medication prescribed" (EC-3). No maximum medication count (EC-16).

**2. Data model.** `Visit.ComplaintsText` and `Visit.DiagnosisText` (`nvarchar(4000)`, full Unicode, formatting stripped on paste — C-20/EC-65), `MedicationLine` per §4 with unique `(VisitId, Sequence)`. Migration: **`AddMedicationLines`**.

**3. API design.** Written through `PATCH /api/visits/{id}/draft` (F-13) — the whole medication list is sent as an ordered array and reconciled server-side by `Sequence`, so reordering and deletion never orphan a line. One additional read endpoint:

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/visits/{id}/review` | — | `PreFinalizeReviewDto { Patient, Vitals, ComplaintsText, DiagnosisText, Medications[], Warnings[] }` | 200, 404, 401 | Cookie |

`Warnings[]` carries the blank-diagnosis warning (EC-8), any doctor-configured vitals-range breach (EC-13) and blank medication sub-fields (EC-23) — **all advisory, none blocking**.

**4. Frontend design.**
- `features/consultation/complaints-section.component.ts` — textarea with a visible character counter against the 4,000 limit (EC-10); paste handler strips formatting and keeps text (EC-65).
- `features/consultation/diagnosis-section.component.ts` — same pattern.
- `features/consultation/medications-section.component.ts` — repeating rows, keyboard-first (`Enter` adds a row, `Alt+Up/Down` reorders), drug name required, other four free.
- `features/consultation/pre-finalize-review.component.ts` — **R-19**: a read-only screen rendering the medication list **exactly as it will print**, shown between "Finalize" and the print action. Review, not validation (EC-14).
- `consultation.service.ts` gains `getReview(visitId: string): Observable<PreFinalizeReviewDto>`.

**5. Data integrity check.** **Silent loss** (EC-10): long complaint text clipped at print or export is invisible clinical loss — prevented by an enforced stored max with a visible counter plus wrapping (never clipping) in the print layout (F-17). **EC-14** (a 5-vs-50 dosage typo) is explicitly *not* addressed by validation — no clinical guard is appropriate here; the pre-finalize review is the mitigation, and it is a review, not a rule. Medication reconciliation by `Sequence` prevents orphaned lines when rows are reordered.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/MedicationLineTests.cs` — reordering rewrites `Sequence` without orphaning (unique constraint holds); a line with only a drug name is valid (**EC-23**); zero medications is valid (**EC-3**); 40 lines are accepted (**EC-16**). `PMS.Application.Tests/Services/VisitReviewTests.cs` — blank diagnosis produces a warning, not an error (**EC-8**).
- Backend integration: `PMS.Api.IntegrationTests/Controllers/VisitsContentTests.cs` — a 4,000-character complaint round-trips exactly; 4,001 returns 400 (**EC-10**); emoji and mixed-script text survive the round trip (**EC-65**, **EC-62**).
- Frontend unit: `medications-section.component.spec.ts` (keyboard add/reorder), `complaints-section.component.spec.ts` (counter, paste stripping), `pre-finalize-review.component.spec.ts` (renders blanks visibly — EC-23).
- E2E: `frontend/e2e/consultation-content.spec.ts` — golden path complaints → diagnosis → two medications → review; **EC-3** advice-only visit with zero medications; **EC-8** finalize with a blank diagnosis after acknowledging the warning.

**7. Acceptance criteria.**
- [ ] A medication line saves with only a drug name filled in (EC-23).
- [ ] A visit with zero medications can be finalized and its prescription prints "No medication prescribed" (EC-3).
- [ ] Finalizing with a blank diagnosis shows one warning and proceeds when acknowledged; it is never hard-blocked (EC-8).
- [ ] The complaints field shows a live character counter and rejects input beyond 4,000 characters with a message, rather than silently truncating (EC-10).
- [ ] Pasting formatted rich text into complaints stores the plain text and drops the formatting (EC-65).
- [ ] The pre-finalize review shows every medication row exactly as it will print, including empty sub-fields (EC-14, EC-23).
- [ ] Medications can be added, reordered and removed using the keyboard alone (B-1 keyboard-only path).
- [ ] 40 medication lines save and render without error (EC-16).

**8. Effort & dependencies.** **M (L while OQ-13 open).** Depends on F-13. Depended on by F-16, F-17.

---

### F-16 — Finalize + appointment auto-complete + idempotency

**1. Readiness — Ready.** D-5 converged on **option C** (optional link; finalizing auto-sets the appointment to Completed), and D-1 D fixes finalize as the commit point.

**2. Data model.** `Visit.FinalizedAt`, `Visit.LifecycleState` (already added in `AddVisitDraft`) plus `Visit.FinalizeRequestId: Guid?` for server-side de-duplication (EC-44). Migration: **`AddVisitFinalization`**.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| POST | `/api/visits/{id}/finalize` | `FinalizeVisitRequest { RowVersion, FinalizeRequestId, AcknowledgedWarnings[] }` | `VisitDetailDto` (includes the created `PrescriptionIssue` id from F-17) | 200, 400, 404, 409 (stale/already finalized with a **different** request id), 422 (vitals incomplete), 401 | Cookie |

`VisitService.FinalizeAsync` in one transaction: validate vitals (F-14), set `LifecycleState = Finalized` and `FinalizedAt`, create the `PrescriptionIssue` snapshot (F-17), set the linked appointment to `Completed` via `AppointmentStatusPolicy` (F-12), and write two `AuditEvent` rows (`VisitFinalized`, `PrescriptionIssued`). **Idempotent:** a repeat call with the same `FinalizeRequestId` returns 200 with the original result and creates nothing new (EC-44).

**4. Frontend design.**
- `features/consultation/finalize-button.component.ts` — generates a `FinalizeRequestId` once per attempt, disables on first click, and calls `ConsultationService.finalize(visitId, req)`. Double-click is neutralised on both sides (EC-44).
- On success it navigates to `/visits/:id/print` (F-17).
- The consultation form switches to read-only immediately after a 200; further edits go through F-18 amendments.

**5. Data integrity check.** The commit point of the D-1 lifecycle: after it, the visit is **immutable** and corrections append (F-18) — this is the **Mutable history** closure. Double-submit **Duplicate** (EC-44) is closed on both client and server. The appointment auto-complete closes the **Orphan** exposure in RISK-7: `Completed` now always has a visit behind it, and the doctor never has to remember to set it. Snapshot-before-render (EC-56) means the record survives a rendering failure.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/VisitFinalizeServiceTests.cs` — vitals incomplete → 422 (EC-19); linked appointment moves Scheduled → Completed; a **walk-in** with no appointment finalizes fine (D-5 C, **EC-38**); repeat `FinalizeRequestId` returns the original (**EC-44**); a finalize that throws during snapshot creation rolls back the whole transaction, leaving the visit a draft.
- Backend integration: `PMS.Api.IntegrationTests/Controllers/VisitFinalizeTests.cs` — two parallel finalize calls create exactly one `PrescriptionIssue` (**EC-44**); a subsequent `PATCH .../draft` on a finalized visit returns 409.
- Frontend unit: `finalize-button.component.spec.ts` (disabled on first click; single request on double-click).
- E2E: `frontend/e2e/consultation-finalize.spec.ts` — golden path finalize → print preview; **EC-44** double-click produces one visit and one prescription; **EC-38** cancelled appointment, doctor sees the patient anyway as a walk-in.

**7. Acceptance criteria.**
- [ ] Finalize sets `LifecycleState = Finalized` and `FinalizedAt`, and every subsequent draft PATCH on that visit returns 409.
- [ ] A visit linked to a Scheduled appointment sets that appointment to Completed in the same transaction; the doctor performs no extra click (D-5 C).
- [ ] A walk-in visit with no appointment finalizes with no appointment side effect (EC-38).
- [ ] Double-clicking Finalize produces exactly one `Visit`, one `PrescriptionIssue` and one audit entry (EC-44).
- [ ] Finalize with a vital that has neither value nor reason returns 422 and the visit stays a draft (EC-19).
- [ ] A failure during snapshot creation leaves the visit as a draft with no partial finalize state (EC-58).
- [ ] Finalize writes `VisitFinalized` and `PrescriptionIssued` audit rows.

**8. Effort & dependencies.** **M.** Depends on F-13, F-14, F-15, F-12. Depended on by F-17, F-18, F-19.

---

### F-17 — Prescription snapshot, print layout, reprint

**1. Readiness — Ready.** D-4 converged on **option C with reprints flagged (light D)**: print stores an immutable snapshot of exactly what was printed; reprints are recorded and flagged.

**2. Data model.** `PrescriptionIssue` per §4 — `SnapshotJson` holds the fully-resolved document (clinic header fields, patient demographics **as at issue time**, vitals, diagnosis, medication lines, footer), `SnapshotHash` is a SHA-256 of it, `RenderedPdf` optionally caches the bytes. Append-only: no update or delete path exists. Migration: **`AddPrescriptionIssue`**.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/visits/{id}/prescription-issues` | — | `PrescriptionIssueDto[]` | 200, 404, 401 | Cookie |
| GET | `/api/prescription-issues/{issueId}` | — | `PrescriptionSnapshotDto` | 200, 404, 401 | Cookie |
| GET | `/api/prescription-issues/{issueId}/pdf` | — | `application/pdf` | 200, 404, 500, 401 | Cookie |
| POST | `/api/visits/{id}/prescription-issues/reprint` | `ReprintRequest { SourceIssueId }` | `PrescriptionIssueDto` (`IssueKind = Reprint`, **snapshot copied verbatim from the source**) | 201, 404, 409, 401 | Cookie |

The original issue is created inside `FinalizeAsync` (F-16) — **the snapshot is written before any rendering is attempted** (EC-56), so a rendering failure never costs the record. PDF rendering uses **QuestPDF** in `PMS.Infrastructure/Printing/QuestPdfRenderer.cs`; the browser print path renders the same snapshot through a dedicated Angular print view.

**4. Frontend design.**
- `features/prescription/prescription-print.component.ts` — route `/visits/:id/print`; loads the snapshot (not the live visit) and renders the print layout. Calls `window.print()` on a user action.
- `features/prescription/prescription-layout.component.ts` + `prescription-print.scss` — implements **R-16**: repeating header on every page, "Page 1 of N", medication rows never split across a page break (`break-inside: avoid`), long complaint/diagnosis text **wraps and never clips** (EC-9, EC-10), long names truncate in the header only (EC-11), and a print font carrying non-Latin scripts (EC-62).
- `features/prescription/prescription-history.component.ts` — lists all issues for a visit with kind and timestamp; a "Reprint" action creates a flagged `Reprint` issue and re-renders **the original snapshot** (EC-52).
- `prescription.service.ts` — `listIssues(visitId)`, `getSnapshot(issueId)`, `downloadPdf(issueId)`, `reprint(visitId, req)`.
- All free text is escaped on output, **including in the print view** (R-21, EC-61) — Angular interpolation does this by default; the acceptance criteria below verify no `innerHTML` binding exists on the print path.

**5. Data integrity check.** Closes **Mutable history** on the single most consequential artefact in the product (RISK-5, EC-34): once a visit can be amended, "what the patient is holding" and "what the record says" diverge, and only the snapshot reconciles them. Closes a **Duplicate** vector too (EC-52): the app cannot know whether the print dialog completed, so "issued" means "generated", and reprint is one click — so a doctor reacting to a cancelled dialog never starts a second visit. Snapshot-before-render closes EC-56.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/PrescriptionServiceTests.cs` — the snapshot captures the patient's name **as at issue time** and is unaffected by a later demographic edit (**EC-34**); a reprint copies the source snapshot byte-for-byte and sets `IssueKind = Reprint` (**EC-52**); zero medications renders "No medication prescribed" (**EC-3**); `SnapshotHash` matches the content.
- Backend integration: `PMS.Api.IntegrationTests/Controllers/PrescriptionsControllerTests.cs` — no endpoint or context path can update or delete a `PrescriptionIssue`; a PDF render failure returns 500 with `ProblemDetails` while the issue row still exists (**EC-55**, **EC-56**).
- Frontend unit: `prescription-layout.component.spec.ts` (page-break classes applied; header fields present), `prescription.service.spec.ts`.
- E2E: `frontend/e2e/prescription-print.spec.ts` (Playwright, PDF via `page.pdf()`) — golden path finalize → print; **EC-9** a 12-medication prescription paginates with a repeating header and "Page 1 of 2"; **EC-10** a 4,000-character complaint wraps with no clipped text; **EC-62** a Devanagari patient name renders as glyphs, not boxes; **EC-11** a 300-character name truncates in the header only. **Print output is verified on Chrome, Edge and Safari before go-live (C-35/R-16)** — the Safari pass is a manual checklist item in the release runbook, since Playwright's WebKit is not Safari's print engine.

**7. Acceptance criteria.**
- [ ] Finalizing creates exactly one `PrescriptionIssue` with `IssueKind = Original` and a populated `SnapshotJson`.
- [ ] Editing the patient's name after finalize does **not** change what a reprint of that visit prints (EC-34).
- [ ] Reprint creates a new row flagged `Reprint` and reproduces the original document identically (EC-52).
- [ ] A prescription with 12 medications prints across pages with a repeating clinic header, "Page N of M", and no medication row split across a page break (EC-9, EC-16).
- [ ] A 4,000-character complaint prints wrapped in full with no clipped text (EC-10).
- [ ] A patient name in Devanagari prints as readable glyphs, not boxes (EC-62).
- [ ] A visit with no medications prints "No medication prescribed" rather than an empty section (EC-3).
- [ ] Printing is blocked while `ClinicProfile.IsSetupComplete` is false (EC-1).
- [ ] Free text containing `<script>` or `&` renders literally on the printed page (EC-61).
- [ ] No code path updates or deletes a `PrescriptionIssue` — verified by `NoHardDeleteTests`.
- [ ] The layout has been visually verified on Chrome, Edge and Safari and the results recorded in the release checklist (C-35).

**8. Effort & dependencies.** **L** (print layout across three browsers plus PDF rendering is genuinely over a week; RISK-18 is High/Major). Depends on F-16, F-6. Depended on by F-18, F-19, F-20.

---

### F-18 — Amendments after finalize

**1. Readiness — Needs decision (OQ-2).** D-1 converged on **option D** (append-only amendments); OQ-2 is the owner ratifying that a finalized consultation may be edited at all and that changes are visibly marked.

> **Assumption (OQ-2 — may a finalized consultation be edited after printing, and must the change be marked?):** building D-1 **option D** as converged — **yes, via append-only amendments**, never in-place edits, each stamped with a timestamp and a required reason and always visible in history. If the owner instead chooses D-1 option C (post-finalize edits overwrite silently), **that must be recorded as an accepted risk with no audit answer** (brainstorm §14) — do not simply drop this feature and leave the gap unstated.
>
> **Diverging from the brainstorm doc: no.** This plan takes D-1 D exactly as converged, including the honest trade-off stated in §14 — the doctor needs one sentence of onboarding that corrections append rather than overwrite. That onboarding line belongs in the amendment dialog copy, and is listed as an acceptance criterion below.

**2. Data model.** `VisitAmendment` per §4, append-only. Amendable fields in Phase 1: `ComplaintsText`, `DiagnosisText`, medication lines (as a whole-list replacement recorded as one amendment), vitals values. Each amendment stores `PriorValue` and `NewValue`. `Visit` itself is updated to the new value **and** the prior value is preserved in the amendment row — the history renders both. Migration: **`AddVisitAmendment`**.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| POST | `/api/visits/{id}/amendments` | `CreateAmendmentRequest { FieldChanged, NewValue, Reason }` | `VisitAmendmentDto` | 201, 400, 404, **409 (visit is still a draft — edit it instead)**, 401 | Cookie |
| GET | `/api/visits/{id}/amendments` | — | `VisitAmendmentDto[]` | 200, 404, 401 | Cookie |

`Reason` is required (min length enforced by validation). Each amendment writes a `VisitAmended` audit row in the same transaction. A medication or diagnosis amendment offers an **amended reissue** (`PrescriptionIssueKind.AmendedReissue`, F-17), which creates a **new** snapshot without touching the original.

**4. Frontend design.**
- `features/consultation/amendment-dialog.component.ts` — opened from a finalized visit's read-only view; shows the current value, a new-value input and a required reason box, plus the onboarding sentence: "Corrections are added as a dated amendment. The original record and the prescription already given to the patient are preserved."
- `features/history/visit-detail.component.ts` (F-19) renders an "Amendments" section listing each amendment with its date, field, prior value and reason.
- `consultation.service.ts` gains `createAmendment(visitId, req)` and `listAmendments(visitId)`.

**5. Data integrity check.** This is the **Mutable history** closure for the visit record (RISK-1's second half, EC-34): a finalized record can never be silently rewritten; corrections append with a timestamp and reason, and the original prescription snapshot stays intact so the paper in the patient's hand remains reconcilable with the record.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/VisitAmendmentServiceTests.cs` — amending a draft returns the domain error (use the draft path instead); an amendment without a reason is rejected; `PriorValue` captures the pre-amendment value exactly; an amendment never mutates any existing `PrescriptionIssue` (**EC-34**).
- Backend integration: `PMS.Api.IntegrationTests/Controllers/AmendmentsControllerTests.cs` — after an amendment, `GET /api/prescription-issues/{originalId}` returns unchanged content; an amended reissue creates a second issue row.
- Frontend unit: `amendment-dialog.component.spec.ts` (reason required; onboarding copy present).
- E2E: `frontend/e2e/visit-amendment.spec.ts` — **EC-34**: finalize and print, then amend the diagnosis, then confirm history shows both the amendment and the original printed snapshot.

**7. Acceptance criteria.**
- [ ] A finalized visit cannot be edited in place through any endpoint; the only write path is `POST /api/visits/{id}/amendments`.
- [ ] An amendment without a reason returns 400.
- [ ] After an amendment, the original `PrescriptionIssue` content is byte-identical to before (EC-34).
- [ ] Visit history shows every amendment with its date, the field changed, the prior value and the reason.
- [ ] An amended reissue creates a new `PrescriptionIssue` flagged `AmendedReissue` and leaves the original in place.
- [ ] The amendment dialog states in one sentence that corrections append and the original is preserved (brainstorm §14 onboarding note).
- [ ] Each amendment writes a `VisitAmended` audit row in the same transaction.

**8. Effort & dependencies.** **M (L while OQ-2 open).** Depends on F-16, F-17, F-5. Depended on by F-19 (rendering).

---

### F-19 — Patient history + visit detail + date filter

**1. Readiness — Ready.** C-25's default is adopted as converged: **visit-date range filter; drafts shown and clearly flagged.**

**2. Data model.** No new entity. Migration: **`AddVisitHistoryIndexes`** — index on `Visit (PatientId, ClinicDate DESC)` to hold the < 2 s history-load budget (B-6).

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/patients/{id}/visits?from=&to=&includeDrafts=true` | — | `VisitListItemDto[]` | 200, 400, 404, 401 | Cookie |
| GET | `/api/visits/{id}/detail` | — | `VisitDetailDto { Visit, Vitals, Medications[], Amendments[], PrescriptionIssues[] }` | 200, 404, 401 | Cookie |

`VisitListItemDto { Id, ClinicDate, LifecycleState, DiagnosisSummary, MedicationCount, HasAmendments, IsBackdated, PrescriptionIssueCount }`.

**4. Frontend design.**
- `features/history/patient-history.component.ts` — route `/patients/:id/history`; timeline of visits, newest first, with `shared/date-range-filter/date-range-filter.component.ts`.
- `features/history/visit-detail.component.ts` — route `/visits/:id/detail`; read-only render of vitals, complaints, diagnosis, medications, amendments (F-18) and prescription issues (F-17).
- Draft visits render with a distinct "Draft — unfinished" badge and a "Resume" action (**EC-33**); backdated visits show both `ClinicDate` and `CreatedOn` with a "recorded later" label (**EC-39**).
- A patient with zero visits shows a "First visit" empty state, not an empty table (**EC-5**).
- `history.service.ts` — `listVisits(patientId, filter)`, `getVisitDetail(visitId)`.

**5. Data integrity check.** Drafts are **always visible** here — that is the obligation D-1 introduces (brainstorm §14: "drafts must always be visible in history, or an abandoned draft becomes a record nobody knows exists"). Backdating is allowed but labelled: `ClinicDate` and `CreatedOn` are both shown, so undisclosed backdating is impossible (**EC-39**). This is a read-only feature — no write path, no new integrity exposure.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/VisitHistoryServiceTests.cs` — date filter is inclusive on both ends in clinic timezone; drafts included by default (**EC-25**, **EC-33**); `IsBackdated` surfaces (**EC-39**).
- Backend integration: `PMS.Api.IntegrationTests/Controllers/VisitHistoryTests.cs` — a patient with 200 visits returns the filtered page in under 2 s (B-6).
- Frontend unit: `patient-history.component.spec.ts` (draft badge, first-visit empty state — **EC-5**), `date-range-filter.component.spec.ts`, `visit-detail.component.spec.ts` (amendments section rendered).
- E2E: `frontend/e2e/patient-history.spec.ts` — golden path view history and open a past visit; **EC-33** an unfinished draft is visible and resumable from history.

**7. Acceptance criteria.**
- [ ] A patient's history lists every visit newest-first with date, diagnosis summary and medication count.
- [ ] Draft visits appear in history with a visible "Draft — unfinished" badge and a working Resume action (EC-33).
- [ ] A date-range filter returns visits on the boundary dates themselves (inclusive) in clinic timezone.
- [ ] A backdated visit shows both its clinic date and the date it was recorded (EC-39).
- [ ] A patient with no visits shows a "First visit" state, not an empty grid (EC-5).
- [ ] Opening a past visit shows vitals, complaints, diagnosis, medications, amendments and prescription issues on one screen.
- [ ] History for a patient with 200 visits loads in under 2 s (B-6, BRD NFR Performance).

**8. Effort & dependencies.** **M.** Depends on F-13, F-16, F-17, F-18. Depended on by F-20.

---

### F-20 — Export CSV/PDF (scoped, confirmed, audited)

**1. Readiness — Needs decision (OQ-10).** D-6 converged on **B + C + F** (scoped to the current view, confirmation naming the record count, audit entry).

> **Assumption (OQ-10 — current-view export or full-database export, and who may use it):** building **D-6 B+C+F** — export is scoped to the current view (this patient's history, or this date range), a confirmation names exactly what is leaving and how many records, and an `DataExported` audit row is written. **No full-database export button exists.** Password-protected PDF / passphrase-protected CSV is parking-lot **P-3**. If the owner requires a full-database export, that is a new scope decision with the privacy consequence stated in D-6 option A.
>
> **Non-negotiable regardless of the answer (D-6, EC-59, EC-60):** CSV output neutralises formula-injection prefixes (`=`, `+`, `-`, `@`) and applies RFC 4180 quoting for commas, quotes and newlines inside complaint and diagnosis text.

**2. Data model.** No new entity — export is recorded as an `AuditEvent` (F-5). Migration: **none**.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/export/preview?scope=patient|visits&patientId=&from=&to=` | — | `ExportPreviewDto { RecordCount, ScopeDescription, Columns[] }` | 200, 400, 401 | Cookie |
| POST | `/api/export/csv` | `ExportRequest { Scope, PatientId?, From?, To?, ConfirmedCount }` | `text/csv` (filename `pms-<scope>-<yyyyMMdd-HHmm>.csv`) | 200, 400, **409 (count changed since preview)**, 401 | Cookie |
| POST | `/api/export/pdf` | `ExportRequest` | `application/pdf` | 200, 400, 409, 500, 401 | Cookie |

Backed by `ExportService` using `ICsvWriter` (`PMS.Infrastructure/Export/CsvWriter.cs`, RFC 4180 + formula-prefix neutralisation) and the F-17 `IPdfRenderer`. **Export is disabled when `RecordCount == 0`** — a zero-row file that looks like a successful export is never produced (**EC-6**).

**4. Frontend design.**
- `features/export/export-dialog.component.ts` — opened from patient history (F-19) and from the search results view; calls `preview()` first and renders "Export 37 visits for Ramesh K. (01 Jan – 18 Aug 2026) as CSV?" before enabling the confirm button (D-6 C).
- `export.service.ts` — `preview(scope: ExportScope): Observable<ExportPreviewDto>`, `downloadCsv(req)`, `downloadPdf(req)`.
- A one-line standing notice in the dialog: exported files are unencrypted and remain on this computer until deleted (**EC-67** — the app cannot control the filesystem and says so rather than pretending otherwise).

**5. Data integrity check.** **Silent loss in the exported artefact** (EC-59): an unescaped comma or newline in a complaint splits the CSV row and the export *looks* successful — closed by RFC 4180 quoting. **Weaponised export** (EC-60): a field starting `=` executes when the file is opened in a spreadsheet on the clinic PC — closed by prefix neutralisation. Privacy exposure (EC-67) is reduced by scoping and made answerable by the audit entry, not eliminated — stated honestly here and in §8.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/ExportServiceTests.cs` and `PMS.Infrastructure.Tests/CsvWriterTests.cs` — a complaint containing `a, b "c" \n d` round-trips through a CSV parser to the identical string (**EC-59**); a field beginning `=cmd|` is neutralised (**EC-60**); zero records returns the disabled result rather than an empty file (**EC-6**); scope never widens beyond the requested patient/date range.
- Backend integration: `PMS.Api.IntegrationTests/Controllers/ExportControllerTests.cs` — a successful export writes exactly one `DataExported` audit row naming the scope and count; a mismatched `ConfirmedCount` returns 409.
- Frontend unit: `export-dialog.component.spec.ts` (confirm disabled until preview returns; count displayed; disabled at zero).
- E2E: `frontend/e2e/export.spec.ts` — golden path export a patient's history to CSV and assert the downloaded file's row count; **EC-60** a patient note beginning with `=` downloads neutralised.

**7. Acceptance criteria.**
- [ ] Every export is preceded by a confirmation naming the scope and the exact record count (D-6 C).
- [ ] There is no UI or API route that exports the entire database in one call (D-6 B).
- [ ] A complaint containing commas, double quotes and newlines survives a CSV round-trip byte-identically (EC-59).
- [ ] A field beginning `=`, `+`, `-` or `@` is neutralised in the CSV output (EC-60).
- [ ] Export is disabled and produces no file when the scope contains zero records (EC-6).
- [ ] Each completed export writes one `DataExported` audit row recording scope, record count and timestamp (D-6 F).
- [ ] The export dialog states that the downloaded file is unencrypted and remains on the computer (EC-67).
- [ ] A failed PDF render returns an explicit error and no partial file, rather than a silent blank download (EC-55).

**8. Effort & dependencies.** **M (L while OQ-10 open).** Depends on F-8, F-19, F-5. Depended on by nothing.

---

### F-21 — Backup + visible backup status

**1. Readiness — Needs decision (OQ-3).** R-9 converged on the mechanism (visible status, last-success timestamp, rehearsed restore); the numbers are the owner's.

> **Assumption (OQ-3 — the recovery objective that replaces "No data loss"):** building against **RPO = 24 hours for the database plus ≤ 5 seconds of in-progress typing** (the F-13 autosave window), delivered as a **nightly SQL Server full backup at 01:00 plus transaction-log backups every 30 minutes**, retained **30 days**, with a **verified restore rehearsed before go-live** and re-rehearsed quarterly. If the owner states a tighter RPO, the log-backup interval changes; nothing else does.

**2. Data model.** `BackupStatus` singleton per §4. Migration: **`AddBackupStatus`**. The backups themselves are **SQL Server Agent jobs created in SSMS** — scripted into `backend/deploy/sql/backup-jobs.sql` and version-controlled, not managed by the application. The application only *observes* them.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status codes | Auth |
|---|---|---|---|---|---|
| GET | `/api/backup-status` | — | `BackupStatusDto { LastSuccessAt, HoursSinceLastSuccess, State (Ok/Stale/Failed/Unknown), Message, FreeSpaceGb }` | 200, 401 | Cookie |

`BackupStatusProbe` (`PMS.Infrastructure/Backup/`) reads `msdb.dbo.backupset` for the last successful full/log backup and queries `sys.dm_os_volume_stats` for free space (**EC-57**). `State = Stale` when the last success is older than 26 hours; `Failed` when the last recorded attempt failed.

**4. Frontend design.**
- `features/home/backup-status-card.component.ts` — permanent card on `/today`: green with the last-success timestamp, amber when stale, red when failed or when free space is below the configured threshold. **The timestamp is always shown, never just a green tick** (EC-50).
- `backup-status.service.ts` — `get(): Observable<BackupStatusDto>`, polled every 15 minutes.
- `doc`/runbook deliverable: `backend/deploy/RESTORE-RUNBOOK.md` covering the rehearsed restore (**EC-54**) — a written artefact, not code.

**5. Data integrity check.** Closes the review's single most dangerous **Silent loss** item (RISK-9, EC-50): a backup that fails silently is invisible until the day it matters. Visible status with a real timestamp converts it into something someone notices. EC-53 (power cut) is bounded by the F-13 autosave window; EC-57 (storage full) surfaces here and a failing write fails loudly via the F-1 error pipeline.

**6. Test strategy.**
- Backend unit: `PMS.Infrastructure.Tests/BackupStatusProbeTests.cs` — a last success 30 hours ago yields `Stale`; a failed attempt yields `Failed`; no backup history yields `Unknown`, never `Ok` (**EC-50**).
- Backend integration: `PMS.Api.IntegrationTests/Controllers/BackupStatusControllerTests.cs` — endpoint returns a populated DTO against LocalDB with a seeded `backupset` fixture.
- Frontend unit: `backup-status-card.component.spec.ts` — each state renders its colour and always renders the timestamp.
- E2E: `frontend/e2e/backup-status.spec.ts` — **EC-50**: with a stubbed stale status, `/today` shows the amber warning and the last-success date.
- **Manual, pre-go-live:** a full restore rehearsal from a real backup into a scratch database, timed and recorded in the runbook (**EC-54**). This is a release-gate checklist item, not an automated test.

**7. Acceptance criteria.**
- [ ] `/today` always shows the last successful backup timestamp, not merely a status icon (EC-50).
- [ ] A backup older than 26 hours renders as Stale in amber; a failed attempt renders red.
- [ ] With no backup history at all, the state is `Unknown` and never `Ok` (EC-50).
- [ ] Free disk space below the configured threshold raises the card to red (EC-57).
- [ ] `backend/deploy/sql/backup-jobs.sql` is in source control and creates the nightly full + 30-minute log jobs when run in SSMS.
- [ ] A restore rehearsal has been performed, timed, and signed off in `backend/deploy/RESTORE-RUNBOOK.md` before go-live (EC-54).
- [ ] The stated recovery objective (24 h database / 5 s typing) appears in the runbook, replacing "No data loss" (B-3).

**8. Effort & dependencies.** **M (L while OQ-3 open).** Depends on F-1, F-2. Depended on by nothing; **go-live gate**.

---

### F-22 — Retention & deletion policy enforcement

**1. Readiness — Blocked — needs decision first.**

**What must be resolved:** **OQ-8 — "What retention period applies to patient records, and is deletion ever permitted?"** (coverage row **C-33**; **RISK-13**; **EC-73**). The brainstorm doc states plainly: *"The BRD is silent and I will not invent a jurisdiction."* Right-to-be-forgotten versus a mandatory medical-record retention period is a legal question whose answer is jurisdiction-specific and may need external advice — the two possible answers ("never delete, retain N years" versus "delete on request after N years") produce opposite implementations.

**Why no concrete steps:** the entity, the job and the UI all depend on the answer. A retention model needs a purge job, a retention clock on `Patient`/`Visit`, and a legal-hold flag; a delete-on-request model needs an irreversible anonymisation path that must *not* orphan visits — the exact hole F-10 was built to close. Naming file targets before the policy exists would be guessing at a legal outcome.

**Where it must be resolved:** BRD — there is currently **no retention or deletion section at all**; one must be added. Then **OQ-8** in `doc/brainstorm-pms-verification.md` §12.

**Interim while unresolved:** **F-10's archive-not-delete stands as the stated safe default** (EC-37, EC-73), and it holds indefinitely without schema change. Nothing downstream is blocked; when OQ-8 lands, F-22 is an additive job plus a policy screen.

**Effort & dependencies.** **L** (Blocked; may require legal input before design can begin). Depends on F-10 and on OQ-8. Blocks nothing in the build.

---

## 7. Not planned in Phase 1

The BRD's *Out of Scope* list and the brainstorm's parking lot (§11) get no feature sections. One-line architectural note only: nothing in this plan forecloses them. Multi-user (**P-4**) is not foreclosed because Identity is already a real user table rather than a hardcoded credential, and every audit row carries a timestamp that a `UserId` column can join to later. Duplicate merge (**P-1**) is not foreclosed because `ArchivedIntoPatientId` already records the survivor pointer that a merge tool would consume. Vitals trend charting (**P-13**) is not foreclosed because vitals are stored as structured numerics, not free text. Structured diagnosis coding (**P-5**) is not foreclosed because `DiagnosisText` can gain a sibling code column additively. Pagination (**P-12**) is not built (EC-18, `accepted`) but every list endpoint returns a DTO array behind a service method that can become paged without a contract break.

---

## 8. Cross-cutting concerns

| Concern | Approach | Where it lands |
|---|---|---|
| **Auth** | Cookie-based ASP.NET Core Identity, `HttpOnly`/`Secure`/`SameSite=Strict`, single seeded user, 5-attempt lockout, app-level auto-lock (F-3). Explicitly **not JWT** — see §2. Ratification pending (§9) | F-3; guards in `core/auth.guard.ts` |
| **Credential recovery** | **Unresolved — F-4 Blocked (OQ-6).** Go-live gate | F-4 |
| **Audit** | Append-only `AuditEvent`, written in the same transaction as the audited action, 12 action types (F-5) | F-5, consumed by F-9, F-10, F-12, F-13, F-16, F-17, F-18, F-20 |
| **Backup & recovery** | Nightly full + 30-minute log backups as SQL Agent jobs scripted in `backend/deploy/sql/backup-jobs.sql`; 30-day retention; **rehearsed restore before go-live**; visible status card with a real timestamp (F-21). RPO: 24 h database + ≤ 5 s typing (Assumption, OQ-3) | F-21 |
| **Error handling** | One `ExceptionHandlingMiddleware`; all failures return RFC 7807 `ProblemDetails`; the frontend `error.interceptor.ts` renders a toast. **A failed write is never rendered as success** (EC-51) | F-1, F-13 |
| **Encryption** | In transit: HTTPS + HSTS, `Encrypt=True` on the SQL connection. At rest: SQL Server **TDE** on the `PMS` database plus BitLocker on the host volume. Backup files inherit TDE encryption — verify the certificate is backed up separately, or the backups are unrestorable | Deployment checklist; F-21 |
| **Access control** | Single authenticated role; every controller carries `[Authorize]` by default via a global filter, with `[AllowAnonymous]` only on `/api/health` and `/api/auth/login`. `Cache-Control: no-store` on every patient-data response (EC-71) | F-1, F-3 |
| **Input normalisation & output escaping (R-21)** | Trim + collapse whitespace, NFC-normalise, straighten smart quotes on every text save; strip formatting on paste; escape all free text on output **including the print view** (EC-61, EC-64, EC-65) | F-7, F-15, F-17 |
| **Time & timezone (R-22)** | `DateTimeOffset` everywhere; one clinic timezone from `ClinicProfile.TimeZoneId`; `Visit.ClinicDate` fixed at draft creation; **never render browser-local time** (EC-47, EC-48) | F-1, F-11, F-13 |
| **CSV export** | `PMS.Infrastructure/Export/CsvWriter.cs` — RFC 4180 quoting plus formula-prefix neutralisation. Non-negotiable per D-6 | F-20 |
| **PDF export & print** | QuestPDF server-side for downloads; the Angular print view renders the same snapshot for browser printing. Snapshot written **before** rendering (EC-56); multi-page rules per R-16 | F-17, F-20 |
| **Performance (BRD NFR)** | Type-ahead < 300 ms (indexes + debounce + request cancellation, F-8); history load < 2 s (composite index, F-19); route-level lazy `loadComponent` and an initial render < 2 s (F-2). The BRD's 2–5 s figure is retired in favour of the tighter number per C-9 | F-2, F-8, F-19 |
| **Keyboard-first (B-1)** | Global shortcut service, explicit tab order, `Enter`-to-add-medication; an E2E spec completes a whole consultation without a mouse | F-2, F-15 |
| **Configuration** | Connection string and Identity keys via user-secrets locally and environment variables in the clinic; `appsettings.json` holds only non-secrets (autosave interval, idle-lock minutes, backup staleness threshold, free-space threshold) | F-1, F-3, F-13, F-21 |

---

## 9. Test strategy summary

**Tooling, stated once:**

| Layer | Tool | Location |
|---|---|---|
| Backend unit | **xUnit + NSubstitute** | `backend/tests/PMS.Application.Tests/` |
| Backend infrastructure unit | xUnit | `backend/tests/PMS.Infrastructure.Tests/` |
| Backend integration | **xUnit + `WebApplicationFactory<Program>` + SQL Server LocalDB**, database reset between tests with Respawn | `backend/tests/PMS.Api.IntegrationTests/` |
| Frontend unit | **Jasmine + Karma** (Angular CLI default — no bespoke runner) | co-located `*.spec.ts` |
| End-to-end | **Playwright** (TypeScript), Chromium + Firefox + WebKit projects | `frontend/e2e/` |
| Print verification | Playwright `page.pdf()` for layout assertions, **plus a manual Chrome/Edge/Safari checklist before go-live** (C-35 — WebKit is not Safari's print engine) | `frontend/e2e/prescription-print.spec.ts` + release runbook |

**Coverage map** (● planned · ○ not applicable):

| Feature | BE unit | BE integration | FE unit | E2E | Edge cases covered (EC IDs) |
|---|---|---|---|---|---|
| F-1 Skeleton | ● | ● | ● | ○ | EC-47, EC-48 |
| F-2 Shell | ○ | ○ | ● | ● | EC-2, EC-4, EC-5 |
| F-3 Auth | ● | ● | ● | ● | EC-43, EC-68, EC-70, EC-71 |
| F-4 Recovery | **Blocked** | **Blocked** | **Blocked** | **Blocked** | EC-74 |
| F-5 Audit | ● | ● | ● | ● | EC-69 |
| F-6 ClinicProfile | ● | ● | ● | ● | EC-1, EC-11 |
| F-7 Patient | ● | ● | ● | ● | EC-12, EC-15, EC-17, EC-20, EC-21, EC-22, EC-24, EC-26, EC-62, EC-63, EC-64 |
| F-8 Search | ● | ● | ● | ● | EC-7, EC-28, EC-29, EC-62, EC-63 |
| F-9 Duplicates | ● | ● | ● | ● | EC-27, EC-29, EC-64 |
| F-10 Archive | ● | ● | ● | ● | EC-30, EC-37 |
| F-11 Appointments | ● | ● | ● | ● | EC-4, EC-31, EC-40, EC-48 |
| F-12 Status machine | ● | ● | ● | ● | EC-35, EC-36 |
| F-13 Draft lifecycle | ● | ● | ● | ● | EC-32, EC-33, EC-39, EC-41, EC-42, EC-45, EC-46, EC-47, EC-51, EC-53, EC-58 |
| F-14 Vitals | ● | ● | ● | ● | EC-13, EC-19, EC-66 |
| F-15 Content | ● | ● | ● | ● | EC-3, EC-8, EC-10, EC-14, EC-16, EC-23, EC-65 |
| F-16 Finalize | ● | ● | ● | ● | EC-19, EC-38, EC-44, EC-58 |
| F-17 Prescription | ● | ● | ● | ● | EC-3, EC-9, EC-10, EC-11, EC-34, EC-52, EC-55, EC-56, EC-61, EC-62 |
| F-18 Amendments | ● | ● | ● | ● | EC-34 |
| F-19 History | ● | ● | ● | ● | EC-5, EC-25, EC-33, EC-39 |
| F-20 Export | ● | ● | ● | ● | EC-6, EC-55, EC-59, EC-60, EC-67 |
| F-21 Backup | ● | ● | ● | ● + manual restore | EC-50, EC-54, EC-57 |
| F-22 Retention | **Blocked** | **Blocked** | **Blocked** | **Blocked** | EC-73 |

**Visible gaps in this map, stated rather than hidden:** F-4 and F-22 have no tests because they have no design. EC-49 (same doctor on two devices) is covered only indirectly by F-13's server-authoritative drafts and has no dedicated spec. EC-18, EC-40, EC-72 and EC-75 are `accepted` in the brainstorm doc and are deliberately untested.

---

## 10. Open items

Every `Blocked` and every `Assumption:` from §6, in one place — the single home for these, mirroring the brainstorm doc's parking-lot pattern.

| # | Item | Type | Feature | OQ | Default being built against | Consequence if the owner answers differently |
|---|---|---|---|---|---|---|
| 1 | Credential recovery path for the single user | **Blocked** | F-4 | OQ-6 | None — no steps written | Design + build once decided. **Go-live gate** (RISK-8) |
| 2 | Retention period & deletion policy | **Blocked** | F-22 | OQ-8 | None — archive-not-delete stands as interim | Additive job + policy screen; no schema change (RISK-13) |
| 3 | Vitals exception path | Assumption | F-14 | OQ-1 | D-7 C — value **or** doctor-defined "not recorded" reason | A hard block reinstates the fabrication vector (RISK-3) |
| 4 | Edit after print / amendment visibility | Assumption | F-18 | OQ-2 | D-1 D — append-only dated amendments | Option C (silent overwrite) must be recorded as an accepted risk with no audit answer |
| 5 | Acceptable loss window / recovery objective | Assumption | F-13, F-21 | OQ-3 | 5 s autosave debounce; RPO 24 h DB + 5 s typing; 30-day retention | Configuration constant + log-backup interval change only |
| 6 | Prescription header/footer content | Assumption | F-6, F-17 | OQ-5 | §7.1 field set; clinic name + doctor name mandatory | Additive migration + one print-layout slot |
| 7 | DOB vs. age | Assumption | F-7 | OQ-7 | D-3 C — optional DOB + age with `AgeCapturedOn` | Mandatory DOB would make walk-in registration harder and invites invented dates |
| 8 | Appointment scheduling model | Assumption | F-11 | OQ-9 | Simple dated list with optional time; overlaps allowed, same-day warned | **A slot calendar is a different, larger build — re-plan F-11 rather than stretch it** |
| 9 | Export scope | Assumption | F-20 | OQ-10 | D-6 B+C+F — current view only, confirmed, audited | A full-database export carries the D-6 A privacy consequence explicitly |
| 10 | Shared PC vs. private device | Assumption | F-3 | OQ-11 | Shared clinic PC (stricter): 10-minute auto-lock, no autofill, no-store | Private device relaxes the idle timer to 60 minutes; nothing else changes |
| 11 | Audit trail scope | Assumption | F-5 | OQ-12 | R-12's 12 minimal actions | Declining audit costs F-18 its trail; record as an accepted risk (RISK-12) |
| 12 | Required medication fields / diagnosis mandatory | Assumption | F-15 | OQ-13 | Drug name required; diagnosis warn-not-block (EC-8, EC-23) | Making diagnosis mandatory is a clinical rule and is the doctor's call to set |
| 13 | Gender value list | Assumption | F-7 | OQ-14 | Configurable lookup incl. "Unspecified" | Seed-data change only, no schema change |
| 14 | "Recent patients" meaning | Assumption | F-8 | OQ-16 | Last 10 **consulted** | "Recently viewed" needs a new tracking table — **confirm this one early** |
| 15 | Auth mechanism (cookie vs. JWT) | **New decision, needs ratification** | F-3, §2 | — (not in the BRD or brainstorm doc) | Cookie-based Identity, `HttpOnly`/`Secure`/`SameSite=Strict` | Reasoned in §2 against EC-68/EC-70/EC-71; raised here rather than defaulted silently because it touches security |
| 16 | Paper-usage baseline (BRD success criterion) | Owner item | — | OQ-15 | Not built against; no feature depends on it | Per B-5, restate as "zero handwritten records after go-live" or drop it |
| 17 | Duplicate merge in Phase 1 | Deferred | — | OQ-17 | Parking lot **P-1**; archive + pointer is the interim (EC-30) | If pulled forward, it consumes `ArchivedIntoPatientId`, already in the schema |

---

## 11. Recommended build sequence (one developer)

1. **Hold the OQ meeting first** — OQ-1 through OQ-8, plus OQ-16 (it has a schema consequence). One hour clears six Blockers and eleven `Needs decision` tags.
2. F-1 → F-2 → F-3 (skeleton, shell, auth).
3. F-5 → F-6 (audit and clinic profile — both are dependencies of the consultation).
4. F-7 → F-8 → F-9 → F-10 (the patient aggregate, complete with its duplicate and lifecycle guards).
5. **F-13** (the critical path's longest link; start it the moment F-7 lands, before appointments).
6. F-14 → F-15 → F-16 (the consultation content and its commit point).
7. F-17 → F-18 → F-19 (prescription, amendments, history).
8. F-11 → F-12 (appointments; deliberately late because OQ-9 has no converged option and the consultation flow does not depend on it — D-5 C makes the appointment link optional).
9. F-20 → F-21 (export, backup).
10. **Release gates before go-live:** F-4 resolved (OQ-6), F-22 policy stated (OQ-8), restore rehearsal signed off (EC-54), print verified on Chrome/Edge/Safari (C-35), TDE and its certificate backup verified.
