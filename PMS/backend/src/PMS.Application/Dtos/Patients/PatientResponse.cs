namespace PMS.Application.Dtos.Patients;

/// <summary>
/// The summary shape of a patient — enough to identify one person and not mistake them for
/// another (planning-pms-verification.md, F-5 point 3; brainstorm REC-12, RSK-12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type carries more than a name and an id.</b> REC-12 exists because of RSK-12,
/// "wrong-patient selection from a name-only picker", whose impact is rated Critical: the clinic
/// has several Ravi Kumars, and a list showing only names offers no way to tell them apart before
/// a prescription is written against the wrong history. So every summary of a patient anywhere in
/// this application carries name <em>plus</em> a phone tail <em>plus</em> an age — and this DTO is
/// where that becomes structural rather than a habit each screen has to remember. F-7 builds its
/// picker from this exact type.
/// </para>
/// <para>
/// <b>The phone tail, not the phone.</b> <see cref="PhoneTail"/> is the last four digits, which is
/// what disambiguates two people with one name while putting materially less contact detail on a
/// screen that may be visible from the waiting side of the desk. The full number is on the detail
/// response, which is the record you have deliberately opened.
/// </para>
/// <para>
/// <b><see cref="AgeDisplay"/> is computed on the server, on purpose.</b> The alternative is for
/// the React app and F-14's PDF renderer to each format an age, and the first time they disagree —
/// "~40" on screen against "40" on the printed prescription — the printed document is the one the
/// patient walks out with. One formatter, one string, both consumers.
/// </para>
/// </remarks>
/// <param name="Id">The patient's identifier.</param>
/// <param name="FullName">The name exactly as it was entered.</param>
/// <param name="PhoneTail">Last four digits of the phone, or null when no phone was recorded.</param>
/// <param name="AgeDisplay">
/// Human-readable age — <c>"41"</c>, <c>"~40 (recorded 2026)"</c>, <c>"3 days"</c>, or
/// <c>"Age not recorded"</c>. Never a bare number that hides which of those it is.
/// </param>
/// <param name="Gender">The recorded gender value, or null if it was not answered.</param>
/// <param name="Status">"Active" or "Inactive".</param>
/// <param name="IsProfileIncomplete">
/// True when something worth chasing is missing. Surfaced rather than hidden, so a thin record
/// looks thin instead of looking finished (E-8).
/// </param>
public sealed record PatientResponse(
    Guid Id,
    string FullName,
    string? PhoneTail,
    string AgeDisplay,
    string? Gender,
    string Status,
    bool IsProfileIncomplete);
