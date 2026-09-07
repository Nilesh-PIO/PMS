namespace PMS.Domain.Enums;

/// <summary>
/// Whether a patient record is still offered for new work
/// (planning-pms-verification.md, section 4; brainstorm E-33).
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no <c>Deleted</c> member, and that absence is deliberate.</b> Plan section 4 names
/// it directly: a patient has <em>no hard-delete endpoint</em> while any visit references them.
/// A deleted patient takes a consultation history with them, and the clinic would have no way to
/// answer "what did we prescribe this person in 2026". F-8 turns a record off with
/// <see cref="Inactive"/> and a reason; nothing removes it.
/// </para>
/// <para>
/// Numbering starts at 1 so that <c>default(PatientStatus)</c> is not silently a valid state. A
/// row that reached the database with 0 in this column got there without going through
/// <c>PatientService</c>, and it should look wrong rather than look active.
/// </para>
/// </remarks>
public enum PatientStatus
{
    /// <summary>Never persisted. Exists so an unset value is visibly unset.</summary>
    Unspecified = 0,

    /// <summary>Registered and available for appointments and consultations.</summary>
    Active = 1,

    /// <summary>
    /// Retired from new work by F-8, with <c>Patient.InactiveReason</c> recording why. Every
    /// historical visit, prescription and appointment stays exactly as it was.
    /// </summary>
    Inactive = 2,
}
