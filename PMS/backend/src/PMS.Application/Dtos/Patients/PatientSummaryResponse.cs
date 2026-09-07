namespace PMS.Application.Dtos.Patients;

/// <summary>
/// One row in a patient picker (planning-pms-verification.md, F-7 point 3; brainstorm E-28,
/// REC-12, RSK-12).
/// </summary>
/// <remarks>
/// <para>
/// <b>This DTO is a safety mechanism, not a view model.</b> RSK-12 — "wrong-patient selection from
/// a name-only picker" — is rated <em>Critical</em>, and E-28 states the rule plainly: never show a
/// name alone in a selection list. The way to make that rule hold is to make a name-only row
/// impossible to build, so every field a physician needs in order to tell two people apart is
/// non-optional on the wire. A client cannot render a name-only row from this type without
/// deliberately throwing information away.
/// </para>
/// <para>
/// Plan F-7 point 3 requires <c>fullName</c>, <c>phoneTail</c>, <c>ageDisplay</c>,
/// <c>lastVisitDate</c> and <c>status</c>. All five are here. <see cref="Id"/> is added because the
/// picker's whole purpose is to navigate to one; <see cref="RegisteredOn"/>,
/// <see cref="IsMerged"/>, <see cref="IsProfileIncomplete"/> and <see cref="MatchKind"/> are added
/// for the reasons given on each.
/// </para>
/// </remarks>
/// <param name="Id">The patient to open.</param>
/// <param name="FullName">The name exactly as it was entered.</param>
/// <param name="PhoneTail">
/// Last four digits, or null when no phone was recorded. The strongest everyday disambiguator
/// between two people with one name, and materially less exposure than the whole number on a screen
/// that faces a waiting room.
/// </param>
/// <param name="AgeDisplay">
/// The same server-formatted age the profile and F-14's printed prescription show — <c>"41"</c>,
/// <c>"~40 (recorded 2026)"</c>, <c>"3 days"</c>, <c>"Age not recorded"</c>.
/// </param>
/// <param name="Gender">The recorded gender value, or null.</param>
/// <param name="LastVisitDate">
/// The date of this patient's most recent visit.
/// <para>
/// <b>ASSUMPTION (plan F-7 point 2), and it is a temporary null.</b> The plan describes this as
/// "a projected read (computed in the query, not stored)", which it will be — but the
/// <c>Visit</c> entity is <b>F-10</b> and does not exist yet, so on this branch the value is always
/// null. The field ships now rather than being added later so that the picker's contract and every
/// client written against it are already the right shape when F-10 lands: F-10 fills this in and no
/// DTO, no client type and no test signature changes. Until then
/// <see cref="RegisteredOn"/> carries the "when did we last deal with this person" axis, so E-28's
/// disambiguation requirement is met today rather than deferred.
/// </para>
/// </param>
/// <param name="RegisteredOn">
/// The date the record was created. Present so a picker row has a date to show while
/// <see cref="LastVisitDate"/> is null (see above) — two patients with the same name, the same age
/// and no phone between them are still told apart by when each was registered.
/// </param>
/// <param name="Status">"Active" or "Inactive".</param>
/// <param name="IsMerged">
/// True when this record points at a survivor (F-6's <c>MergedIntoPatientId</c>). Surfaced so that
/// a merged record found through the "include inactive" toggle is visibly a merged record rather
/// than an ordinary choice — the one thing worse than not finding a patient is attaching a
/// prescription to the half of a split history nobody reads.
/// </param>
/// <param name="IsProfileIncomplete">
/// True when the profile is missing a phone, an age or a gender (E-8). Carried into the picker so
/// the gap is visible at the moment someone is about to choose the record, not only once they open
/// it.
/// </param>
/// <param name="MatchKind">
/// Why this row is in the result: <c>"Name"</c>, <c>"Phone"</c> or <c>"SimilarName"</c>.
/// <para>
/// <b>Load-bearing, not decoration.</b> <c>"SimilarName"</c> means the exact search found nothing
/// and this row came back from the fuzzy fallback (E-30) — it is a <em>guess</em>. A guess rendered
/// identically to an exact hit is a new wrong-patient path, so the reason travels with the row and
/// the UI labels it.
/// </para>
/// </param>
public sealed record PatientSummaryResponse(
    Guid Id,
    string FullName,
    string? PhoneTail,
    string AgeDisplay,
    string? Gender,
    DateOnly? LastVisitDate,
    DateOnly RegisteredOn,
    string Status,
    bool IsMerged,
    bool IsProfileIncomplete,
    string MatchKind);

/// <summary>The values <see cref="PatientSummaryResponse.MatchKind"/> can take.</summary>
public static class PatientMatchKind
{
    /// <summary>Matched on the name, exactly as typed.</summary>
    public const string Name = "Name";

    /// <summary>Matched on the phone digits, including the last-4 case (E-59).</summary>
    public const string Phone = "Phone";

    /// <summary>
    /// Matched only by similarity, after an exact search returned nothing (E-30). A suggestion.
    /// </summary>
    public const string SimilarName = "SimilarName";

    /// <summary>Not a search result at all — a row from the recent-patients list.</summary>
    public const string Recent = "Recent";
}
