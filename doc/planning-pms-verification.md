# Patient Management Application — Phase 1 Implementation Plan

- **Plans:** `BRD/Doc_BRD.md` (what to build)
- **Grounded in:** `doc/brainstorm-pms-verification.md`, refresh dated 2026-08-20 (what can go wrong, and what converged). IDs below are **that file's current IDs** — coverage `C-1..C-49`, edge cases `E-1..E-66`, risks `RSK-1..RSK-15`, recommendations `REC-1..REC-19`, open questions `Q-1..Q-16`, and the consultation-capture options `A..H` from §6/§7. No ID mapping from any earlier version of either document is reused here.
- **Date:** 2026-08-18
- **Scope:** Phase 1 only — one general physician, one clinic. Everything in the BRD's "Out of Scope" list (L58–69) and everything in brainstorm §11 is excluded; where the architecture must simply not foreclose a deferred item, that is one line, not a feature section.
- **Stack (fixed):** React + TypeScript (Vite) · ASP.NET Core Web API · SQL Server via SSMS · EF Core Code-First
- **Status:** Implementation plan. **Two features are `Blocked` and carry no build steps.** Fifteen carry an explicit `Assumption:` line tied to a `Q-` ID (or, where §12 has no matching question, labelled as this plan's own finding).
- **Supersedes:** the prior contents of this file in full. This is a re-derivation against the refreshed brainstorm doc, not an incremental edit.

---

## 1. Headline

**Nothing is fully unblocked except the scaffolding — and that is fine, because thirteen of the sixteen open questions are one-meeting policy calls.** F-1 (solution + app shell) is the only feature that is `Ready` with no gate at all. Fifteen features are buildable today behind a stated default, each labelled `Assumption:` with its `Q-` ID. **F-20 (backup, restore, encryption at rest) is `Blocked` on Q-1 + Q-12** and **F-21 (credential recovery / lockout) is `Blocked` on C-44, for which brainstorm §12 has no open question at all** — that omission is this plan's own finding and should be added to the §12 agenda.

**Highest-leverage next step: run the one-hour decision session on Q-1..Q-16 that brainstorm §15 recommends, and add two items to the agenda that §12 does not currently carry — credential recovery/lockout (C-44) and the appointment time model (C-24).** Q-1 alone unblocks F-20 and settles encryption, backup destination and outage behaviour; Q-2, Q-3, Q-5 and Q-6 convert the four largest gated features from `L` to `M`.

**Critical path (from §5, longest blocking chain — this, not the sum of efforts, sets the earliest finish date):**
`F-1 → F-2 → F-3 → F-5 → F-9 → F-10 → F-11 → F-13 → F-14 → F-15 → F-17` — eleven links. F-3 (ClinicProfile + first-run gate) sits early because nothing prints without it (C-32 / REC-4 / E-1) and because vitals units live there (E-24). **F-20 is not on the build path but is a hard go-live gate:** the clinic must not take real patient data without a rehearsed restore (E-50) and a defined encryption-at-rest story (C-45).

---

## 2. Architecture overview

Stated once; every feature section references this rather than re-deciding.

**Solution layout.** `backend/` holds one ASP.NET Core Web API solution of four projects — `PMS.Api` (controllers, middleware, composition root, serves the built SPA), `PMS.Application` (services, DTOs, validators, abstractions), `PMS.Infrastructure` (EF Core `PmsDbContext`, entity configurations, migrations, repositories, PDF/CSV writers), `PMS.Domain` (entities and enums, zero framework dependencies). `frontend/` holds a **React 18 + TypeScript workspace built with Vite**.

**API shape.** RESTful controllers, one per aggregate: `AuthController`, `ClinicProfileController`, `ClinicSettingsController`, `PatientsController`, `AppointmentsController`, `VisitsController`, `PrescriptionsController`, `ExportController`, `AuditController`. Controllers depend on `PMS.Application` service interfaces and **never on `PmsDbContext`**. Request/response DTOs are separate types from EF entities; **no entity crosses the wire**. Prefix `/api`, JSON everywhere, all errors as RFC-7807 `ProblemDetails` (§7).

**Data access.** EF Core Code-First. Entities in `PMS.Domain/Entities/`; `PmsDbContext` plus `IEntityTypeConfiguration<T>` classes in `PMS.Infrastructure/Persistence/`; migrations in `PMS.Infrastructure/Migrations/`. **Every schema change is a named migration**, named in the feature section that introduces it — never implicit. Services take repository / `IUnitOfWork` interfaces declared in `PMS.Application/Abstractions/`. Writes that span more than one table (finalize, amend, register-with-audit) run inside one `IUnitOfWork.SaveChangesAsync` transaction.

**Frontend structure.** Folder-per-feature under `frontend/src/features/<feature>/`: function components (`*.tsx`), one co-located API module (`<feature>Api.ts`), hooks (`use<Thing>.ts`), and `types/` holding TypeScript interfaces mirroring the API DTOs. Shared layout, UI primitives, the fetch wrapper and the error boundary live in `frontend/src/shared/`. **Server state is managed with TanStack Query (React Query) v5 throughout** — no mixed fetching strategies; plain component state is used only for uncommitted form input. Routing via **React Router v6**, with concrete paths named per feature.

**Auth — explicit decision (not defaulted silently).** **Cookie-based authentication using the ASP.NET Core cookie handler, not a JWT in `localStorage`/`sessionStorage`.** Reason: a token in web storage is readable by any script on the page and persists in the browser profile of a machine that sits in a consulting room — a direct conflict with E-62 (screen left unlocked between patients) and E-65 (browser autofill / cached form data on the clinic machine). The cookie is `HttpOnly`, `Secure`, `SameSite=Strict`, sliding within an absolute lifetime, with no persistent "remember me". **Trade-off accepted:** cookie auth wants same-origin, so `PMS.Api` serves the built React bundle from `wwwroot` in every environment and Vite's dev server proxies `/api` in development. This holds identically under all three deployment options in Q-1, so it does not pre-empt that decision.

**Print / PDF rendering — explicit decision.** The prescription is rendered **server-side to PDF (QuestPDF) in `PMS.Infrastructure/Printing/`**, and the browser prints that PDF; it is *not* an HTML `@media print` stylesheet per browser. Reason: C-47 and E-10 — Chrome, Edge and Safari differ materially in page breaks, repeated headers and font fallback, and the printed prescription is the product's main physical output. One renderer removes that whole class of defect and makes reprints byte-identical (E-52). Trade-off: one server round-trip (~200–400 ms) before the print dialog. QuestPDF licensing is an open item (§9).

**Environments.** Connection string and every secret come from configuration — `appsettings.json` for non-secret defaults, **user-secrets locally, environment variables in the deployed environment**. No connection string, password pepper or signing key is ever committed. Re-stated in F-1 and F-20.

**Effort notation used throughout.** Per the rubric, a feature resting on an unresolved `Q-` is tagged **L** regardless of build size, because its true cost includes the decision cycle. To keep the signal, the post-decision build cost is shown in parentheses: `L (M after Q-3)`. A bare `L` means genuinely over a week of engineering.

**Deviation policy.** One concretization is called out where it occurs rather than made silently: brainstorm §6.2 sketches `Complaint` and `Diagnosis` as child rows; this plan stores each as a single text column on `Visit` (they are 1:1 per visit and never repeat). Noted again in F-12. Nothing else deviates from the conventions above.

---

## 3. Solution & repo structure (as it will look at the end of Phase 1)

```
Hospital-managment/
├─ BRD/Doc_BRD.md
├─ doc/
│  ├─ brainstorm-pms-verification.md
│  └─ planning-pms-verification.md              <- this file
├─ backend/
│  ├─ PMS.sln
│  ├─ src/
│  │  ├─ PMS.Domain/
│  │  │  ├─ Entities/   AppUser.cs · ClinicProfile.cs · SettingOption.cs
│  │  │  │              VitalRangeSetting.cs · Patient.cs · Appointment.cs
│  │  │  │              Visit.cs · VisitVitals.cs · MedicationLine.cs
│  │  │  │              PrescriptionIssue.cs · VisitAmendment.cs · AuditEvent.cs
│  │  │  └─ Enums/      VisitState.cs · AppointmentStatus.cs · AppointmentSource.cs
│  │  │                 PatientStatus.cs · SettingCategory.cs · VitalMetric.cs
│  │  │                 AuditEventType.cs · TemperatureUnit.cs
│  │  ├─ PMS.Application/
│  │  │  ├─ Abstractions/  IUnitOfWork.cs · IPatientRepository.cs · IVisitRepository.cs
│  │  │  │                 IAppointmentRepository.cs · IAuditWriter.cs · IClock.cs
│  │  │  │                 IPdfRenderer.cs · ICsvWriter.cs
│  │  │  ├─ Services/      PatientService.cs · PatientDuplicateService.cs
│  │  │  │                 AppointmentService.cs · VisitService.cs · VitalsService.cs
│  │  │  │                 MedicationService.cs · PrescriptionService.cs
│  │  │  │                 AmendmentService.cs · HistoryService.cs · ExportService.cs
│  │  │  │                 ClinicProfileService.cs · ClinicSettingsService.cs · AuthService.cs
│  │  │  ├─ Dtos/          (one folder per aggregate)
│  │  │  └─ Validation/    (FluentValidation validators, one per request DTO)
│  │  ├─ PMS.Infrastructure/
│  │  │  ├─ Persistence/   PmsDbContext.cs · Configurations/*.cs · Repositories/*.cs
│  │  │  ├─ Migrations/    (named migrations, see each feature)
│  │  │  ├─ Printing/      QuestPdfPrescriptionRenderer.cs · PrescriptionDocument.cs
│  │  │  └─ Export/        Rfc4180CsvWriter.cs
│  │  └─ PMS.Api/
│  │     ├─ Controllers/   Auth · ClinicProfile · ClinicSettings · Patients
│  │     │                 Appointments · Visits · Prescriptions · Export · Audit
│  │     ├─ Middleware/    ProblemDetailsMiddleware.cs · RequestTimingMiddleware.cs
│  │     ├─ wwwroot/       (built React bundle)
│  │     ├─ Program.cs · appsettings.json · appsettings.Development.json
│  └─ tests/
│     ├─ PMS.Application.Tests/     Services/*Tests.cs
│     ├─ PMS.Api.IntegrationTests/  Endpoints/*Tests.cs · TestWebAppFactory.cs
│     └─ PMS.E2E/                   (Playwright specs, *.spec.ts)
└─ frontend/
   ├─ index.html · vite.config.ts · tsconfig.json · package.json
   └─ src/
      ├─ main.tsx · App.tsx · routes.tsx
      ├─ shared/
      │  ├─ api/         httpClient.ts · problemDetails.ts · queryClient.ts
      │  ├─ components/  AppLayout.tsx · EmptyState.tsx · SaveStateBadge.tsx
      │  │               ConfirmDialog.tsx · PatientPickerRow.tsx · ScreenLock.tsx
      │  ├─ hooks/       useIdleTimer.ts · useBeforeUnloadGuard.ts · useSubmitOnce.ts
      │  └─ types/       problemDetails.ts · paging.ts
      └─ features/
         ├─ auth/        LoginPage.tsx · authApi.ts · useSession.ts · types/
         ├─ setup/       FirstRunSetupPage.tsx · setupApi.ts · types/
         ├─ clinic/      ClinicProfilePage.tsx · ClinicSettingsPage.tsx
         │               VitalRangesPage.tsx · clinicApi.ts · useClinicProfile.ts · types/
         ├─ patients/    PatientSearch.tsx · PatientList.tsx · PatientForm.tsx
         │               PatientProfile.tsx · DuplicateWarningDialog.tsx
         │               RecentPatients.tsx · patientsApi.ts · usePatients.ts
         │               usePatientDuplicates.ts · types/
         ├─ appointments/DailyAppointmentList.tsx · AppointmentForm.tsx
         │               AppointmentStatusMenu.tsx · appointmentsApi.ts
         │               useAppointments.ts · types/
         ├─ visits/      ConsultationPage.tsx · VitalsSection.tsx · ComplaintSection.tsx
         │               DiagnosisSection.tsx · MedicationSection.tsx
         │               FinalizeDialog.tsx · AmendmentPanel.tsx · DraftBanner.tsx
         │               visitsApi.ts · useVisitDraft.ts · useAutosave.ts
         │               useVisitLock.ts · types/
         ├─ prescriptions/PrescriptionPreview.tsx · prescriptionsApi.ts
         │               usePrescription.ts · types/
         ├─ history/     PatientHistory.tsx · HistoryDateFilter.tsx · VisitDetail.tsx
         │               historyApi.ts · usePatientHistory.ts · types/
         ├─ export/      ExportPage.tsx · exportApi.ts · useExport.ts · types/
         └─ audit/       AuditLogPage.tsx · auditApi.ts · useAuditLog.ts · types/
```

---

## 4. Data model overview (Phase 1)

Concretizes brainstorm §6.2 to real EF types. No DDL, indexes or constraint tuning here beyond the constraints that are load-bearing for a decision — that is migration review, not planning.

| Entity | Key properties (name : type) | Relationships |
|---|---|---|
| `AppUser` | `Id:Guid` · `UserName:string` · `PasswordHash:string` · `SecurityStamp:string` · `FailedAttempts:int` · `LockoutEndUtc:DateTimeOffset?` · `LastLoginUtc:DateTimeOffset?` | standalone (exactly one row) |
| `ClinicProfile` | `Id:int` (singleton, always 1) · `ClinicName:string` · `AddressLines:string` · `DoctorName:string` · `DoctorRegistrationNo:string` · `SignatureImage:byte[]?` · `PrescriptionFooter:string?` · `TemperatureUnit:TemperatureUnit` · `IsSetupComplete:bool` · `UpdatedUtc:DateTimeOffset` | referenced by prescription rendering |
| `SettingOption` | `Id:int` · `Category:SettingCategory` (`Gender`, `VitalsNotRecordedReason`) · `Value:string` · `DisplayOrder:int` · `IsActive:bool` | doctor-configured lookup; **values are data, never hardcoded logic** |
| `VitalRangeSetting` | `Id:int` · `Metric:VitalMetric` · `WarnLow:decimal?` · `WarnHigh:decimal?` · `UpdatedUtc:DateTimeOffset` | doctor-defined thresholds (E-12); the system enforces only what it is given |
| `Patient` | `Id:Guid` · `FullName:string(200)` · `NormalizedName:string` · `DateOfBirth:DateOnly?` · `ApproxAgeYears:int?` · `AgeRecordedOn:DateOnly?` · `Gender:string?` · `PrimaryPhone:string?` · `NormalizedPhone:string?` · `AltContact:string?` · `RegisteredUtc:DateTimeOffset` · `Status:PatientStatus` · `InactiveReason:string?` · `MergedIntoPatientId:Guid?` · `RowVersion:byte[]` | `1..* Appointment`, `1..* Visit`; self-reference `MergedIntoPatientId → Patient.Id` |
| `Appointment` | `Id:Guid` · `PatientId:Guid` · `ScheduledForUtc:DateTimeOffset` · `DurationMinutes:int` · `Status:AppointmentStatus` · `Source:AppointmentSource` (`Booked`/`WalkIn`) · `CreatedUtc` · `RowVersion:byte[]` | `Patient 1..*`; `0..1 Visit` |
| `Visit` | `Id:Guid` · `PatientId:Guid` · `AppointmentId:Guid?` · `State:VisitState` (`Draft`/`Finalized`) · `StartedUtc:DateTimeOffset` · `VisitDate:DateOnly` (fixed at draft creation, never recomputed — E-44) · `FinalizedUtc:DateTimeOffset?` · `ComplaintText:string?` · `DiagnosisText:string?` · `LockToken:Guid?` · `LockHeartbeatUtc:DateTimeOffset?` · `RowVersion:byte[]` | `Patient 1..*`; `0..1 Appointment`; owns `VisitVitals`, `MedicationLine`, `PrescriptionIssue`, `VisitAmendment` |
| `VisitVitals` | `VisitId:Guid` (PK+FK, 1:1) · `TemperatureValue:decimal?` · `TemperatureNotRecordedReason:string?` · `BpSystolic:int?` · `BpDiastolic:int?` · `BpNotRecordedReason:string?` · `PulseBpm:int?` · `PulseNotRecordedReason:string?` · `RecordedUtc` | `Visit 1:1`. **Absent is `null` + a reason — never a sentinel number** (E-18) |
| `MedicationLine` | `Id:Guid` · `VisitId:Guid` · `LineNo:int` · `Name:string` · `Dosage:string` · `Frequency:string?` · `Duration:string?` · `Instructions:string?` | `Visit 1..*`; zero rows is legal (E-5) |
| `PrescriptionIssue` | `Id:Guid` · `VisitId:Guid` · `PrescriptionNumber:string` (unique) · `IssuedUtc:DateTimeOffset` · `PrintCount:int` · `LastPrintedUtc:DateTimeOffset?` | `Visit 1:1`, created at finalize |
| `VisitAmendment` | `Id:Guid` · `VisitId:Guid` · `Sequence:int` · `Text:string` · `CreatedUtc:DateTimeOffset` | `Visit 1..*`, **append-only — no update, no delete** (E-32) |
| `AuditEvent` | `Id:long` · `EventType:AuditEventType` · `EntityType:string` · `EntityId:string` · `OccurredUtc:DateTimeOffset` · `Summary:string` · `PayloadJson:string?` | append-only; six event types (REC-9, §5.7) |

**Load-bearing constraints (the only ones stated at plan level):** `PrescriptionIssue.PrescriptionNumber` unique; `VisitAmendment` and `AuditEvent` have no update/delete path exposed by any service; `Patient` has **no hard-delete endpoint** while any `Visit` references it (E-33); `Visit.VisitDate` is written once at draft creation; a finalized `Visit` rejects any mutation of its clinical columns at the service layer (E-32).

**Not foreclosed but not built:** `MergedIntoPatientId` exists so Phase-2 merge tooling (§11) is possible without destroying history; nothing else in the parking lot is modelled.

---

## 5. Dependency map (build order)

A `Blocked` row blocks everything downstream of it in this table. Nothing is downstream of F-20 or F-21 in build terms — both are **go-live gates**, which is why they sit at the end and must not be treated as optional.

| ID | Feature | Depends on | Effort | Readiness |
|---|---|---|---|---|
| F-1 | Solution scaffolding, app shell, health check, error contract | — | M | **Ready** |
| F-2 | Login, session policy, idle screen lock | F-1 | L (M after C-44 session call) | Needs decision (C-44, REC-11 — no `Q-` exists) |
| F-3 | ClinicProfile + first-run setup gate | F-1, F-2 | L (S after Q-4) | Needs decision (Q-4) |
| F-4 | Doctor-configured settings: gender list, vitals reasons, plausibility ranges | F-3 | L (S after Q-9, Q-10) | Needs decision (Q-9, Q-10) |
| F-5 | Patient registration & profile | F-1, F-2, F-4 | L (M after Q-7, Q-16, Q-9) | Needs decision (Q-7, Q-16, Q-9) |
| F-6 | Duplicate detection at registration + `merged_into` pointer | F-5 | L (M after Q-13) | Needs decision (Q-13) |
| F-7 | Patient search, recent patients, disambiguating picker | F-5 | L (M after search-semantics call) | Needs decision (C-22 — no `Q-` exists) |
| F-8 | Patient edit + deactivate (no hard delete) | F-5, F-17 | L (M after Q-6) | Needs decision (Q-6) |
| F-9 | Appointments: scheduling, daily list, status machine, walk-in start | F-5, F-7 | L (M after Q-5, Q-14) | Needs decision (Q-5, Q-14) |
| F-10 | Visit lifecycle: draft, autosave, save-state, resume, single-tab lock, finalize | F-9 | L (L after Q-3) | Needs decision (Q-3) |
| F-11 | Vitals capture — mandatory-or-reason | F-10, F-4 | L (M after Q-2, Q-10) | Needs decision (Q-2, Q-10) |
| F-12 | Complaints & diagnosis capture | F-10 | L (S after Q-8) | Needs decision (Q-8) |
| F-13 | Medications | F-10 | L (M after medication-required-subset call) | Needs decision (C-31/E-22 — no `Q-` exists) |
| F-14 | Prescription generation, print, reprint | F-3, F-11, F-12, F-13 | L (M after Q-4, Q-8) | Needs decision (Q-4, Q-8) |
| F-15 | Visit amendments (append-only) | F-14 | L (M after Q-3) | Needs decision (Q-3) |
| F-16 | Patient history + date filter | F-10, F-14, F-15 | M | **Ready** (build gated only by upstream) |
| F-17 | Audit trail (six event types) | F-1 | L (M after audit acceptance) | Needs decision (REC-9/C-48 — no `Q-` exists) |
| F-18 | Export CSV / PDF | F-14, F-16, F-17 | L (M after Q-11) | Needs decision (Q-11) |
| F-19 | Keyboard-first input + performance instrumentation | F-10, F-11, F-12, F-13 | L (S after Q-15) | Needs decision (Q-15) |
| F-20 | Backup, restore rehearsal, backup-status indicator, encryption at rest | F-1 | **L** | **Blocked** (Q-1, Q-12) |
| F-21 | Credential recovery, lockout policy | F-2 | **L** | **Blocked** (C-44 — brainstorm §12 has no question for this) |

**Critical path:** `F-1 → F-2 → F-3 → F-5 → F-9 → F-10 → F-11 → F-13 → F-14 → F-15 → F-17`. F-4, F-6, F-7, F-8, F-12, F-17 and F-19 run parallel to it. F-16 and F-18 tail the path. F-20 and F-21 are off-path but gate go-live.

---

## 6. Feature plans

---

### F-1 — Solution scaffolding, app shell, error contract

**1. Readiness.** **Ready.** No open question touches it; the stack is fixed and the conventions are in §2.

**2. Data model.** `PmsDbContext` created with no entity sets beyond `AppUser` (F-2 populates it). Migration: **`InitialCreate`**. Connection string read from `ConnectionStrings:Pms` via configuration only (§2, Environments) — SSMS is used to create the empty `PmsDb` database; `dotnet ef database update` builds the schema.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| GET | `/api/health` | — | `HealthResponse` | 200 | anonymous |
| GET | `/api/health/db` | — | `HealthResponse` | 200, 503 | anonymous |

**4. Frontend design.** `frontend/src/main.tsx`, `App.tsx`, `routes.tsx` (React Router v6 route table), `shared/api/httpClient.ts` (`request<T>(path, init): Promise<T>`, throws a typed `ProblemDetailsError`), `shared/api/queryClient.ts` (TanStack Query client, `retry: 1`, `refetchOnWindowFocus: false`), `shared/components/AppLayout.tsx`, `shared/components/EmptyState.tsx`, `shared/types/problemDetails.ts`. Routes registered as placeholders: `/login`, `/setup`, `/`, `/patients`, `/patients/:id`, `/visits/:id`, `/settings/clinic`, `/export`, `/audit`.

**5. Data integrity check.** No user data path yet. The contract established here — every failed write surfaces as a typed error the UI must render, never a swallowed promise — is what makes E-47 ("doctor believes it saved") preventable in every later feature.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/ClockTests.cs` (deterministic `IClock`, used by every later service test).
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/HealthEndpointTests.cs` using `TestWebAppFactory : WebApplicationFactory<Program>` against LocalDB.
- Frontend unit: `frontend/src/shared/api/httpClient.test.ts` (Vitest) — ProblemDetails parsing, non-JSON error bodies.
- E2E: Playwright smoke spec `PMS.E2E/app-shell.spec.ts` — app loads, unauthenticated user is redirected to `/login`.

**7. Acceptance criteria.**
- [ ] `dotnet build backend/PMS.sln` succeeds with four projects and three test projects.
- [ ] `dotnet ef migrations add InitialCreate -p PMS.Infrastructure -s PMS.Api` produces a migration; `database update` creates `PMSDb` visible in SSMS.
- [ ] `GET /api/health/db` returns 200 with a live SQL Server and 503 with the connection string removed.
- [ ] No connection string, password or key appears in any committed file; `dotnet user-secrets list` supplies it locally.
- [ ] `npm run build` in `frontend/` emits to `backend/src/PMS.Api/wwwroot`, and browsing the API root serves the SPA (same-origin requirement of the §2 auth decision).

**8. Effort & dependencies.** **M.** Depends on nothing. **Blocks every other feature.**

---

### F-2 — Login, session policy, idle screen lock

**1. Readiness.** **Needs decision.**
> **Assumption:** session lifetime and password policy follow REC-11 — **idle screen lock at 5 minutes (blurs PHI, preserves the in-progress draft in memory and on the server), session absolute expiry at 12 hours, sliding renewal on activity, and re-authentication in place that never discards a draft** (E-41, E-62). Password minimum 12 characters, no forced rotation. This corresponds to **C-44 / REC-11**; brainstorm §12 carries **no `Q-` for it**, which is this plan's own finding — see §9. Lockout and credential *recovery* are split out to F-21 and are **Blocked**, because with exactly one user there is nobody to perform a reset and the brainstorm records no owner decision.

**2. Data model.** `AppUser` (see §4). Migration: **`AddAppUser`**. Seeded by a one-time `PMS.Api` startup task that reads the initial credential from configuration (user-secrets / environment variable) and refuses to run twice.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| POST | `/api/auth/login` | `LoginRequest{UserName,Password}` | `SessionResponse{userName,expiresUtc,setupComplete}` | 200, 400, 401 | anonymous |
| POST | `/api/auth/logout` | — | — | 204 | cookie |
| GET | `/api/auth/session` | — | `SessionResponse` | 200, 401 | cookie |
| POST | `/api/auth/reauth` | `LoginRequest` | `SessionResponse` | 200, 401 | expired-or-valid cookie |

**4. Frontend design.** `features/auth/LoginPage.tsx` (route `/login`), `features/auth/authApi.ts` (`login(req): Promise<Session>`, `logout(): Promise<void>`, `getSession(): Promise<Session>`, `reauth(req): Promise<Session>`), `features/auth/useSession.ts` (TanStack Query, `staleTime: 60_000`), `shared/components/ScreenLock.tsx` (overlay driven by `shared/hooks/useIdleTimer.ts(idleMs)`), `shared/components/RequireAuth.tsx` route guard. `POST /api/auth/reauth` is called from the lock overlay — **the consultation page beneath it is never unmounted** (E-41). `autoComplete="off"` on all patient-data forms is set here as a shared form convention (E-65).

**5. Data integrity check.** Silent-loss risk: session expiry mid-consultation. Prevented by in-place re-auth over a preserved draft (E-41) plus F-10's server-side autosave, so nothing depends on the browser holding the only copy. Screen lock blurs but does not unmount, so no state is discarded (E-62).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/AuthServiceTests.cs` — hash verification, wrong password, expiry calculation.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/AuthEndpointTests.cs` — login sets an `HttpOnly`/`Secure`/`SameSite=Strict` cookie; protected endpoint returns 401 without it; `reauth` returns a fresh cookie.
- Frontend unit: `features/auth/LoginPage.test.tsx`, `shared/hooks/useIdleTimer.test.ts`.
- E2E: `PMS.E2E/auth.spec.ts` — golden path login/logout; **severe edge case E-41**: idle past the lock timeout while a draft consultation is open, re-authenticate, and confirm the typed text is still on screen and still on the server.

**7. Acceptance criteria.**
- [ ] Login with correct credentials sets a cookie flagged `HttpOnly`, `Secure`, `SameSite=Strict`; no token is written to `localStorage` or `sessionStorage` (assert in the E2E spec).
- [ ] Any `/api/*` route other than `health` and `auth/login` returns 401 without a valid cookie.
- [ ] After 5 minutes idle the screen-lock overlay covers all PHI; the underlying route is still mounted (E-62).
- [ ] Re-authenticating from the overlay restores the exact view, and a draft open before the lock retains every typed character (E-41).
- [ ] Patient-data inputs render with `autocomplete="off"` (E-65).

**8. Effort & dependencies.** **L (M after the C-44 session call).** Depends on F-1. Blocks F-3, F-5, F-21 and effectively every authenticated feature.

---

### F-3 — ClinicProfile + first-run setup gate

**1. Readiness.** **Needs decision.**
> **Assumption:** the entity and the gate are settled by **REC-4** (Tier 1, converged) and are built as specified; what remains open is the *content* — **Q-4** (header/footer text, registration number, who supplies the signature image). Building against: fields exactly as in §4, signature stored as an uploaded PNG ≤ 200 KB in `ClinicProfile.SignatureImage`, footer free text ≤ 500 characters, and **no prescription can be printed until `IsSetupComplete` is true** (E-1). If the doctor supplies no signature image, the printed footer shows a ruled signature area instead — never a broken-image placeholder.

**2. Data model.** `ClinicProfile` singleton (§4). Migration: **`AddClinicProfile`**. `IsSetupComplete` is set by the service only when `ClinicName`, `DoctorName` and `DoctorRegistrationNo` are all non-empty and `TemperatureUnit` is chosen (E-24).

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| GET | `/api/clinic-profile` | — | `ClinicProfileResponse` | 200, 404 | cookie |
| PUT | `/api/clinic-profile` | `UpsertClinicProfileRequest` | `ClinicProfileResponse` | 200, 400 | cookie |
| POST | `/api/clinic-profile/signature` | multipart `file` | `ClinicProfileResponse` | 200, 400, 413 | cookie |
| DELETE | `/api/clinic-profile/signature` | — | `ClinicProfileResponse` | 200 | cookie |

**4. Frontend design.** `features/setup/FirstRunSetupPage.tsx` (route `/setup`), `features/clinic/ClinicProfilePage.tsx` (route `/settings/clinic`) — both render the same `ClinicProfileForm.tsx`. `features/clinic/clinicApi.ts` (`getProfile(): Promise<ClinicProfile>`, `saveProfile(req): Promise<ClinicProfile>`, `uploadSignature(file): Promise<ClinicProfile>`), `features/clinic/useClinicProfile.ts`. A router-level guard in `routes.tsx` redirects to `/setup` whenever `session.setupComplete === false` (E-1).

**5. Data integrity check.** No patient data. The integrity risk it removes is E-1 — a prescription printed with no clinic identity, which is an unusable clinical document. The gate makes that state unreachable rather than merely unlikely.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/ClinicProfileServiceTests.cs` — `IsSetupComplete` transitions, oversize-signature rejection.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/ClinicProfileEndpointTests.cs` — PUT then GET round-trip; signature upload persists bytes.
- Frontend unit: `features/clinic/ClinicProfilePage.test.tsx` — required-field validation, unit selector.
- E2E: `PMS.E2E/first-run.spec.ts` — **E-1**: a fresh database routes to `/setup`, and the prescription action is unreachable until the profile is saved.

**7. Acceptance criteria.**
- [ ] With an empty `ClinicProfile` table, every authenticated route redirects to `/setup`.
- [ ] Saving clinic name, doctor name, registration number and temperature unit sets `IsSetupComplete = true` and lifts the redirect.
- [ ] `POST /api/prescriptions/...` returns 409 with a `ProblemDetails` naming setup as incomplete while `IsSetupComplete` is false (E-1).
- [ ] An uploaded signature renders in the prescription preview; with no signature, a ruled signature area renders instead.
- [ ] The chosen temperature unit is displayed alongside every stored temperature in UI and print (E-24).

**8. Effort & dependencies.** **L (S after Q-4).** Depends on F-1, F-2. **Blocks F-4 and F-14** — nothing prints until this exists (C-32 / RSK-4).

---

### F-4 — Doctor-configured settings (gender list, vitals reasons, plausibility ranges)

**1. Readiness.** **Needs decision.**
> **Assumption (Q-9):** gender options are a doctor-editable list seeded with `Female`, `Male`, `Other`, `Not stated` — the seed is data, editable in the UI, and **"Not stated" is never removable** (E-23).
> **Assumption (Q-10):** temperature unit is chosen once in `ClinicProfile` (E-24); BP is always mmHg, stored as two integers. Plausibility thresholds are **empty by default** and entered by the doctor in `VitalRangeSetting`; when a threshold is blank, no warning fires. Warnings are **soft — confirm and continue, never a hard block** (E-12). **This plan does not author any clinical range; the system enforces only what the doctor enters.**
> **Assumption (Q-2, shared with F-11):** vitals not-recorded reasons seeded as `Equipment unavailable`, `Patient declined`, `Not clinically indicated`, `Other` (§5.2), doctor-editable.

**2. Data model.** `SettingOption`, `VitalRangeSetting` (§4). Migration: **`AddClinicSettings`**, including a seed of the option lists above via `HasData`.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| GET | `/api/clinic-settings/options?category=Gender` | — | `SettingOptionResponse[]` | 200 | cookie |
| PUT | `/api/clinic-settings/options/{category}` | `SettingOptionListRequest` | `SettingOptionResponse[]` | 200, 400 | cookie |
| GET | `/api/clinic-settings/vital-ranges` | — | `VitalRangeResponse[]` | 200 | cookie |
| PUT | `/api/clinic-settings/vital-ranges` | `VitalRangeListRequest` | `VitalRangeResponse[]` | 200, 400 | cookie |

**4. Frontend design.** `features/clinic/ClinicSettingsPage.tsx` (route `/settings/options`), `features/clinic/VitalRangesPage.tsx` (route `/settings/vitals-ranges`), `features/clinic/clinicApi.ts` extended with `getOptions(category)`, `saveOptions(category, items)`, `getVitalRanges()`, `saveVitalRanges(items)`; hooks `useSettingOptions(category)` and `useVitalRanges()`.

**5. Data integrity check.** Duplicate/consistency risk: free-text gender producing `M`/`Male`/`male` in one column (C-20). Prevented by storing only values drawn from `SettingOption`. Deactivating an option is a soft flag (`IsActive=false`) so historical patient rows never lose their recorded value — no orphaned lookups.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/ClinicSettingsServiceTests.cs` — cannot delete `Not stated`; blank threshold means no warning emitted.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/ClinicSettingsEndpointTests.cs` — seed present after migration; PUT reorders and deactivates.
- Frontend unit: `features/clinic/VitalRangesPage.test.tsx` — blank threshold submits as null, not zero.
- E2E: `PMS.E2E/settings.spec.ts` — **E-12**: a doctor-set upper threshold produces a confirmable warning on the consultation page, and confirming still saves the value.

**7. Acceptance criteria.**
- [ ] The gender dropdown in F-5 renders exactly the active `SettingOption` rows, in `DisplayOrder`.
- [ ] Deactivating a gender option leaves existing patient records displaying their stored value unchanged.
- [ ] With all `VitalRangeSetting` rows blank, entering any numeric vital produces **no** warning.
- [ ] Setting a threshold then entering a value outside it produces a warning that can be confirmed and saved (E-12) — never a block.
- [ ] No range value is present in source code; all come from the database.

**8. Effort & dependencies.** **L (S after Q-9, Q-10).** Depends on F-3. Blocks F-5 (gender list) and F-11 (reasons and ranges).

---

### F-5 — Patient registration & profile

**1. Readiness.** **Needs decision.**
> **Assumption (Q-16):** age is captured as **`DateOfBirth` when known, otherwise `ApproxAgeYears` + `AgeRecordedOn`; a bare mutable age is never stored** (C-19, E-9, E-21). Display renders `~40 (recorded 2026)` for approximations, age in days under one month and months under two years (E-11), and rejects a future DOB.
> **Assumption (Q-7):** phone is **optional but prompted** — saving without one is allowed, the profile shows "No contact recorded", and the profile is flagged incomplete (E-8, E-20).
> **Assumption (Q-9):** gender values come from F-4.
Name is a **single free-text field** — a required-surname design rejects real patients (E-13, C-18).

**2. Data model.** `Patient` (§4). Migration: **`AddPatient`**. `NormalizedName` = trimmed, internal whitespace collapsed, case-folded (REC-18, E-60); `NormalizedPhone` = digits only (E-59). Both are written by `PatientService` on every save, never by the client. A DB check constraint enforces `DateOfBirth IS NOT NULL OR ApproxAgeYears IS NOT NULL OR both NULL` — a bare `ApproxAgeYears` without `AgeRecordedOn` is rejected at the service layer (this is the load-bearing rule from E-9).

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| POST | `/api/patients` | `CreatePatientRequest` | `PatientResponse` | 201, 400, 409 (duplicate-confirmation required, F-6) | cookie |
| GET | `/api/patients/{id}` | — | `PatientDetailResponse` | 200, 404 | cookie |

**4. Frontend design.** `features/patients/PatientForm.tsx` (routes `/patients/new` and, in F-8, `/patients/:id/edit`), `features/patients/PatientProfile.tsx` (route `/patients/:id`), `features/patients/patientsApi.ts` (`createPatient(req): Promise<Patient>`, `getPatient(id): Promise<PatientDetail>`), `features/patients/usePatients.ts` (`useCreatePatient()`, `usePatient(id)`). Submit uses `shared/hooks/useSubmitOnce.ts` (E-43/E-46). The profile header always shows name + phone tail + age/DOB (REC-12, feeding F-7's picker).

**5. Data integrity check.** Duplicate risk is the dominant one (C-23, E-25). This feature contributes the *normalisation* half — whitespace and phone-format variants collapse before comparison (E-60, E-59) — and F-6 adds detection. Mutable-history risk from a bare age is removed by the DOB/approx-age rule (E-9). Create is idempotent per submit token, so a double-click cannot create two patients (E-46).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/PatientServiceTests.cs` — normalisation, DOB-in-future rejection, approx-age-without-date rejection, incomplete-profile flag.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/PatientsEndpointTests.cs` — create then fetch; unicode name round-trip (E-57).
- Frontend unit: `features/patients/PatientForm.test.tsx` — mononym accepted (E-13), no-phone accepted with prompt (E-20), approx-age path.
- E2E: `PMS.E2E/patient-registration.spec.ts` — golden path; **E-8**: register with name only and confirm the incomplete flag is visible on the profile.

**7. Acceptance criteria.**
- [ ] A patient with a single-word name and no phone can be saved, and the profile shows "Profile incomplete" and "No contact recorded" (E-8, E-13, E-20).
- [ ] Entering an approximate age stores `ApproxAgeYears` + `AgeRecordedOn` and displays `~40 (recorded 2026)`; the API rejects a bare age with 400 (E-9, E-21).
- [ ] A DOB of today is accepted and the age displays in days (E-11); a future DOB is rejected with 400.
- [ ] `"  Ravi   Kumar  "` and `"Ravi Kumar"` both persist `NormalizedName = "ravi kumar"` (E-60).
- [ ] A name containing non-Latin script saves, displays and re-fetches unchanged (E-57).
- [ ] Double-clicking Save creates exactly one patient row (E-46).

**8. Effort & dependencies.** **L (M after Q-7, Q-16, Q-9).** Depends on F-1, F-2, F-4. Blocks F-6, F-7, F-8, F-9.

---

### F-6 — Duplicate detection at registration + `merged_into` pointer

**1. Readiness.** **Needs decision.**
> **Assumption (Q-13):** the identity rule is REC-2's shape — a candidate is flagged when **(normalised name similarity ≥ 0.85 by trigram/Levenshtein ratio AND same `NormalizedPhone`) OR (normalised name similarity ≥ 0.85 AND same `DateOfBirth`)**. The check **warns, never blocks** (a blocking rule is worse than a duplicate). The 0.85 threshold and the "who resolves it" answer are exactly what Q-13 must confirm; the doctor is the only user, so resolution is a confirm dialog. **Merge tooling is Phase 2** (§11) — this feature ships detection plus the non-destructive `MergedIntoPatientId` pointer only (E-26).

**2. Data model.** Adds `MergedIntoPatientId:Guid?` usage and an index on `NormalizedPhone` and `NormalizedName`. Migration: **`AddPatientDuplicateIndexes`**. No row is ever deleted or rewritten by this feature.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| POST | `/api/patients/duplicate-check` | `DuplicateCheckRequest{fullName,phone,dateOfBirth}` | `DuplicateCandidateResponse[]` | 200 | cookie |
| POST | `/api/patients?confirmDuplicate=true` | `CreatePatientRequest` | `PatientResponse` | 201 | cookie |
| POST | `/api/patients/{id}/mark-merged` | `MarkMergedRequest{mergedIntoPatientId,note}` | `PatientResponse` | 200, 400, 409 | cookie |

`POST /api/patients` (F-5) returns **409 + candidate list** when duplicates are found and `confirmDuplicate` is absent.

**4. Frontend design.** `features/patients/DuplicateWarningDialog.tsx` (rendered from `PatientForm.tsx`), `features/patients/usePatientDuplicates.ts` (`useDuplicateCheck()` — debounced 400 ms on name/phone blur), `patientsApi.ts` gains `checkDuplicates(req)` and `markMerged(id, req)`. Every candidate row uses `shared/components/PatientPickerRow.tsx` — **name + phone tail + age/DOB + last visit date, never name alone** (REC-12, E-28).

**5. Data integrity check.** This is the Duplicate-mode feature. Detection prevents most split histories (E-25, E-30); `mark-merged` writes a pointer and sets `Status=Inactive` so **both histories remain readable and neither is destroyed** (E-26, E-33). No destructive operation exists in this feature's API surface at all.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/PatientDuplicateServiceTests.cs` — name-similarity boundary either side of the threshold, shared household phone returns *all* matches (E-27), self-exclusion on edit.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/PatientDuplicateEndpointTests.cs` — 409 without confirm, 201 with confirm, `mark-merged` rejects a cycle.
- Frontend unit: `features/patients/DuplicateWarningDialog.test.tsx` — candidate rows show four disambiguating fields (E-28).
- E2E: `PMS.E2E/patient-duplicates.spec.ts` — **E-25**: register the same person twice and confirm the warning appears before the second record is created; **E-27**: two family members on one phone both appear in the candidate list.

**7. Acceptance criteria.**
- [ ] Registering a name+phone already on file returns 409 with candidates before any row is written (E-25).
- [ ] The warning is dismissible — confirming creates the patient (warn, never block).
- [ ] A phone shared by three family members returns all three as candidates; none is auto-selected (E-27).
- [ ] Every candidate row displays name, phone tail, age/DOB and last visit date (E-28, REC-12).
- [ ] `mark-merged` leaves both patients' visits queryable and deletes nothing (E-26).
- [ ] No endpoint in this feature deletes or overwrites a patient row.

**8. Effort & dependencies.** **L (M after Q-13).** Depends on F-5. Blocks nothing structurally, but its absence raises RSK-2 for every downstream clinical record.

---

### F-7 — Patient search, recent patients, disambiguating picker

**1. Readiness.** **Needs decision.**
> **Assumption:** search semantics per C-22/C-35, for which **brainstorm §12 carries no `Q-`** (this plan's own finding — see §9; it is adjacent to Q-13). Building against: **case-insensitive substring match on `NormalizedName`, plus digits-only match on `NormalizedPhone` including a last-4-digits suffix match**; minimum query length 2; 300 ms debounce; results ranked exact-prefix → prefix → substring → phone; **inactive and merged patients are excluded by default with an "include inactive" toggle**. Fuzzy name matching (the real fix for E-30) uses the same similarity function as F-6 and is applied only when the exact-match set is empty.
> **NFR:** `p95 ≤ 2 s from keystroke to rendered result at 5,000 patients / 25,000 visits` (C-12, REC-19) — met by the `NormalizedName`/`NormalizedPhone` indexes from F-6, server-side `TOP 20`, and no client-side filtering.

**2. Data model.** No new entities. Reuses the F-6 indexes; adds `LastVisitDate` as a projected read (computed in the query, not stored) for picker rows.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| GET | `/api/patients/search?query=&includeInactive=&take=` | — | `PatientSummaryResponse[]` | 200, 400 | cookie |
| GET | `/api/patients/recent?take=10` | — | `PatientSummaryResponse[]` | 200 | cookie |

`PatientSummaryResponse` always carries `fullName`, `phoneTail`, `ageDisplay`, `lastVisitDate`, `status`.

**4. Frontend design.** `features/patients/PatientSearch.tsx` (global, mounted in `AppLayout`; `/` focuses it — REC-16), `features/patients/PatientList.tsx` (route `/patients?query=`), `features/patients/RecentPatients.tsx` (on route `/`), `patientsApi.ts` gains `searchPatients(query, opts)` and `getRecentPatients(take)`, hooks `usePatientSearch(query)` (TanStack Query, `keepPreviousData`) and `useRecentPatients()`. Empty result renders `EmptyState` with an inline **"Register '<typed text>' as a new patient"** action (E-7). All rows are `PatientPickerRow.tsx` (E-28).

**5. Data integrity check.** Wrong-patient selection (RSK-12, E-28) is the risk, and it is a clinical-record-attachment risk, not a cosmetic one. Prevented by never rendering a name-only row and never auto-selecting a single match. Duplicate creation via failed search (E-30) is mitigated by the fuzzy fallback plus E-7's inline register action feeding F-6's duplicate check.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/PatientSearchServiceTests.cs` — last-4 phone match, ranking order, inactive exclusion, min-length rejection.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/PatientSearchEndpointTests.cs` — seeded 5,000-row set, asserts result correctness and records elapsed time against the 2 s budget.
- Frontend unit: `features/patients/PatientSearch.test.tsx` (debounce, `/` focus), `features/patients/RecentPatients.test.tsx` (empty state).
- E2E: `PMS.E2E/patient-search.spec.ts` — golden path; **E-28**: two patients with identical name and age are distinguishable in the result list without opening either.

**7. Acceptance criteria.**
- [ ] Typing 4 digits matching the tail of a stored phone returns that patient (E-59).
- [ ] Two patients sharing name and age render distinguishable rows (phone tail + last visit date) and neither is auto-selected (E-28).
- [ ] A no-match query renders "No patient found" plus a register action pre-filled with the typed text (E-7).
- [ ] With 5,000 patients seeded, the integration test's p95 search latency is ≤ 2 s (C-12, REC-19).
- [ ] Recent-patients list is empty-stated, not blank, on a fresh install (E-2).

**8. Effort & dependencies.** **L (M after the search-semantics call).** Depends on F-5 (and F-6's indexes). Blocks F-9.

---

### F-8 — Patient edit + deactivate (no hard delete)

**1. Readiness.** **Needs decision.**
> **Assumption (Q-6):** **no hard delete exists in Phase 1.** A patient is deactivated with a reason; visits remain intact and reachable (E-33, REC-7). Demographic edits are permitted and **written to the audit trail with old and new values** (F-17), never silently overwritten (C-17, §5.7). Retention period and right-to-erasure are deferred to §11 pending Q-6's legal input — the architecture does not foreclose them.

**2. Data model.** Adds `Status`, `InactiveReason` usage on `Patient` plus `RowVersion` concurrency token. Migration: **`AddPatientLifecycleFields`**.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| PUT | `/api/patients/{id}` | `UpdatePatientRequest` (carries `rowVersion`) | `PatientResponse` | 200, 400, 409 (concurrency or duplicate) | cookie |
| POST | `/api/patients/{id}/deactivate` | `DeactivateRequest{reason}` | `PatientResponse` | 200, 400 | cookie |
| POST | `/api/patients/{id}/reactivate` | — | `PatientResponse` | 200 | cookie |

**There is deliberately no `DELETE /api/patients/{id}`.**

**4. Frontend design.** `features/patients/PatientForm.tsx` reused at route `/patients/:id/edit`; `features/patients/DeactivatePatientDialog.tsx` (reason required) opened from `PatientProfile.tsx`; `patientsApi.ts` gains `updatePatient(id, req)`, `deactivatePatient(id, req)`, `reactivatePatient(id)`. Deactivated profiles render a persistent banner and are excluded from F-7 search by default.

**5. Data integrity check.** Orphan + mutable-history modes (RSK-7). No hard delete means a visit can never lose its parent (E-33). Every demographic edit emits an `AuditEvent` with before/after, so a name corrected after a prescription was printed is explainable rather than silent (C-17). `RowVersion` makes a two-tab concurrent edit fail loudly with 409 instead of last-write-wins.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/PatientLifecycleTests.cs` — deactivate requires a reason; edits emit audit events; re-normalisation on rename.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/PatientLifecycleEndpointTests.cs` — stale `rowVersion` returns 409; deactivated patient's visits still return 200 from history.
- Frontend unit: `features/patients/DeactivatePatientDialog.test.tsx`.
- E2E: `PMS.E2E/patient-lifecycle.spec.ts` — **E-33**: deactivate a patient with visits, confirm the history is still reachable and no delete control exists anywhere in the UI.

**7. Acceptance criteria.**
- [ ] No route, controller action or UI control performs a hard delete of a patient (assert by absence in the endpoint list test).
- [ ] Deactivation without a reason returns 400.
- [ ] A deactivated patient's visits remain retrievable via history (E-33).
- [ ] Editing name or DOB writes an `AuditEvent` of type `PatientDemographicsEdited` containing old and new values (§5.7).
- [ ] A stale `rowVersion` returns 409 and the UI shows a reload prompt, not a silent overwrite.

**8. Effort & dependencies.** **L (M after Q-6).** Depends on F-5 and F-17 (audit writer). Blocks nothing downstream.

---

### F-9 — Appointments: scheduling, daily list, status machine, walk-in start

**1. Readiness.** **Needs decision.**
> **Assumption (Q-5):** **walk-ins are supported per option D / REC-6** — the doctor clicks "Start consultation" from a patient profile and the system auto-creates an `Appointment` with `Source = WalkIn`, `ScheduledForUtc = now`, `Status = Scheduled`, then a draft `Visit` against it. Every visit therefore has an appointment parent; no orphan path exists.
> **Assumption (Q-14):** legal transitions are `Scheduled → Completed | Cancelled | NoShow`; **`NoShow → Completed` is allowed** (E-35, normal clinic life); `Cancelled → Completed` is allowed **with a confirm** and the prior status stays visible (E-36); **`Completed → Scheduled` is forbidden** (E-34) — mistakes are handled by amendment, not reversal. Yesterday's `Scheduled` rows are **never auto-marked** — they render as "Past — needs status" with an end-of-day prompt (E-37).
> **Assumption (this plan's own finding — C-24 has no `Q-` for the time model):** appointments are **date + time with a configurable default duration of 15 minutes, free times, no fixed slot grid**; a second same-day appointment for one patient is allowed with a warning (E-29); back- and forward-dating are allowed with a warning beyond ±90 days (E-38). This should be added to the §12 agenda.

**2. Data model.** `Appointment` (§4). Migration: **`AddAppointment`**. Instants stored as `DateTimeOffset` UTC and rendered clinic-local (E-45). Transition legality lives in `AppointmentService`, expressed as an explicit transition table — not scattered `if` statements.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| POST | `/api/appointments` | `CreateAppointmentRequest` | `AppointmentResponse` | 201, 400, 409 (same-day warn requires `confirm=true`) | cookie |
| GET | `/api/appointments?date=YYYY-MM-DD` | — | `AppointmentListResponse` | 200 | cookie |
| GET | `/api/appointments/pending-status` | — | `AppointmentResponse[]` | 200 | cookie |
| PUT | `/api/appointments/{id}` | `RescheduleRequest` | `AppointmentResponse` | 200, 400, 409 | cookie |
| POST | `/api/appointments/{id}/status` | `ChangeStatusRequest{status,confirm,note}` | `AppointmentResponse` | 200, 400, 409 (illegal transition) | cookie |
| POST | `/api/appointments/walk-in` | `StartWalkInRequest{patientId}` | `VisitResponse` | 201, 400, 409 | cookie |

**4. Frontend design.** `features/appointments/DailyAppointmentList.tsx` (route `/`, default today, sorted by time then creation — C-25), `AppointmentForm.tsx` (route `/appointments/new`), `AppointmentStatusMenu.tsx` (renders only legal next states), `PendingStatusBanner.tsx` (E-37). `appointmentsApi.ts`: `listByDate(date)`, `createAppointment(req)`, `changeStatus(id, req)`, `reschedule(id, req)`, `startWalkIn(patientId)`. Hooks `useAppointments(date)`, `useChangeAppointmentStatus()`, `useStartWalkIn()`. Empty day renders `EmptyState` offering **"Start walk-in consultation"** (E-3).

**5. Data integrity check.** Orphan + mutable-history modes (RSK-6). Auto-created walk-in appointments guarantee every `Visit` has a parent (E-3, option D). The transition table makes `Completed → Scheduled` unreachable, so a finalized consultation can never be silently detached (E-34). Every status change writes an `AuditEvent` (F-17), so `NoShow → Completed` leaves a trail (E-35). Midnight rollover writes nothing (E-37).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/AppointmentServiceTests.cs` — full transition matrix including E-34 rejection, E-35 and E-36 acceptance; same-day second appointment requires confirm (E-29); ±90-day warning (E-38).
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/AppointmentsEndpointTests.cs` — walk-in creates appointment + draft visit in one transaction; day query boundaries are inclusive of local midnight.
- Frontend unit: `features/appointments/AppointmentStatusMenu.test.tsx` — a `Completed` row offers no path back to `Scheduled`.
- E2E: `PMS.E2E/appointments.spec.ts` — golden path book → complete; **E-35**: a `NoShow` row is moved to `Completed` and the audit entry appears.

**7. Acceptance criteria.**
- [ ] `POST /api/appointments/{id}/status` with `Completed → Scheduled` returns 409 and changes nothing (E-34).
- [ ] `NoShow → Completed` succeeds and writes an audit event (E-35).
- [ ] `Cancelled → Completed` requires `confirm=true`; the previous status remains visible on the row (E-36).
- [ ] At 00:01 the previous day's `Scheduled` rows still read `Scheduled` and appear under "Past — needs status" (E-37).
- [ ] "Start walk-in consultation" from a patient profile creates exactly one appointment (`Source=WalkIn`) and exactly one draft visit, atomically.
- [ ] A day with no appointments renders an empty state offering the walk-in action (E-3).

**8. Effort & dependencies.** **L (M after Q-5, Q-14).** Depends on F-5, F-7. **Blocks F-10** and therefore the whole clinical path.

---

### F-10 — Visit lifecycle: draft, autosave, save-state, resume, single-tab lock, finalize

**1. Readiness.** **Needs decision.** This is the plan's largest feature and closes RSK-1.
> **Assumption (Q-3):** the converged model from **REC-1 / option C** is built — **continuously autosaved draft → explicit finalize at print → finalized visits immutable, corrections appended as dated amendments (F-15)**. Autosave debounce **2 s after last keystroke, and at latest every 5 s**, giving the RPO stated in §5.3 (**no more than 5 s of typed content lost to a crash**). Q-3 must confirm immutability before this ships; the alternative ("freely editable") would delete F-15 and change this feature's finalize semantics, so it is not a detail that can be settled later.
> **Assumption (E-40, no `Q-`):** a second tab opening the same visit gets a **read-only banner**, enforced by `LockToken` + a 15 s heartbeat; a lock older than 60 s is reclaimable. Silent last-write-wins is not acceptable for clinical content.

**2. Data model.** `Visit` (§4) plus the lock columns. Migration: **`AddVisitLifecycle`**. Rules enforced in `VisitService`: `VisitDate` is written once at draft creation and never recomputed at finalize (E-44); any write to a `Finalized` visit's clinical columns throws and returns 409; finalize is **idempotent by `Idempotency-Key`** so a double-click cannot produce two prescriptions (E-43).

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| POST | `/api/visits` | `StartVisitRequest{patientId,appointmentId?}` | `VisitResponse` | 201, 400, 409 (open draft exists) | cookie |
| GET | `/api/visits/{id}` | — | `VisitDetailResponse` | 200, 404 | cookie |
| PATCH | `/api/visits/{id}/draft` | `UpdateVisitDraftRequest` (partial: complaint, diagnosis, vitals, medications) | `DraftSaveResponse{savedUtc,rowVersion}` | 200, 409 (not draft / lock held), 412 | cookie |
| POST | `/api/visits/{id}/lock/heartbeat` | — | `LockResponse{isOwner,heldSinceUtc}` | 200, 409 | cookie |
| POST | `/api/visits/{id}/finalize` | `FinalizeVisitRequest` + `Idempotency-Key` header | `FinalizedVisitResponse{prescriptionId,prescriptionNumber}` | 200, 400 (vitals gate, F-11), 409 (already finalized) | cookie |
| GET | `/api/visits/open-drafts` | — | `VisitSummaryResponse[]` | 200 | cookie |
| POST | `/api/visits/{id}/discard-draft` | `DiscardDraftRequest{reason}` | — | 204, 409 | cookie |

**4. Frontend design.** `features/visits/ConsultationPage.tsx` (route `/visits/:id`) — one scrollable page holding `VitalsSection.tsx`, `ComplaintSection.tsx`, `DiagnosisSection.tsx`, `MedicationSection.tsx` (option C, single page, not a wizard). `features/visits/visitsApi.ts`: `startVisit(req)`, `getVisit(id)`, `patchDraft(id, patch)`, `heartbeat(id)`, `finalizeVisit(id, req, idempotencyKey)`, `listOpenDrafts()`, `discardDraft(id, req)`. Hooks: `useVisitDraft(id)`, `useAutosave(visitId, values)` (debounced mutation, exposes `status: 'saved' | 'saving' | 'error'` and `savedAt`), `useVisitLock(id)`. `shared/components/SaveStateBadge.tsx` renders **"Saved 10:42" / "Saving…" / "Not saved — retrying"** and never shows saved for an unconfirmed write (E-47, REC-17). `shared/hooks/useBeforeUnloadGuard.ts` warns on tab close with unsaved edits (E-42). `DraftBanner.tsx` marks a draft everywhere it appears (E-31). A "Resume draft" prompt appears on the dashboard from `listOpenDrafts()` (E-49). `FinalizeDialog.tsx` shows the medication list **in large type for visual check before commit** (E-17 — a legibility aid, not clinical advice).

**5. Data integrity check.** All four modes converge here. **Silent loss:** 5 s autosave RPO + honest save-state + beforeunload guard + resume-on-relaunch (E-42, E-47, E-49). **Mutable history:** finalized visits reject clinical edits at the service layer (E-32). **Orphan:** every visit is created with a patient and an appointment parent (F-9). **Duplicate:** finalize is idempotency-keyed and the submit button disables on click (E-43). Abandoned drafts are never silently discarded and never counted as completed visits (E-31).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/VisitServiceTests.cs` — finalize rejects a second call with the same idempotency key; patch on a finalized visit throws; `VisitDate` unchanged when finalize crosses midnight (E-44); lock reclaim after expiry (E-40).
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/VisitsEndpointTests.cs` — draft → patch → finalize round-trip creates exactly one `PrescriptionIssue`; concurrent double finalize yields one.
- Frontend unit: `features/visits/useAutosave.test.ts` (debounce timing, error state never renders "Saved"), `features/visits/ConsultationPage.test.tsx`, `shared/components/SaveStateBadge.test.tsx`.
- E2E: `PMS.E2E/consultation-lifecycle.spec.ts` — golden path draft → finalize; **E-42** (severest): type into the consultation, kill the tab without finalizing, reopen, and confirm every character within the last 5 s window is present; **E-40**: open the same visit in a second tab and confirm it is read-only with a banner.

**7. Acceptance criteria.**
- [ ] Typing in any consultation field triggers a save within 5 s; the badge shows the confirmed save time only after a 200 response (E-47).
- [ ] Killing the browser tab and reopening the visit restores all content typed more than 5 s before the kill (E-42, §5.3 RPO).
- [ ] An abandoned draft appears in the dashboard's resume list and in patient history **labelled "Draft"**, and is not counted as a completed visit (E-31).
- [ ] A finalized visit returns 409 on `PATCH /draft`; the UI offers "Add amendment" instead (E-32).
- [ ] Double-clicking Finalize produces exactly one `PrescriptionIssue` row (E-43).
- [ ] A draft started at 23:58 and finalized at 00:03 records `VisitDate` = the start date (E-44).
- [ ] A second tab on the same visit is read-only and says so (E-40).

**8. Effort & dependencies.** **L (genuinely over a week, and gated on Q-3).** Depends on F-9. **Blocks F-11, F-12, F-13, F-14, F-15, F-16, F-19.**

---

### F-11 — Vitals capture (mandatory-or-reason)

**1. Readiness.** **Needs decision.**
> **Assumption (Q-2):** **REC-3 / option F is adopted — vitals are required to finalize, but each may be marked "not recorded" with a reason from the F-4 list.** This is formally a BRD change (§5.2) and needs the owner's explicit acceptance. Absent is stored as `null` + reason; **no sentinel value is ever written** (E-18).
> **Assumption (Q-10):** temperature unit from `ClinicProfile`; BP as two integers in mmHg; plausibility warnings fire only where the doctor has entered a threshold in `VitalRangeSetting`, and are **soft confirms** (E-12, E-24). This plan authors no clinical range.

**2. Data model.** `VisitVitals` (§4). Migration: **`AddVisitVitals`**. Service rule (load-bearing): for each of temperature, BP and pulse, **exactly one of (value, not-recorded reason) must be present** before finalize succeeds; a value plus a reason, or neither, returns 400.

**3. API design.** Vitals are saved through `PATCH /api/visits/{id}/draft` (F-10) and validated at `POST /api/visits/{id}/finalize`. One dedicated read:

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| GET | `/api/visits/{id}/vitals` | — | `VitalsResponse` | 200, 404 | cookie |

**4. Frontend design.** `features/visits/VitalsSection.tsx` — three inputs, each with a "Not recorded" toggle revealing a reason `<select>` fed by `useSettingOptions('VitalsNotRecordedReason')`. `features/visits/useVitalsWarnings.ts(values, ranges)` returns soft warnings from `useVitalRanges()` (F-4). Temperature input displays the clinic unit as a suffix. Finalize is blocked by `FinalizeDialog.tsx` with a field-level message naming which vital is unaddressed.

**5. Data integrity check.** Silent-loss / fabricated-data mode (RSK-3, E-18). Storing absence as `null` + reason means permanent clinical history never contains an invented `0/0` or `120/80` typed from memory. The escape hatch removes the incentive that a hard block creates.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/VitalsServiceTests.cs` — value+reason rejected, neither rejected, either alone accepted; warning emitted only when a threshold exists (E-12).
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/VitalsEndpointTests.cs` — finalize returns 400 listing the unaddressed vital; succeeds once a reason is supplied.
- Frontend unit: `features/visits/VitalsSection.test.tsx` — toggling "Not recorded" clears the numeric value and requires a reason; `useVitalsWarnings.test.ts`.
- E2E: `PMS.E2E/vitals.spec.ts` — **E-18**: finalize a visit where BP cannot be taken, using "Equipment unavailable", and confirm the stored BP is null and the printed sheet shows "not recorded".

**7. Acceptance criteria.**
- [ ] Finalize fails with 400 when any of temperature, BP or pulse has neither a value nor a reason.
- [ ] Marking BP "not recorded — patient declined" allows finalize; `BpSystolic` and `BpDiastolic` are `NULL` in SQL (verify in SSMS), never `0` (E-18).
- [ ] The printed prescription shows "Not recorded (patient declined)" for that vital — never a blank that reads as an omission.
- [ ] With no thresholds configured, a temperature of 45 saves without any warning; with a doctor-set upper bound of 42, the same entry warns and still saves on confirm (E-12).
- [ ] Every displayed and printed temperature carries its unit (E-24).

**8. Effort & dependencies.** **L (M after Q-2, Q-10).** Depends on F-10, F-4. Blocks F-14.

---

### F-12 — Complaints & diagnosis capture

**1. Readiness.** **Needs decision.**
> **Assumption (Q-8):** **diagnosis is optional** for finalize; when empty, the printed prescription renders an explicit "Diagnosis: not recorded" line so the sheet is never ambiguously blank (E-19). If Q-8 answers "required", the change is one validator plus one acceptance criterion — small, but it is the owner's call, not the developer's.
> **Assumption (C-29, no `Q-`):** complaint soft cap 2,000 characters with a visible counter, hard cap 10,000, whitespace trimmed, line breaks preserved (E-14). Same caps for diagnosis at 2,000 hard.
> **Concretization noted (not silent):** brainstorm §6.2 sketches `Complaint` and `Diagnosis` as child entities; both are 1:1 with a visit and never repeat, so this plan stores them as `Visit.ComplaintText` and `Visit.DiagnosisText`. No behaviour in the brainstorm depends on them being separate rows.

**2. Data model.** `Visit.ComplaintText:string?`, `Visit.DiagnosisText:string?` (already in the F-10 migration). No new migration.

**3. API design.** Saved via `PATCH /api/visits/{id}/draft` (F-10). No dedicated endpoints.

**4. Frontend design.** `features/visits/ComplaintSection.tsx` and `features/visits/DiagnosisSection.tsx` — auto-growing `<textarea>` with character counter, wired to `useAutosave` from F-10. Paste beyond the hard cap truncates with a visible notice rather than silently dropping characters (E-14).

**5. Data integrity check.** Silent-loss risk is inherited from F-10's autosave — these are the two highest-volume free-text fields, so they are the ones a lost save actually costs. Unicode/emoji/smart-quote content round-trips unchanged (E-57), and free text is HTML-escaped at PDF render so it cannot garble the printed document (E-58).

**6. Test strategy.**
- Backend unit: covered in `VisitServiceTests.cs` — cap enforcement, whitespace trim, line-break preservation.
- Backend integration: within `VisitsEndpointTests.cs` — a 10,001-character complaint returns 400; a multi-line unicode complaint round-trips byte-identical.
- Frontend unit: `features/visits/ComplaintSection.test.tsx` — counter, paste truncation notice.
- E2E: covered by `consultation-lifecycle.spec.ts`; **E-58** asserted in `PMS.E2E/prescription.spec.ts` (F-14) where the rendered output is inspected.

**7. Acceptance criteria.**
- [ ] A complaint containing newlines and non-Latin characters saves and re-renders identically (E-57).
- [ ] Pasting 12,000 characters truncates to 10,000 with a visible notice (E-14).
- [ ] Finalizing with an empty diagnosis succeeds and prints "Diagnosis: not recorded" (E-19, under the Q-8 assumption).
- [ ] Text containing `<script>` renders as literal characters in the PDF, not as markup (E-58).

**8. Effort & dependencies.** **L (S after Q-8).** Depends on F-10. Blocks F-14.

---

### F-13 — Medications

**1. Readiness.** **Needs decision.**
> **Assumption (C-31 / E-22 — brainstorm §12 has no `Q-` for the required subset; this plan's own finding):** **Name + Dosage are required per line; Frequency, Duration and Instructions are optional and print only when present.** All five remain free text — a medicine master list is explicitly parked (§11), so spelling variants are an accepted Phase-1 risk.
> **Zero medications is explicitly legal** (E-5): the prescription prints "No medication prescribed".

**2. Data model.** `MedicationLine` (§4). Migration: **`AddMedicationLines`**. `LineNo` preserves the doctor's ordering; lines are replaced wholesale on each draft patch while the visit is a draft, and frozen at finalize.

**3. API design.** Saved via `PATCH /api/visits/{id}/draft` with the full line list. One read for history reuse:

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| GET | `/api/visits/{id}/medications` | — | `MedicationLineResponse[]` | 200, 404 | cookie |

**4. Frontend design.** `features/visits/MedicationSection.tsx` — repeatable rows with add/remove/reorder, `Name` and `Dosage` marked required, Enter adds the next row (keyboard-first, REC-16). `features/visits/useMedicationLines.ts` manages local row state and hands the array to `useAutosave`. `FinalizeDialog.tsx` (F-10) renders these lines in large type as the pre-commit visual check (E-17).

**5. Data integrity check.** Duplicate mode at the *string* level (C-31) is knowingly accepted this phase. The mitigations that do ship: line ordering is explicit, lines freeze at finalize so a printed sheet always matches the record (E-32), and the large-type confirm gives the doctor a legibility check against a mistyped dose (E-17 — **a legibility aid; no dosage rule is encoded anywhere in this system**).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/MedicationServiceTests.cs` — name/dosage required, empty list allowed, `LineNo` reassignment on reorder.
- Backend integration: within `VisitsEndpointTests.cs` — replacing the line set on a draft leaves no orphan rows; attempting it on a finalized visit returns 409.
- Frontend unit: `features/visits/MedicationSection.test.tsx` — Enter adds a row, remove keeps ordering contiguous.
- E2E: `PMS.E2E/prescription.spec.ts` covers **E-5** (advice-only visit) and **E-10** (10+ lines paginating).

**7. Acceptance criteria.**
- [ ] A line with a name but no dosage blocks finalize with a field-level 400 message.
- [ ] Finalizing with zero medication lines succeeds and the printed sheet reads "No medication prescribed" (E-5).
- [ ] Reordering lines then finalizing prints them in the displayed order.
- [ ] The finalize dialog lists every medication line in a type size large enough to proof-read (E-17).
- [ ] No dosage threshold, interaction rule or range check exists anywhere in the codebase.

**8. Effort & dependencies.** **L (M after the required-subset call).** Depends on F-10. Blocks F-14.

---

### F-14 — Prescription generation, print, reprint

**1. Readiness.** **Needs decision.**
> **Assumption (Q-4):** header/footer content comes from `ClinicProfile` (F-3); printing is impossible until `IsSetupComplete` (E-1).
> **Assumption (Q-8):** an empty diagnosis prints "Diagnosis: not recorded" (E-19).
> Rendering is **server-side QuestPDF** per the §2 decision (C-47, E-10). Prescription identity: `PrescriptionNumber` = `YYYYMMDD-NNNN` sequential per day, unique, assigned at finalize (C-32).

**2. Data model.** `PrescriptionIssue` (§4). Migration: **`AddPrescriptionIssue`**. Created inside the finalize transaction (F-10) — never separately, so a finalized visit without a prescription record is unreachable. `PrintCount` increments on each render; **the visit is "issued" at finalize regardless of whether the print dialog is completed** (E-52).

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| GET | `/api/prescriptions/{visitId}/preview` | — | `PrescriptionPreviewResponse` (JSON, for on-screen check) | 200, 404, 409 (draft) | cookie |
| GET | `/api/prescriptions/{visitId}/pdf` | — | `application/pdf` stream | 200, 404, 409 (setup incomplete / draft), 500 | cookie |
| POST | `/api/prescriptions/{visitId}/record-print` | `RecordPrintRequest{isReprint}` | `PrescriptionIssueResponse` | 200, 404 | cookie |

**4. Frontend design.** `features/prescriptions/PrescriptionPreview.tsx` (route `/visits/:id/prescription`) — embeds the PDF in an `<iframe>` and offers Print and Reprint. `features/prescriptions/prescriptionsApi.ts`: `getPreview(visitId)`, `getPdfUrl(visitId)`, `recordPrint(visitId, isReprint)`. Hook `usePrescription(visitId)`. Finalize (F-10) navigates here automatically. PDF failure renders a retry action and an explicit error — **never a silent blank frame** (E-53).

**5. Data integrity check.** Mutable-history mode. The PDF is rendered from the frozen finalized visit, so a reprint months later is byte-identical to the sheet the patient holds (E-32, E-52). Finalize commits **before** rendering, so a server error after the print click cannot lose the record — print is a retryable downstream step (E-51). Every print and reprint writes an `AuditEvent` (F-17, E-63). Pagination repeats patient name, date and "Page n of m" and **never truncates** (E-10).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/PrescriptionServiceTests.cs` — number generation and uniqueness, 409 on a draft visit, 409 when setup incomplete, print-count increment.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/PrescriptionsEndpointTests.cs` — PDF endpoint returns a non-empty `application/pdf`; a 12-medication visit produces > 1 page and the last page carries the footer.
- Frontend unit: `features/prescriptions/PrescriptionPreview.test.tsx` — render failure shows retry, not a blank frame (E-53).
- E2E: `PMS.E2E/prescription.spec.ts` — golden path finalize → preview → print; **E-10**: 12 medications paginate with repeated header and "Page 1 of 2"; **E-52**: cancelling the print dialog leaves the visit finalized and reprint available.

**7. Acceptance criteria.**
- [ ] A finalized visit produces a PDF containing clinic name, doctor name, registration number, patient name + age, all three vitals with units (or "not recorded" + reason), diagnosis, medication lines, and the footer/signature area.
- [ ] A draft visit returns 409 from both prescription endpoints.
- [ ] With `IsSetupComplete = false`, the PDF endpoint returns 409 (E-1).
- [ ] 12 medication lines paginate; every page repeats patient name, date and "Page n of m"; nothing is truncated (E-10).
- [ ] Cancelling the browser print dialog leaves the visit finalized; the reprint action works and increments `PrintCount` (E-52).
- [ ] A reprint produced a week later is byte-identical to the original PDF (E-32).
- [ ] The same PDF is produced in Chrome, Edge and Safari (server-rendered — asserted by hash comparison in the E2E run) (C-47).

**8. Effort & dependencies.** **L (M after Q-4, Q-8).** Depends on F-3, F-11, F-12, F-13. Blocks F-15, F-18.

---

### F-15 — Visit amendments (append-only)

**1. Readiness.** **Needs decision.**
> **Assumption (Q-3):** finalized visits are immutable and corrections are appended as dated amendments (REC-1). If Q-3 answers "freely editable", **this feature does not exist** and F-10's finalize semantics change — which is why Q-3 must be answered before F-10 ships, not before F-15.

**2. Data model.** `VisitAmendment` (§4). Migration: **`AddVisitAmendment`**. `Sequence` is server-assigned; **no update or delete endpoint or service method exists** — that absence is the feature.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| POST | `/api/visits/{id}/amendments` | `CreateAmendmentRequest{text}` | `AmendmentResponse` | 201, 400, 409 (visit is a draft) | cookie |
| GET | `/api/visits/{id}/amendments` | — | `AmendmentResponse[]` | 200, 404 | cookie |

**4. Frontend design.** `features/visits/AmendmentPanel.tsx` — rendered on a finalized `ConsultationPage.tsx` and inside `history/VisitDetail.tsx`; shows the original record read-only above a chronological amendment list with an "Add amendment" form. `visitsApi.ts` gains `addAmendment(visitId, req)` and `listAmendments(visitId)`; hook `useAmendments(visitId)`. Attempting to edit a finalized field surfaces "This visit is finalized — add an amendment instead", not a disabled control with no explanation (E-32, E-39).

**5. Data integrity check.** Mutable-history mode, closed. The original text is never overwritten; each amendment carries its own timestamp; the printed sheet the patient holds always matches the stored original (E-32). Reopening a finalized visit produces an amendment, never an edit (E-39). Each amendment writes an `AuditEvent` (F-17).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/AmendmentServiceTests.cs` — sequence assignment, rejection on a draft visit, no update/delete method exposed on the interface.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/AmendmentsEndpointTests.cs` — two amendments return in order; PUT and DELETE on the route return 405.
- Frontend unit: `features/visits/AmendmentPanel.test.tsx` — original section is read-only; empty amendment text is rejected.
- E2E: `PMS.E2E/amendments.spec.ts` — **E-32**: finalize, print, then correct the diagnosis by amendment; confirm the original diagnosis text is still visible and the reprinted PDF is unchanged.

**7. Acceptance criteria.**
- [ ] No API route, service method or UI control can modify or delete a finalized visit's clinical fields or an existing amendment (405/409 asserted).
- [ ] An amendment renders with its own date and is visually separated from the original record.
- [ ] Reprinting after an amendment reproduces the original prescription; the amendment is not injected into the historical sheet (E-32).
- [ ] Amendments on a draft visit return 409 (E-39).

**8. Effort & dependencies.** **L (M after Q-3).** Depends on F-14. Blocks F-16 (history renders amendments).

---

### F-16 — Patient history + date filter

**1. Readiness.** **Ready.** C-33 and C-34 are `Ready` in the brainstorm with named defaults; this feature's schedule risk is entirely upstream (F-10, F-14, F-15).
Defaults adopted: **newest-first ordering; drafts included but visibly distinct (E-31); inclusive date range picker with presets (Last 30 days / This year / All); explicit empty state (E-4); pagination at 50 visits (E-16).**

**2. Data model.** No new entities. Read-only projections `VisitSummaryResponse` and `VisitDetailResponse`; a covering index on `Visit(PatientId, VisitDate DESC)` is added in migration **`AddVisitHistoryIndex`** to hold the C-12 latency budget at 25,000 visits.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| GET | `/api/patients/{id}/visits?from=&to=&page=&size=` | — | `PagedResponse<VisitSummaryResponse>` | 200, 400, 404 | cookie |
| GET | `/api/visits/{id}/detail` | — | `VisitDetailResponse` (vitals + complaint + diagnosis + medications + amendments + prescription) | 200, 404 | cookie |

**4. Frontend design.** `features/history/PatientHistory.tsx` (rendered inside route `/patients/:id`), `features/history/HistoryDateFilter.tsx`, `features/history/VisitDetail.tsx` (route `/patients/:id/visits/:visitId`). `features/history/historyApi.ts`: `listVisits(patientId, filter)`, `getVisitDetail(visitId)`. Hook `usePatientHistory(patientId, filter)` with `keepPreviousData` so filter changes never blank the list. Draft rows render `DraftBanner`'s badge (E-31).

**5. Data integrity check.** Orphan/readability mode. History is the surface where a mislabelled draft becomes a clinical misreading — an unfinished draft displayed as a completed visit is the exact failure REC-1's top unresolved edge case (E-31) warns about. Prevented by a mandatory, non-dismissible "Draft — not finalized" badge on every draft row and detail view.

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/HistoryServiceTests.cs` — inclusive bounds, newest-first order, drafts included with state flag, paging.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/HistoryEndpointTests.cs` — 25,000-visit seed, asserts p95 latency inside the C-12 budget and correct page boundaries.
- Frontend unit: `features/history/PatientHistory.test.tsx` (draft badge present), `features/history/HistoryDateFilter.test.tsx` (empty-match state).
- E2E: `PMS.E2E/patient-history.spec.ts` — golden path; **E-31**: a patient with one finalized and one abandoned visit shows two visually distinct rows and the draft is excluded from any "completed visits" count.

**7. Acceptance criteria.**
- [ ] Visits render newest-first with date, diagnosis snippet and medication count.
- [ ] A draft visit carries a "Draft — not finalized" badge in both the list and the detail view (E-31).
- [ ] A date range matching nothing renders an explicit empty state, not a blank grid (E-4, C-34).
- [ ] A patient with zero visits renders "No previous visits" plus a start-consultation action (E-4).
- [ ] With 25,000 visits seeded, the first history page returns inside the 2 s budget (C-12, REC-19).
- [ ] Visit detail shows vitals (with units or not-recorded reasons), complaint, diagnosis, medications, amendments and prescription number.

**8. Effort & dependencies.** **M.** Depends on F-10, F-14, F-15. Blocks F-18.

---

### F-17 — Audit trail (six event types)

**1. Readiness.** **Needs decision.**
> **Assumption (REC-9 / C-48 — brainstorm §12 carries no `Q-` for the audit trail; this plan's own finding, see §9):** a minimal append-only trail on **six event types** — `VisitFinalized`, `VisitAmended`, `PrescriptionPrinted` (with reprint flag), `PatientDemographicsEdited` (old + new), `PatientDeactivated`, `ExportGenerated`. Not a general-purpose audit framework. Retention of audit rows follows whatever Q-6 sets for clinical data.

**2. Data model.** `AuditEvent` (§4). Migration: **`AddAuditEvent`**. `IAuditWriter.WriteAsync(...)` is called **inside the same transaction as the event it records**, so an audited action and its trail commit or fail together. No update or delete path exists.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| GET | `/api/audit?from=&to=&type=&entityId=&page=&size=` | — | `PagedResponse<AuditEventResponse>` | 200, 400 | cookie |

There is deliberately no write endpoint — audit rows are only ever written by services.

**4. Frontend design.** `features/audit/AuditLogPage.tsx` (route `/audit`), `features/audit/auditApi.ts` (`listAudit(filter)`), `features/audit/useAuditLog.ts`. Read-only table: time, event type, entity, summary.

**5. Data integrity check.** Mutable-history mode (RSK-9, E-63). Same-transaction writes mean an action can never exist without its trail; no update/delete surface means the trail cannot be edited to match a story. This is the record that answers "what was prescribed, when, and what changed" years later (§5.7).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/AuditWriterTests.cs` — event written for each of the six types; a rolled-back transaction leaves no audit row.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/AuditEndpointTests.cs` — finalize then query returns exactly one `VisitFinalized`; PUT/DELETE on `/api/audit/{id}` return 405.
- Frontend unit: `features/audit/AuditLogPage.test.tsx` — filter by type, paging.
- E2E: `PMS.E2E/audit.spec.ts` — **E-63**: perform finalize, print, reprint, amend, demographic edit, deactivate and export, and confirm six distinct entries appear.

**7. Acceptance criteria.**
- [ ] Each of the six actions writes exactly one `AuditEvent` with a UTC timestamp.
- [ ] `PatientDemographicsEdited` contains both the old and the new value.
- [ ] `PrescriptionPrinted` distinguishes an original from a reprint.
- [ ] A failed/rolled-back finalize leaves no audit row (verified by count query in SSMS or the integration test).
- [ ] No API surface can modify or delete an audit row.

**8. Effort & dependencies.** **L (M after audit acceptance).** Depends on F-1. Consumed by F-8, F-9, F-14, F-15, F-18.

---

### F-18 — Export CSV / PDF

**1. Readiness.** **Needs decision.**
> **Assumption (Q-11):** export scope defaults to the **narrowest useful selection** (REC-10) — three scopes only: **one visit**, **one patient's visits (optionally date-ranged)**, and **all visits in a date range**. **There is no whole-database export in Phase 1.** Every export writes an `ExportGenerated` audit event (F-17) and shows a PHI warning before the file is produced (E-61).
> Hardening is `Ready` and non-negotiable: RFC-4180 quoting, formula-injection prefix escaping, UTF-8 BOM (E-55, E-56, E-57).

**2. Data model.** No new entities; export events land in `AuditEvent`. No migration.

**3. API design.**

| Method | Route | Request DTO | Response DTO | Status | Auth |
|---|---|---|---|---|---|
| POST | `/api/export/preview` | `ExportRequest{scope,patientId?,visitId?,from?,to?}` | `ExportPreviewResponse{rowCount,estimatedSizeKb}` | 200, 400 | cookie |
| POST | `/api/export/csv` | `ExportRequest` | `text/csv` stream | 200, 400, 409 (empty range), 500 | cookie |
| POST | `/api/export/pdf` | `ExportRequest` | `application/pdf` stream | 200, 400, 409, 500 | cookie |

**4. Frontend design.** `features/export/ExportPage.tsx` (route `/export`) — scope selector, date range, preview row count, and a **PHI warning that must be acknowledged before the download starts** (E-61). `features/export/exportApi.ts`: `previewExport(req)`, `downloadCsv(req)`, `downloadPdf(req)`. Hook `useExport()`. A zero-row preview blocks the download with "Nothing to export in this range" rather than emitting an empty file (E-6). Generation failure shows a retry and no partial file (E-53).

**5. Data integrity check.** Output-integrity and privacy modes. A complaint containing commas, quotes or newlines is RFC-4180 quoted so columns never shift silently (E-55) — a corrupted export is the failure that *looks* fine when opened. Fields beginning `=`, `+`, `-`, `@` are prefix-escaped so an exported file cannot execute in Excel on someone else's machine (E-56). UTF-8 BOM keeps non-Latin names intact (E-57). The file leaving the app is unpreventable; it is warned and logged (E-61).

**6. Test strategy.**
- Backend unit: `PMS.Application.Tests/Services/ExportServiceTests.cs` and `PMS.Application.Tests/Export/Rfc4180CsvWriterTests.cs` — embedded comma/quote/newline round-trip, formula-prefix escaping for all four characters, BOM present, zero-row rejection.
- Backend integration: `PMS.Api.IntegrationTests/Endpoints/ExportEndpointTests.cs` — CSV re-parses to the expected column count for every row; each export writes one audit event.
- Frontend unit: `features/export/ExportPage.test.tsx` — warning must be acknowledged; zero-row preview disables download.
- E2E: `PMS.E2E/export.spec.ts` — **E-55/E-56**: export a visit whose complaint contains `"Chest pain, since Monday\nworse at night"` and a diagnosis beginning `=`, then assert column alignment and the escaped prefix in the downloaded file.

**7. Acceptance criteria.**
- [ ] A complaint containing a comma, a double quote and a newline exports as a single correctly-quoted field; re-parsing yields the original text (E-55).
- [ ] A field beginning `=`, `+`, `-` or `@` is prefix-escaped in the CSV (E-56).
- [ ] The CSV opens in Excel with non-Latin names rendered correctly (BOM present) (E-57).
- [ ] An export over an empty date range is refused with a message and no file is written (E-6).
- [ ] Every successful export writes one `ExportGenerated` audit event naming scope and row count (E-61).
- [ ] No endpoint offers a whole-database export.

**8. Effort & dependencies.** **L (M after Q-11).** Depends on F-14 (PDF renderer), F-16 (projections), F-17 (audit). Blocks nothing.

---

### F-19 — Keyboard-first input + performance instrumentation

**1. Readiness.** **Needs decision.**
> **Assumption (Q-15):** the 2–3 minute target is split per **REC-14 / §5.1** — **(a) system overhead ≤ 30 s per visit**, instrumented and regression-tested, and **(b) end-to-end ≤ 3 min** against a defined typical-visit fixture (3 vitals, ≤200-char complaint, ≤100-char diagnosis, 2 medications), used as an acceptance test only. Both numbers need the owner's confirmation; the keyboard work itself (REC-16) is `Ready` and independent.
> **Assumption (REC-19):** the single latency number used everywhere is **p95 ≤ 2 s at 5,000 patients / 25,000 visits**, and the sizing ceiling is **≤ 40 consultations/day, ≤ 6,000 patients, ≤ 30,000 visits over five years** (C-46) — stated so nobody over-engineers.

**2. Data model.** None. `RequestTimingMiddleware.cs` (F-1) emits per-endpoint server timings to the log; no PHI is logged.

**3. API design.** No new endpoints. Server-Timing headers are added to existing responses.

**4. Frontend design.** `shared/hooks/useHotkeys.ts` registering: `/` focus global search, `Alt+1..4` jump to Vitals/Complaint/Diagnosis/Medications, `Ctrl+Enter` finalize, `Esc` close dialogs (option E, REC-16). `shared/components/HotkeyHintRow.tsx` makes them discoverable. Fixed and tested tab order across `ConsultationPage.tsx`. `shared/api/httpClient.ts` records client-side round-trip timings behind a dev flag.

**5. Data integrity check.** No new write path. One risk it introduces: `Ctrl+Enter` finalizing prematurely — mitigated because finalize always routes through `FinalizeDialog.tsx` (F-10), so the shortcut opens the confirm, never commits directly.

**6. Test strategy.**
- Backend unit: n/a.
- Backend integration: `PMS.Api.IntegrationTests/Performance/EndpointBudgetTests.cs` — seeded dataset, asserts search, history and finalize stay inside budget.
- Frontend unit: `shared/hooks/useHotkeys.test.ts`, `features/visits/ConsultationPage.taborder.test.tsx`.
- E2E: `PMS.E2E/typical-visit.spec.ts` — the §5.1 fixture driven **keyboard-only**, recording total system overhead.

**7. Acceptance criteria.**
- [ ] A complete typical visit (fixture above) can be entered and finalized without touching the mouse.
- [ ] `/`, `Alt+1..4`, `Ctrl+Enter` and `Esc` behave as listed and are shown in a hint row.
- [ ] `Ctrl+Enter` opens the finalize confirm; it never commits directly.
- [ ] Measured system overhead for the fixture visit is ≤ 30 s (Q-15 assumption).
- [ ] Search and history p95 ≤ 2 s at 5,000 patients / 25,000 visits (C-12, REC-19).
- [ ] Shortcuts are verified working in Chrome, Edge and Safari (C-47).

**8. Effort & dependencies.** **L (S after Q-15).** Depends on F-10..F-13. Blocks nothing.

---

### F-20 — Backup, restore rehearsal, backup-status indicator, encryption at rest

**1. Readiness.** **Blocked — needs decision first.**

**What must be resolved, and where:**
- **Q-1 (Critical, policy call): where does this run — clinic PC, LAN server, or cloud?** Until this is answered there is no backup *destination*, no meaning for "encryption at rest" (C-45: disk encryption + key custody on a clinic PC vs. storage encryption + TLS in cloud), no session-security context, and no answer to what happens to the clinic during an outage (C-9, §5.6).
- **Q-12 (Critical, policy call then design + build): what is the acceptable data-loss window (RPO), and who is told when a backup fails?** A backup nobody monitors is not a backup (C-43, E-48), and an untested restore is an assumption, not a control (E-50).

**Why no steps are written:** every concrete artefact this feature needs — a SQL Server Agent job vs. a scheduled `sqlcmd` script vs. a managed cloud backup, a destination path or container, a TDE/BitLocker decision, an alerting channel — is determined by Q-1. Writing file targets now would be guessing at infrastructure, and this is the one feature where a wrong guess is unrecoverable.

**What is already fixed and does not need to wait:** the §5.3 **application-level RPO of 5 s of typed content** is delivered by F-10's autosave regardless of Q-1. Only the *database* backup RPO and destination are blocked.

**Effort & dependencies.** **L** (Blocked; the decision cycle plus a restore rehearsal). Depends on F-1 and on Q-1 + Q-12. **Blocks go-live, not the build.** Recommendation: put Q-1 first on the §12 decision-session agenda, exactly as brainstorm §10's sequencing note says — it unblocks four other items in one meeting.

---

### F-21 — Credential recovery, lockout policy

**1. Readiness.** **Blocked — needs decision first.**

**What must be resolved, and where:** **C-44** — "with exactly one user there is no one to reset the password and no one to unlock a locked-out account." **Brainstorm §12 carries no open question for this**, which is a gap in the open-questions list rather than an answered item; it is listed in §9 below and should be added to the decision-session agenda alongside Q-1.

The specific calls needed: (a) is there a lockout after N failed attempts, and if so what unlocks it; (b) what is the recovery path when the sole credential is lost — a recovery code issued at setup, a second break-glass account, or documented direct database intervention; (c) who holds the recovery artefact physically.

**Why no steps are written:** each of the three candidate answers implies different entities (recovery-code hash on `AppUser`, a second `AppUser` row, or none at all), different endpoints and a different first-run flow. There is no honest default here — the wrong choice either locks the doctor out of their own patient records or leaves a permanent bypass on a machine holding PHI. This is a security decision and this plan will not make it silently.

**Effort & dependencies.** **L** (Blocked). Depends on F-2 and a resolution of C-44. **Blocks go-live** — the clinic should not run on a single credential with no recovery path.

---

## 7. Cross-cutting concerns

**Authentication & session.** Cookie-based, `HttpOnly` / `Secure` / `SameSite=Strict`, per the §2 decision and F-2. Every `/api/*` endpoint except `health` and `auth/login` requires the cookie. Idle screen lock (5 min) is separate from session expiry (12 h) so a lock never costs a draft (E-41, E-62, REC-11).

**Authorization.** One role, one user. Access control is therefore binary — authenticated or not. **Row-level rules are deliberately absent** and the data model does not add a tenant/owner column, per C-46: multi-doctor is parked (§11) and must not be built for.

**Encryption.** *In transit:* HTTPS enforced by `UseHsts` + `UseHttpsRedirection`; the auth cookie is `Secure`-only. *At rest:* **Blocked on Q-1** (F-20) — TDE, BitLocker or cloud storage encryption are three different answers to three different deployment models, and choosing before Q-1 would be a guess (C-45).

**Logging & audit.** Two distinct channels, not one. *Operational logs* (Serilog to rolling files, or the cloud sink Q-1 implies) record request timings and exceptions and **never contain PHI** — no patient names, no complaint text, no medication lines. *Clinical audit* is `AuditEvent` (F-17), append-only, six event types, written in the same transaction as the action.

**Error handling.** One convention: `ProblemDetailsMiddleware.cs` maps validation failures to 400 with a field-keyed `errors` object, domain-rule violations to 409 with a machine-readable `type` (e.g. `visit-already-finalized`, `setup-incomplete`, `illegal-status-transition`), concurrency conflicts to 409 with `rowVersion`, and unhandled exceptions to 500 with a correlation id and no internal detail. `shared/api/httpClient.ts` throws a typed `ProblemDetailsError`; every mutation hook surfaces it — **no promise is silently swallowed**, because a swallowed rejection is exactly the E-47 failure ("doctor believes it saved").

**Backup & restore.** **Blocked on Q-1 + Q-12 (F-20).** What is already decided and independent of that: the application-level RPO of ≤ 5 s of typed content (§5.3, delivered by F-10), a visible last-successful-backup indicator wherever the backup mechanism ends up reporting from (REC-8, E-48), and a **rehearsed restore before go-live** as a hard gate (E-50).

**CSV export implementation.** `PMS.Infrastructure/Export/Rfc4180CsvWriter.cs` — server-side, streaming, RFC-4180 quoting, formula-injection prefix escaping, UTF-8 BOM (E-55, E-56, E-57). Never assembled client-side.

**PDF export & prescription implementation.** `PMS.Infrastructure/Printing/QuestPdfPrescriptionRenderer.cs` — server-side QuestPDF for both the prescription (F-14) and the PDF export (F-18), sharing one document builder so a prescription inside an export is identical to the printed original. Cross-browser print divergence (C-47) is removed by construction.

**Performance (NFR).** Page load < 2 s (BRD L177) is met by a Vite production build with route-level code splitting, and by TanStack Query caching that keeps the daily list and recent patients warm. Search and history hold p95 ≤ 2 s at the stated volumes (C-12, REC-19) via the `NormalizedName`/`NormalizedPhone` and `Visit(PatientId, VisitDate)` indexes plus server-side paging — **no client-side filtering of full result sets anywhere**. Consultation-flow overhead is instrumented and budgeted in F-19.

**Clinical-rule boundary.** No vitals range, dosage limit, frequency rule or interaction check exists anywhere in this codebase. Thresholds are rows in `VitalRangeSetting` entered by the doctor; the system enforces only what it is given (E-12, C-31, REC-3). This constraint is a review item on every pull request touching F-11 or F-13.

---

## 8. Test strategy summary

**Tooling, stated once.** Backend unit: **xUnit + NSubstitute**, with `FluentAssertions`. Backend integration: **xUnit + `WebApplicationFactory<Program>` against SQL Server LocalDB**, database created per test class from migrations and torn down after (never against a developer's dev database). Frontend unit: **Vitest + React Testing Library** (Vitest chosen over Jest for Vite-native config; used consistently across every feature). End-to-end: **Playwright** (chosen over Cypress for first-class multi-browser coverage — Chrome, Edge and WebKit/Safari are a stated BRD compatibility requirement, C-47).

**Coverage map — feature × layer.** `Y` = specs planned; `—` = not applicable; `Blocked` = cannot be specified until the gate clears.

| Feature | BE unit | BE integration | FE unit | E2E | Edge cases covered |
|---|---|---|---|---|---|
| F-1 shell | Y | Y | Y | Y | — |
| F-2 auth/session | Y | Y | Y | Y | E-41, E-62, E-65 |
| F-3 clinic profile | Y | Y | Y | Y | E-1 |
| F-4 settings | Y | Y | Y | Y | E-12, E-23, E-24 |
| F-5 registration | Y | Y | Y | Y | E-8, E-11, E-13, E-20, E-21, E-9, E-57, E-60 |
| F-6 duplicates | Y | Y | Y | Y | E-25, E-26, E-27, E-28, E-30, E-46 |
| F-7 search | Y | Y | Y | Y | E-2, E-7, E-28, E-59 |
| F-8 edit/deactivate | Y | Y | Y | Y | E-33 |
| F-9 appointments | Y | Y | Y | Y | E-3, E-29, E-34, E-35, E-36, E-37, E-38 |
| F-10 visit lifecycle | Y | Y | Y | Y | E-31, E-39, E-40, E-42, E-43, E-44, E-47, E-49, E-51 |
| F-11 vitals | Y | Y | Y | Y | E-12, E-18, E-24 |
| F-12 complaint/diagnosis | Y | Y | Y | via F-14 | E-14, E-19, E-57, E-58 |
| F-13 medications | Y | Y | Y | via F-14 | E-5, E-17, E-22 |
| F-14 prescription | Y | Y | Y | Y | E-1, E-5, E-10, E-51, E-52, E-53 |
| F-15 amendments | Y | Y | Y | Y | E-32, E-39 |
| F-16 history | Y | Y | Y | Y | E-4, E-16, E-31, E-34 |
| F-17 audit | Y | Y | Y | Y | E-63 |
| F-18 export | Y | Y | Y | Y | E-6, E-55, E-56, E-57, E-61 |
| F-19 keyboard/perf | — | Y | Y | Y | C-40, C-12 |
| F-20 backup/restore | Blocked | Blocked | Blocked | Blocked | E-48, E-49, E-50, E-54 |
| F-21 credential recovery | Blocked | Blocked | Blocked | Blocked | C-44 |

**Visible gaps, named rather than hidden.** (a) F-20 and F-21 have **no test coverage at any layer** until their gates clear, and they carry four Critical `[DI]` edge cases between them (E-48, E-49, E-50, E-54) — this is the most important row in the table. (b) **E-54 (disk/storage full)** cannot be exercised meaningfully until the deployment model exists; it is currently untestable, not merely untested. (c) F-12 and F-13 have no dedicated E2E spec — they are exercised through F-14's, which is deliberate (they have no standalone user journey) but means an F-14 spec failure masks them.

**Edge cases knowingly untested in Phase 1:** E-64 (shared printer, `accepted` in brainstorm §8.9), E-45 (DST, parked in §11), E-15 and E-16's extreme volumes (asserted by seeded data, not by production-scale load testing).

---

## 9. Open items

Every `Blocked` gate and every `Assumption:` from §6, in one place. Items marked **"no `Q-` exists"** are this plan's own findings — they were not raised as open questions in brainstorm §12 and should be added to that agenda.

| # | Item | Type | Feature(s) | Source ID | Needed from | Default being built against |
|---|---|---|---|---|---|---|
| 1 | Deployment model (clinic PC / LAN / cloud) | **Blocker** | F-20 | Q-1 | Owner (policy call) | none — no steps written |
| 2 | RPO for database backup + who is alerted on failure | **Blocker** | F-20 | Q-12 | Owner, then design+build | none — no steps written |
| 3 | Credential recovery + lockout for a single user | **Blocker** | F-21 | C-44 — **no `Q-` exists** | Owner (security call) | none — no steps written |
| 4 | Vitals: may a visit finalize with a recorded reason instead of a value? | Assumption | F-11, F-4 | Q-2 | Owner | mandatory-or-reason, absent stored as null + reason (REC-3, E-18) |
| 5 | Finalized visits immutable with append-only amendments? | Assumption | F-10, F-15 | Q-3 | Owner | immutable + dated amendments (REC-1, option C) |
| 6 | Clinic header/footer content and signature image source | Assumption | F-3, F-14 | Q-4 | Owner | ClinicProfile fields per §4; print blocked until setup complete (E-1) |
| 7 | Walk-ins — can a consultation exist without a booked appointment? | Assumption | F-9 | Q-5 | Owner | yes; appointment auto-created with `Source=WalkIn` (REC-6, option D) |
| 8 | Hard delete and retention period for patient records | Assumption | F-8 | Q-6 | Owner + legal | no hard delete; deactivate with reason; retention deferred (E-33) |
| 9 | Is phone required at registration? | Assumption | F-5 | Q-7 | Owner | optional, prompted, profile flagged incomplete (E-20, E-8) |
| 10 | Is diagnosis required before printing? | Assumption | F-12, F-14 | Q-8 | Owner | optional; prints "Diagnosis: not recorded" (E-19) |
| 11 | Gender value list | Assumption | F-4, F-5 | Q-9 | Owner | doctor-editable list seeded Female/Male/Other/Not stated (E-23) |
| 12 | Vitals units and plausibility thresholds | Assumption | F-4, F-11 | Q-10 | Owner (thresholds are the doctor's) | unit in ClinicProfile; thresholds blank by default; soft warnings only (E-12, E-24) |
| 13 | Export scope | Assumption | F-18 | Q-11 | Owner | visit / patient / date-range only; **no whole-DB export** (REC-10, E-61) |
| 14 | Patient identity rule + similarity threshold | Assumption | F-6 | Q-13 | Owner + design | name similarity ≥ 0.85 AND (phone OR DOB); warn, never block (REC-2) |
| 15 | Legal appointment status transitions | Assumption | F-9 | Q-14 | Owner | per E-34/E-35/E-36/E-37 as tabled in F-9 |
| 16 | How the 2–3 minute target is measured | Assumption | F-19 | Q-15 | Owner | ≤ 30 s system overhead + ≤ 3 min end-to-end on the §5.1 fixture (REC-14) |
| 17 | DOB vs approximate age at registration | Assumption | F-5 | Q-16 | Owner | DOB when known, else approx age + recorded-on; never bare age (E-9) |
| 18 | Appointment time model (date+time, duration, slots) | Assumption | F-9 | C-24 — **no `Q-` exists** | Owner | date+time, free times, default 15 min, warn on second same-day (E-29, E-38) |
| 19 | Search match semantics (substring / fuzzy / ranking) | Assumption | F-7 | C-22, C-35 — **no `Q-` exists** | Owner | substring + digits-only phone incl. last-4; fuzzy fallback on empty result |
| 20 | Medication required-field subset | Assumption | F-13 | C-31, E-22 — **no `Q-` exists** | Owner | Name + Dosage required; others optional and printed only when present |
| 21 | Audit trail scope and acceptance | Assumption | F-17 | REC-9, C-48 — **no `Q-` exists** | Owner | six event types per §5.7; append-only |
| 22 | QuestPDF licence tier for the deployed environment | Assumption | F-14, F-18 | — (this plan's finding) | Owner/legal | Community licence assumed; confirm before go-live |

**Deferred, not planned (brainstorm §11 is the single home):** duplicate merge tooling, prefill-from-last-visit (option H — its failure mode is a wrong medication on a real prescription, and §11 sequences it after the lifecycle work is stable), medicine master list, structured/coded diagnosis, right-to-erasure workflow, and everything on the BRD's out-of-scope list. The data model does not foreclose any of them — `MergedIntoPatientId` and the append-only `AuditEvent` are the two places that keep those doors open.
