# Business Requirements Document

## Product Goal

Build a simple, web-based Patient Management Application for a general physician to efficiently manage daily clinical activities such as appointment scheduling, patient records, complaints, diagnosis, and medication.

The goal is to reduce manual paperwork, improve consultation efficiency, and maintain accurate, easily accessible patient history.

---

## Users and Stakeholders

**Primary Users:**
- General Physician (Single User)

**Secondary Users:**
- None (Receptionist access not included in Phase 1)

**Stakeholders:**
- Clinic Owner (Doctor)
- Product Owner
- Development Team

---

## Problem Statement

General physicians in small clinics often rely on paper-based systems or fragmented tools to manage patient data and appointments. This results in:
- Slow patient lookup and history tracking  
- Risk of lost or incomplete records  
- Inefficient consultation workflow  
- Lack of structured medical records  

A lightweight, web-based solution is needed to streamline and digitize daily operations.

---

## Scope

The first version of the application will include:

- Web-based access (browser-based system)  
- Patient registration and profile management  
- Appointment scheduling and tracking  
- Recording patient complaints (symptoms)  
- Diagnosis documentation  
- Medication and prescription management  
- Printable prescriptions (with basic header, footer, and content)  
- Mandatory vitals capture (temperature, BP, pulse)  
- Patient visit history tracking  
- Basic search functionality  
- Data export (CSV/PDF)  

---

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

---

## Success Criteria

- Doctor can complete a consultation record within 2–3 minutes  
- Patient search and history retrieval within 2–5 seconds  
- At least 80% reduction in paper usage  
- Smooth generation and printing of prescriptions  
- Successful export of data in CSV/PDF format  
- High usability with minimal training required  

---

## Functional Requirements

### Patient Management
- Add, edit, and view patient details  
- Capture:
  - Name  
  - Age / DOB  
  - Gender  
  - Contact details  
- Search patients by name or phone number  

---

### Appointment Management
- Schedule appointments  
- View daily appointment list  
- Update appointment status:
  - Scheduled  
  - Completed  
  - Cancelled  
  - No-show  

---

### Consultation Workflow

#### Vitals Capture (Mandatory)
- Record for every consultation:
  - Temperature  
  - Blood Pressure  
  - Pulse  

---

#### Complaints
- Enter patient symptoms (free text)

---

#### Diagnosis
- Record diagnosis notes  

---

#### Medication / Prescription
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

---

### Patient History
- View previous visits  
- Access:
  - Vitals  
  - Complaints  
  - Diagnosis  
  - Prescriptions  
- Filter by date  

---

### Search & Navigation
- Quick patient search  
- View recent patients  
- Easy navigation between patient profile and visits  

---

### Data Export
- Export patient or visit data as:
  - CSV  
  - PDF  

---

## Non-Functional Requirements

- **Usability:**  
  Simple, minimal UI optimized for fast data entry during consultations  

- **Performance:**  
  - Page load time < 2 seconds  
  - Fast patient search and retrieval  

- **Reliability:**  
  - No data loss  
  - Regular automated backups  

- **Security:**  
  - Secure login (single user authentication)  
  - Data encryption (at rest and in transit)  

- **Scalability:**  
  - Designed for a single clinic with moderate patient volume  

- **Compatibility:**  
  - Works on modern web browsers (Chrome, Edge, Safari)  

---

## Open Questions

- None (all major product decisions defined for Phase 1)