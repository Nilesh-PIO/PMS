# Module 02 — Appointment Management

Source: `BRD/Doc_BRD.md` → Functional Requirements → Appointment Management.

## Requirements

- Schedule appointments.
- View daily appointment list.
- Update appointment status:
  - Scheduled
  - Completed
  - Cancelled
  - No-show

## Related modules

- [01-patient-management.md](01-patient-management.md) — every appointment is booked against a patient record.
- [03-consultation-workflow.md](03-consultation-workflow.md) — the BRD does not state how an appointment relates to the consultation it produces; this is the module that consultation link would connect to.

## Notes

The BRD lists the four statuses but does not define the transitions between them (e.g. whether a `No-show` can later become `Completed`, or what happens when a `Scheduled` appointment's date passes unattended). See `doc/brainstorm-pms-verification.md` for the state-machine analysis and `doc/planning-pms-verification.md` for the adopted transition rules.
