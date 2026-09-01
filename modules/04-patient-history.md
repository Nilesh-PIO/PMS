# Module 04 — Patient History

Source: `BRD/Doc_BRD.md` → Functional Requirements → Patient History.

## Requirements

- View previous visits.
- Access:
  - Vitals
  - Complaints
  - Diagnosis
  - Prescriptions
- Filter by date.

## Relevant success criteria

- Patient search and history retrieval within 2–5 seconds (see [00-overview.md](00-overview.md)).

## Related modules

- [03-consultation-workflow.md](03-consultation-workflow.md) — this module displays what that one records.
- [01-patient-management.md](01-patient-management.md) — history is scoped to one patient's record.

## Notes

The BRD doesn't specify whether "filter by date" means a single visit date or a range, or whether an in-progress/unfinished consultation appears here. See `doc/brainstorm-pms-verification.md` and `doc/planning-pms-verification.md` for how this was resolved.
