using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// One entry in a doctor-configured lookup list - a gender option, or a reason a vital could not
/// be recorded (planning-pms-verification.md, section 4 and F-4; brainstorm C-20, E-23, E-18).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this table exists at all (C-20).</b> A free-text gender field guarantees <c>M</c>,
/// <c>Male</c> and <c>male</c> in the same column within a month, and every later filter, export
/// and count is then wrong in a way nobody notices. Patient rows record a value drawn from this
/// list, so the column has one spelling per meaning.
/// </para>
/// <para>
/// <b>Why deactivation is a flag and not a delete.</b> A patient row stores the gender
/// <em>string</em>, not a foreign key to this table (see plan section 4: <c>Patient.Gender</c> is
/// <c>string?</c>). That is deliberate - a consultation from 2026 must still read exactly as it
/// was recorded even if the list is edited in 2028. It also means a deleted option would leave
/// historical rows displaying a value that no longer appears anywhere in settings, which reads as
/// data corruption to whoever finds it. <see cref="IsActive"/> keeps the value present and
/// explicable while removing it from the dropdown.
/// </para>
/// </remarks>
public class SettingOption
{
    public int Id { get; set; }

    /// <summary>Which list this entry belongs to.</summary>
    public SettingCategory Category { get; set; } = SettingCategory.Unspecified;

    /// <summary>
    /// The value as it is shown to the physician and stored on the patient or vitals row. Compared
    /// case-insensitively when the list is edited, so <c>Male</c> and <c>male</c> cannot both
    /// exist - the duplicate this whole table exists to prevent.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Position in the dropdown, 1-based. Set from the order the physician submits, never typed
    /// by hand, so two options can never claim the same slot through a form.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether the option is offered for new entries. <c>false</c> hides it from every dropdown
    /// while leaving historical values intact - see the class remarks.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
