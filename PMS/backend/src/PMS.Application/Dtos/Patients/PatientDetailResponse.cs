namespace PMS.Application.Dtos.Patients;

/// <summary>
/// The full patient profile (planning-pms-verification.md, F-5 point 3).
/// </summary>
/// <remarks>
/// <para>
/// Everything <see cref="PatientResponse"/> carries, plus the values that only matter once you have
/// deliberately opened one person's record — the full phone number, the alternate contact, and the
/// raw age inputs.
/// </para>
/// <para>
/// <b>Both the formatted age and the raw fields are sent.</b> <see cref="AgeDisplay"/> is what is
/// shown, so screen and print agree; <see cref="DateOfBirth"/>, <see cref="ApproxAgeYears"/> and
/// <see cref="AgeRecordedOn"/> are also present because F-8's edit form has to load the values that
/// were actually stored, and re-deriving them from a display string would be guesswork.
/// </para>
/// </remarks>
/// <param name="Id">The patient's identifier.</param>
/// <param name="FullName">The name exactly as entered.</param>
/// <param name="NormalizedName">
/// The matching form (E-60). Returned so the duplicate behaviour F-6 builds on is inspectable
/// rather than invisible — and so an acceptance test can assert it without reaching into the
/// database.
/// </param>
/// <param name="DateOfBirth">Date of birth, when known.</param>
/// <param name="ApproxAgeYears">Approximate age, when the date of birth is not known.</param>
/// <param name="AgeRecordedOn">The date that approximation was taken.</param>
/// <param name="AgeDisplay">The same formatted age the summary and the prescription show.</param>
/// <param name="Gender">The recorded gender value, or null.</param>
/// <param name="PrimaryPhone">The phone exactly as entered, or null.</param>
/// <param name="PhoneTail">Last four digits, or null.</param>
/// <param name="AltContact">Alternate contact, or null.</param>
/// <param name="RegisteredUtc">When the record was created.</param>
/// <param name="Status">"Active" or "Inactive".</param>
/// <param name="InactiveReason">Why it was retired (F-8), or null.</param>
/// <param name="MergedIntoPatientId">The survivor this record was merged into (F-6), or null.</param>
/// <param name="IsProfileIncomplete">True when <see cref="MissingFields"/> is non-empty.</param>
/// <param name="MissingFields">
/// The specific things that are missing — <c>"phone"</c>, <c>"gender"</c>, <c>"age"</c>. Naming
/// them turns "Profile incomplete" from a nag into something the physician can act on in one look
/// (E-8, E-20).
/// </param>
public sealed record PatientDetailResponse(
    Guid Id,
    string FullName,
    string NormalizedName,
    DateOnly? DateOfBirth,
    int? ApproxAgeYears,
    DateOnly? AgeRecordedOn,
    string AgeDisplay,
    string? Gender,
    string? PrimaryPhone,
    string? PhoneTail,
    string? AltContact,
    DateTimeOffset RegisteredUtc,
    string Status,
    string? InactiveReason,
    Guid? MergedIntoPatientId,
    bool IsProfileIncomplete,
    IReadOnlyList<string> MissingFields);
