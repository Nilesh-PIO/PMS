namespace PMS.Domain.Enums;

/// <summary>
/// The doctor-configured lookup lists (planning-pms-verification.md, section 4 and F-4).
/// </summary>
/// <remarks>
/// <para>
/// The values in each list are <b>data</b>, editable by the physician; the categories themselves
/// are <b>code</b>, because each one is consumed by a specific field that has to know which list
/// to read. Adding a category is a feature, not a settings change.
/// </para>
/// <para>
/// <b><see cref="Unspecified"/> = 0 is deliberate</b>, following F-3's <c>TemperatureUnit</c>: the
/// enum is persisted as an int, and a zero left behind by a hand-run INSERT in SSMS must not be
/// able to masquerade as a real category. Every read rejects it.
/// </para>
/// </remarks>
public enum SettingCategory
{
    /// <summary>Never valid. Exists so "no category" is representable and always rejected.</summary>
    Unspecified = 0,

    /// <summary>
    /// The gender options offered when registering a patient (C-20, E-23). Seeded with
    /// <c>Female</c>, <c>Male</c>, <c>Other</c>, <c>Not stated</c>; "Not stated" can never be
    /// removed or deactivated, because forcing a guess is how wrong data gets into a record.
    /// </summary>
    Gender = 1,

    /// <summary>
    /// Why a vital could not be recorded (§5.2, REC-3, E-18). Seeded with
    /// <c>Equipment unavailable</c>, <c>Patient declined</c>, <c>Not clinically indicated</c>,
    /// <c>Other</c>. F-11's mandatory-or-reason escape hatch reads this list, which is why the
    /// service refuses to let it be emptied - an empty list turns the escape hatch back into the
    /// dead end it exists to remove.
    /// </summary>
    VitalsNotRecordedReason = 2,
}
