namespace PMS.Application.Dtos.Patients;

/// <summary>
/// One patient already on file who might be the person being registered
/// (planning-pms-verification.md, F-6 points 3 and 4; brainstorm REC-2, REC-12, RSK-12, E-26,
/// E-27, E-28).
/// </summary>
/// <remarks>
/// <para>
/// <b>Every field here exists to prevent a decision being made on a name alone.</b> RSK-12 —
/// "wrong-patient selection from a name-only picker" — is rated Critical, and this is a screen where
/// the physician is being asked "is this the same person?" about a list of near-identical names. So
/// the row carries the phone tail, the age, the date of birth and the last visit date, which are
/// exactly the four things that tell two Ravi Kumars apart (REC-12, E-28). None of them is optional
/// in the DTO; a null renders as "not recorded" rather than as a blank the eye skips.
/// </para>
/// <para>
/// <b>Why <see cref="MatchReason"/> and <see cref="NameSimilarity"/> are on the wire.</b> A warning
/// the physician cannot interrogate is a warning they learn to click through. "Same phone number,
/// name 92% alike" is a claim they can evaluate in a second; an unexplained yellow box is not.
/// </para>
/// <para>
/// <b>Why a merged-away record still appears.</b> <see cref="MergedIntoPatientId"/> is carried
/// rather than filtered on, and a record that has been pointed at a survivor is still returned as a
/// candidate. Hiding it would hide half of a duplicate cluster from the one person able to make
/// sense of it, and this feature's whole posture is that nothing about a duplicate is destroyed or
/// concealed (E-26). The UI labels it; it is never auto-selected, because nothing here ever is.
/// </para>
/// </remarks>
/// <param name="Id">The candidate patient's identifier.</param>
/// <param name="FullName">The name exactly as it was recorded.</param>
/// <param name="PhoneTail">Last four digits of the recorded phone, or null.</param>
/// <param name="AgeDisplay">
/// The same server-formatted age string every other screen shows — "41", "~40 (recorded 2026)",
/// "3 days" or "Age not recorded".
/// </param>
/// <param name="DateOfBirth">Date of birth when known, so the row shows it as well as the age.</param>
/// <param name="Gender">The recorded gender value, or null.</param>
/// <param name="Status">"Active" or "Inactive".</param>
/// <param name="MergedIntoPatientId">
/// Set when this record has already been marked as a duplicate of another (E-26). Null normally.
/// </param>
/// <param name="LastVisitDate">
/// The date of this patient's most recent visit (REC-12, E-28).
/// <para>
/// <b>Always null until F-10.</b> There is no Visit entity in the schema yet — F-9 and F-10 build
/// it. The field is on the contract now, and rendered now as "No visits recorded", because it is a
/// disambiguating field the plan names for this dialog: adding it later would mean shipping the
/// picker row twice and re-testing it. F-10 fills it in and no consumer changes.
/// </para>
/// </param>
/// <param name="MatchReason">
/// What this row has in common with the person being registered — "phone", "date-of-birth" or
/// "phone-and-date-of-birth". A stable slug, never a sentence, so the UI decides the wording.
/// </param>
/// <param name="NameSimilarity">How alike the two normalized names are, 0 to 1.</param>
/// <param name="IsLikelyDuplicate">
/// True when this row meets the full identity rule — a shared phone or date of birth <em>and</em> a
/// name similarity of at least <c>NameSimilarity.DefaultThreshold</c> (plan F-6 point 1, Q-13).
/// <para>
/// <b>This flag is what lets one endpoint answer two questions that pull in opposite directions.</b>
/// Plan acceptance criterion 3 and E-27 require that a phone shared by three family members returns
/// <em>all three</em> — a phone number identifies a household, not a person, and someone filling in
/// a registration form genuinely wants to know that three Kumars already share this number. But the
/// identity rule for a duplicate has to be narrower than that, or every family member would raise a
/// duplicate warning about every other and the warning would be worthless inside a week (E-28).
/// </para>
/// <para>
/// So the check returns the household and marks the duplicates. Rows with this flag false are
/// context; rows with it true are the ones registration will not proceed past without a
/// confirmation.
/// </para>
/// </param>
public sealed record DuplicateCandidateResponse(
    Guid Id,
    string FullName,
    string? PhoneTail,
    string AgeDisplay,
    DateOnly? DateOfBirth,
    string? Gender,
    string Status,
    Guid? MergedIntoPatientId,
    DateOnly? LastVisitDate,
    string MatchReason,
    double NameSimilarity,
    bool IsLikelyDuplicate);

/// <summary>The stable <c>MatchReason</c> slugs, so the service and its tests cannot drift.</summary>
public static class DuplicateMatchReason
{
    /// <summary>The candidate shares the phone matching key.</summary>
    public const string Phone = "phone";

    /// <summary>The candidate shares the date of birth.</summary>
    public const string DateOfBirth = "date-of-birth";

    /// <summary>Both — the strongest signal this feature can produce.</summary>
    public const string PhoneAndDateOfBirth = "phone-and-date-of-birth";
}
