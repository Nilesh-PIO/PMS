# Module 03 — Consultation Workflow

Source: `BRD/Doc_BRD.md` → Functional Requirements → Consultation Workflow (Vitals Capture, Complaints, Diagnosis, Medication/Prescription). The BRD groups these four as one workflow rather than four separate modules — kept as one file here for the same reason, since they combine into a single consultation record.

## Vitals Capture (Mandatory)

Record for every consultation:
- Temperature
- Blood Pressure
- Pulse

## Complaints

- Enter patient symptoms (free text).

## Diagnosis

- Record diagnosis notes.

## Medication / Prescription

- Add medicines with:
  - Name
  - Dosage
  - Frequency
  - Duration
  - Instructions

- Generate **printable prescription** including:
  - Clinic/doctor header
  - Patient details
  - Vitals
  - Diagnosis
  - Medications
  - Footer (basic notes/signature area)

## Relevant success criteria

- Doctor can complete a consultation record within 2–3 minutes.
- Smooth generation and printing of prescriptions.

## Related modules

- [01-patient-management.md](01-patient-management.md) — a consultation is recorded against a patient.
- [02-appointment-management.md](02-appointment-management.md) — the BRD doesn't state whether a consultation requires a scheduled appointment.
- [04-patient-history.md](04-patient-history.md) — every consultation becomes part of that patient's history.

## Notes

The BRD marks vitals "mandatory" with no exception path, and doesn't define when a consultation becomes a permanent record, whether it can be edited after the prescription is printed, or what happens if it's interrupted. These are the largest gaps found in `doc/brainstorm-pms-verification.md`; the adopted design (draft/finalize lifecycle, vitals exception handling) is in `doc/planning-pms-verification.md`.
