namespace PMS.Application.Dtos.Patients;

/// <summary>
/// "Is this person already on file?" — asked before a registration is submitted
/// (planning-pms-verification.md, F-6 point 3; brainstorm REC-2, E-25, E-30).
/// </summary>
/// <remarks>
/// <para>
/// The plan's shape is <c>{fullName, phone, dateOfBirth}</c>. Deliberately <em>not</em> the same
/// type as <c>CreatePatientRequest</c>, even though the fields overlap: this request asks a
/// question and writes nothing, and giving it its own type means the check can be called from a
/// half-filled form without the caller having to satisfy registration's rules first. Asking early is
/// the entire point — a warning that arrives after the record is written is not a warning, it is a
/// report.
/// </para>
/// </remarks>
public class DuplicateCheckRequest
{
    /// <summary>The name as typed. Normalized server-side before it is compared.</summary>
    public string? FullName { get; set; }

    /// <summary>The phone as typed, in any format (E-59).</summary>
    public string? Phone { get; set; }

    /// <summary>Date of birth, when the form has one.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// A patient to leave out of the results — themselves.
    /// </summary>
    /// <remarks>
    /// <b>ASSUMPTION (plan F-6 points 3 and 6).</b> The plan's DTO sketch lists three fields, but its
    /// own test strategy for this feature names "self-exclusion on edit" as a case to cover, and F-8
    /// re-runs this check when a patient's name or phone is edited. Without this field every edit
    /// would report the patient as a duplicate of themselves, which trains the physician to dismiss
    /// the warning — the precise way a safety check stops working. Optional, so registration (which
    /// has no id yet) simply omits it.
    /// </remarks>
    public Guid? ExcludePatientId { get; set; }
}
