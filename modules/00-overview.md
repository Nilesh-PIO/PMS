# Module 00 — Overview

Source: `BRD/Doc_BRD.md`. This is the canonical BRD, reorganized module by module for easier navigation — nothing here overrides it; if this file and the BRD ever disagree, the BRD wins.

## Product Goal

Build a simple, web-based Patient Management Application for a general physician to efficiently manage daily clinical activities such as appointment scheduling, patient records, complaints, diagnosis, and medication.

The goal is to reduce manual paperwork, improve consultation efficiency, and maintain accurate, easily accessible patient history.

## Users and Stakeholders

**Primary Users:**
- General Physician (Single User)

**Secondary Users:**
- None (Receptionist access not included in Phase 1)

**Stakeholders:**
- Clinic Owner (Doctor)
- Product Owner
- Development Team

## Problem Statement

General physicians in small clinics often rely on paper-based systems or fragmented tools to manage patient data and appointments. This results in:
- Slow patient lookup and history tracking
- Risk of lost or incomplete records
- Inefficient consultation workflow
- Lack of structured medical records

A lightweight, web-based solution is needed to streamline and digitize daily operations.

## Scope (Phase 1)

- Web-based access (browser-based system)
- Patient registration and profile management — see [01-patient-management.md](01-patient-management.md)
- Appointment scheduling and tracking — see [02-appointment-management.md](02-appointment-management.md)
- Recording patient complaints, diagnosis, vitals, and medications — see [03-consultation-workflow.md](03-consultation-workflow.md)
- Printable prescriptions — see [03-consultation-workflow.md](03-consultation-workflow.md)
- Patient visit history tracking — see [04-patient-history.md](04-patient-history.md)
- Basic search functionality — see [05-search-navigation.md](05-search-navigation.md)
- Data export (CSV/PDF) — see [06-data-export.md](06-data-export.md)

## Out of Scope

The following will NOT be included in the initial release:

- Receptionist or multi-user access
- Billing and invoicing
- Insurance processing
- Integration with labs or pharmacies
- AI-based diagnosis or recommendations
- Offline functionality
- Mobile application
- Advanced analytics and reporting
- Multi-doctor or multi-clinic support
- Follow-up alerts/reminders

## Success Criteria

- Doctor can complete a consultation record within 2–3 minutes
- Patient search and history retrieval within 2–5 seconds
- At least 80% reduction in paper usage
- Smooth generation and printing of prescriptions
- Successful export of data in CSV/PDF format
- High usability with minimal training required

## Open Questions (per the BRD)

The BRD states: "None (all major product decisions defined for Phase 1)." For a rigorous edge-case-driven review of that claim, see `doc/brainstorm-pms-verification.md`.

## Module index

| Module | Covers |
|---|---|
| [01-patient-management.md](01-patient-management.md) | Patient registration, profile fields, search by name/phone |
| [02-appointment-management.md](02-appointment-management.md) | Scheduling, daily list, appointment status |
| [03-consultation-workflow.md](03-consultation-workflow.md) | Vitals, complaints, diagnosis, medication/prescription, printable prescription |
| [04-patient-history.md](04-patient-history.md) | Visit history, filtering by date |
| [05-search-navigation.md](05-search-navigation.md) | Quick search, recent patients, navigation |
| [06-data-export.md](06-data-export.md) | CSV/PDF export |
| [07-non-functional-requirements.md](07-non-functional-requirements.md) | Usability, performance, reliability, security, scalability, compatibility — apply across every module above |
| [08-authentication-authorization.md](08-authentication-authorization.md) | Single-user login, access model — consolidated from Users and Stakeholders, Security NFR, and Out of Scope |

For the deeper edge-case analysis and the concrete implementation plan built from this BRD, see `doc/brainstorm-pms-verification.md` and `doc/planning-pms-verification.md`.
