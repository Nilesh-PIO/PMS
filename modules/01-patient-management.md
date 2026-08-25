# Module 01 — Patient Management

Source: `BRD/Doc_BRD.md` → Functional Requirements → Patient Management.

## Requirements

- Add, edit, and view patient details.
- Capture:
  - Name
  - Age / DOB
  - Gender
  - Contact details
- Search patients by name or phone number.

## Relevant success criteria

- Patient search and history retrieval within 2–5 seconds (see [00-overview.md](00-overview.md)).

## Related modules

- [05-search-navigation.md](05-search-navigation.md) — the BRD's "quick patient search" and "recent patients" features build on this module's data.
- [04-patient-history.md](04-patient-history.md) — a patient's visit history is anchored to the record created here.

## Notes

The BRD does not specify field formats, uniqueness rules, or a duplicate-patient policy for this module — see `doc/brainstorm-pms-verification.md` for the edge-case analysis (duplicate identity, DOB vs. age, missing contact details) and `doc/planning-pms-verification.md` for the concrete data model and API design this module was turned into.
