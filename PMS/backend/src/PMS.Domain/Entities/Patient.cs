using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// A person the clinic treats (planning-pms-verification.md, section 4 and F-5; brainstorm C-18,
/// C-19, E-8, E-9, E-13, E-20, E-21, E-59, E-60).
/// </summary>
/// <remarks>
/// <para>
/// <b>One free-text name field, not first + last (C-18, E-13).</b> A required-surname design
/// rejects real patients — mononyms are ordinary in this setting, and the moment a form demands a
/// surname the person at the desk invents one. The single field is the whole name as the patient
/// gives it; <see cref="NormalizedName"/> is the derived form used for matching.
/// </para>
/// <para>
/// <b>Age is never stored as a bare number (C-19, E-9).</b> This is the quiet member of the
/// mutable-history class: a record reading "34" gives no way to know whether that meant 2026 or
/// 2019, so the record silently corrupts itself just by time passing. Two shapes are allowed and
/// no third:
/// <list type="bullet">
///   <item><description><see cref="DateOfBirth"/> alone, when it is known — a fact that stays
///   true.</description></item>
///   <item><description><see cref="ApproxAgeYears"/> together with <see cref="AgeRecordedOn"/>,
///   when it is not — "about 40, as of this date", which is honest about being an estimate and can
///   still be aged forward correctly.</description></item>
/// </list>
/// A bare <see cref="ApproxAgeYears"/> with no <see cref="AgeRecordedOn"/> is rejected by
/// <c>PatientService</c> <em>and</em> by a database check constraint, because this rule is the one
/// that cannot be re-derived later if it is ever broken.
/// </para>
/// <para>
/// <b>Nothing here is ever hard-deleted (E-33).</b> <see cref="Status"/> retires a record (F-8) and
/// <see cref="MergedIntoPatientId"/> points a duplicate at its survivor (F-6) — a pointer, not a
/// rewrite, so the losing record's history is still readable. Neither destroys a row.
/// </para>
/// </remarks>
public class Patient
{
    /// <summary>
    /// Client-opaque identity. A <c>Guid</c> rather than an identity int so a patient id in a URL
    /// or an exported file does not also disclose how many patients the clinic has.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The patient's name exactly as it was entered, including the script it was typed in (E-57).
    /// This is what is displayed and printed; it is never overwritten by the normalized form.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="FullName"/> trimmed, with internal whitespace collapsed and case folded
    /// (REC-18, E-60). Written by <c>PatientService</c> on every save and never by a client.
    /// </summary>
    /// <remarks>
    /// Exists so that <c>"  Ravi   Kumar  "</c> and <c>"Ravi Kumar"</c> are one person rather than
    /// two. Whitespace-variant names are called out in the brainstorm as a cheap and large
    /// contributor to split histories (E-25); this column is the cheap half of that fix, and F-6
    /// builds the similarity check on top of it.
    /// </remarks>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>Date of birth when known. Never in the future.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Approximate age in whole years when the date of birth is unknown (E-21). Only ever set
    /// together with <see cref="AgeRecordedOn"/>, and never together with
    /// <see cref="DateOfBirth"/>.
    /// </summary>
    public int? ApproxAgeYears { get; set; }

    /// <summary>
    /// The date <see cref="ApproxAgeYears"/> was recorded, which is what stops it from becoming a
    /// lie. Displayed as part of the age — "~40 (recorded 2026)" — so an estimate can never
    /// masquerade as an exact age.
    /// </summary>
    public DateOnly? AgeRecordedOn { get; set; }

    /// <summary>
    /// The gender value as it stood when this patient was registered, drawn from F-4's
    /// <see cref="SettingOption"/> list.
    /// </summary>
    /// <remarks>
    /// A <em>string</em> and not a foreign key, deliberately, and F-4's entity documents the other
    /// half of the same decision: the physician may retire a gender option in 2028, and a record
    /// written in 2026 must still read exactly as it was recorded rather than turning into a
    /// dangling id. The list is what constrains the spelling at write time (C-20); the column
    /// preserves what was written.
    /// </remarks>
    public string? Gender { get; set; }

    /// <summary>
    /// Phone number as the physician typed it, formatting and all (E-59). Optional: a patient with
    /// no phone is a real patient, and refusing to register them is worse than an incomplete
    /// profile (Q-7, E-20).
    /// </summary>
    public string? PrimaryPhone { get; set; }

    /// <summary>
    /// <see cref="PrimaryPhone"/> reduced to digits, for matching (E-59). Written by
    /// <c>PatientService</c>. Null when no phone was given — never an empty string, so "no phone"
    /// and "a phone with no digits in it" stay distinguishable.
    /// </summary>
    public string? NormalizedPhone { get; set; }

    /// <summary>
    /// The key F-6 matches phone numbers on — <see cref="NormalizedPhone"/> with one leading trunk
    /// zero removed and reduced to its last ten digits. Written by <c>PatientService</c>, never by a
    /// client. Null when there is no phone, or when it is too short to identify anyone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stored column rather than an expression over <see cref="NormalizedPhone"/>, for two
    /// reasons. It is <em>indexable</em>, so the duplicate check is an index seek rather than a scan
    /// of every patient on every registration; and it keeps the lossy form separate from the
    /// faithful one, so the digits the physician actually typed are never overwritten by a matching
    /// decision.
    /// </para>
    /// <para>
    /// The rule itself, and why it exists at all, lives on <c>PatientNormalizer.PhoneMatchKey</c> —
    /// it is the answer to Q-13's "when are two phone numbers the same number", and it is what makes
    /// <c>+91 98765 43210</c> and <c>098765 43210</c> one patient rather than two.
    /// </para>
    /// </remarks>
    public string? PhoneMatchKey { get; set; }

    /// <summary>A second way to reach the patient — a relative, a neighbour, a landline.</summary>
    public string? AltContact { get; set; }

    /// <summary>When the record was created. Set from <c>IClock</c>, never by the client.</summary>
    public DateTimeOffset RegisteredUtc { get; set; }

    /// <summary>Active, or retired by F-8. Never deleted.</summary>
    public PatientStatus Status { get; set; } = PatientStatus.Active;

    /// <summary>Why the record was retired. F-8 writes it; F-5 always leaves it null.</summary>
    public string? InactiveReason { get; set; }

    /// <summary>
    /// Set by F-6 when this record is found to be a duplicate of another, pointing at the survivor.
    /// </summary>
    /// <remarks>
    /// Plan section 4: this exists so Phase-2 merge tooling is possible <em>without destroying
    /// history</em> (E-26). F-5 never writes it; the column is here from the start because adding
    /// the pointer later would not retroactively give the earlier duplicates anywhere to point.
    /// </remarks>
    public Guid? MergedIntoPatientId { get; set; }

    /// <summary>
    /// Idempotency token for the create that produced this row (E-43, E-46).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Carries a <em>filtered</em> unique index — unique where it is not null — so replaying the
    /// same submission cannot insert a second patient, while the many rows that predate a token, or
    /// arrive from a caller that did not send one, are all free to be null. SQL Server's plain
    /// unique index permits only a single null, which is why the filter is load-bearing rather than
    /// tidy.
    /// </para>
    /// <para>
    /// The index is the guarantee, not the service's read-before-write: two clicks 20 ms apart can
    /// both find nothing and both try to insert, and only a database constraint is actually
    /// deciding at that point.
    /// </para>
    /// </remarks>
    public Guid? SubmissionId { get; set; }

    /// <summary>
    /// SQL Server <c>rowversion</c>. F-8's edit path uses it so that two tabs editing one patient
    /// produce a visible 409 rather than a silent last-write-wins that discards an edit.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
