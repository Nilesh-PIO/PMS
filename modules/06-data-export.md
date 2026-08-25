# Module 06 — Data Export

Source: `BRD/Doc_BRD.md` → Functional Requirements → Data Export.

## Requirements

- Export patient or visit data as:
  - CSV
  - PDF

## Relevant success criteria

- Successful export of data in CSV/PDF format.

## Related modules

- [01-patient-management.md](01-patient-management.md) and [04-patient-history.md](04-patient-history.md) — export operates on records held in these modules.

## Notes

The BRD gives this module three lines despite it being one of the highest-privacy-risk features (exported files leaving the application entirely). It doesn't define export scope (single patient vs. full database), CSV-injection handling, or an audit trail for exports. See `doc/brainstorm-pms-verification.md` and `doc/planning-pms-verification.md` for the adopted scoping and safeguards.
