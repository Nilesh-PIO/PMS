namespace PMS.Application.Dtos.Patients;

/// <summary>
/// The registration form as it arrives from the client
/// (planning-pms-verification.md, F-5 point 3).
/// </summary>
/// <remarks>
/// <para>
/// A separate type from the <c>Patient</c> entity, per plan section 2: no entity crosses the wire.
/// That is not ceremony here — the entity carries <c>NormalizedName</c>, <c>NormalizedPhone</c>,
/// <c>RegisteredUtc</c>, <c>Status</c> and <c>MergedIntoPatientId</c>, none of which a client may
/// set. Leaving them off this type means a malicious or careless caller cannot try.
/// </para>
/// <para>
/// Every property is nullable and nothing is annotated <c>[Required]</c>. Validation happens in
/// <c>PatientService</c> instead, for one reason: the age rule (E-9) is a relationship between
/// three fields, not a property of any one of them, and splitting it across attributes and service
/// code would leave two places to look and one of them incomplete.
/// </para>
/// </remarks>
public class CreatePatientRequest
{
    /// <summary>
    /// The patient's whole name in one field. The only genuinely required value on this form
    /// (C-18, E-13).
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>Date of birth, when known. Rejected if it is in the future (E-11).</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Approximate age in years, when the date of birth is not known (E-21). Must travel with
    /// <see cref="AgeRecordedOn"/> and must not travel with <see cref="DateOfBirth"/>.
    /// </summary>
    public int? ApproxAgeYears { get; set; }

    /// <summary>
    /// The date the approximate age was taken. The client normally omits this and the server
    /// stamps today's date from <c>IClock</c>; it is accepted here so that a back-dated
    /// registration stays possible without a second endpoint.
    /// </summary>
    public DateOnly? AgeRecordedOn { get; set; }

    /// <summary>
    /// A value from F-4's active <c>Gender</c> option list. Optional — "not answered" and
    /// "answered as Not stated" are different states and both are allowed.
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>Phone number in whatever format the physician uses (E-59). Optional (Q-7, E-20).</summary>
    public string? PrimaryPhone { get; set; }

    /// <summary>An alternate contact — a relative or neighbour. Optional.</summary>
    public string? AltContact { get; set; }

    /// <summary>
    /// A token identifying <em>this</em> attempt to submit the form, so that a double-click or a
    /// retried request cannot register the same patient twice (E-43, E-46).
    /// </summary>
    /// <remarks>
    /// The client generates one per opened form (see <c>useSubmitOnce.ts</c>) and re-sends the same
    /// value on a retry. Replaying it returns the patient that was already created rather than
    /// creating another, so a retry after a timeout is safe.
    /// <para>
    /// <b>Optional, not required.</b> Omitting it is allowed and simply forgoes the guarantee — a
    /// 400 here would reject a legitimate <c>curl</c> or a future integration over a field the plan
    /// never named on this DTO. The one client that matters always sends it.
    /// </para>
    /// </remarks>
    public Guid? SubmissionId { get; set; }
}
