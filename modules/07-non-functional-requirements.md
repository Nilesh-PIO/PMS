# Module 07 — Non-Functional Requirements

Source: `BRD/Doc_BRD.md` → Non-Functional Requirements. These apply across every functional module ([01](01-patient-management.md)–[06](06-data-export.md)), not to any single one.

## Usability

Simple, minimal UI optimized for fast data entry during consultations.

## Performance

- Page load time < 2 seconds.
- Fast patient search and retrieval.

## Reliability

- No data loss.
- Regular automated backups.

## Security

- Secure login (single user authentication).
- Data encryption (at rest and in transit).

## Scalability

- Designed for a single clinic with moderate patient volume.

## Compatibility

- Works on modern web browsers (Chrome, Edge, Safari).

## Notes

Several of these are stated as absolutes or without a measurable baseline (e.g. "no data loss," "high usability with minimal training" in the overview's success criteria) — `doc/brainstorm-pms-verification.md`'s "Challenging the BRD" section reframes these into testable targets, and `doc/planning-pms-verification.md` states how each is implemented (e.g. the consultation autosave/backup strategy, the auth mechanism, encryption approach).
