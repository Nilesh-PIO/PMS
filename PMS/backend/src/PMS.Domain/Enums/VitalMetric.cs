namespace PMS.Domain.Enums;

/// <summary>
/// The measurements a plausibility threshold can be set against
/// (planning-pms-verification.md, section 4 and F-4; brainstorm E-12).
/// </summary>
/// <remarks>
/// <para>
/// <b>This enum names measurements. It does not carry a single clinical number</b> - no default,
/// no range, no unit conversion. Thresholds are rows in <c>VitalRangeSetting</c> entered by the
/// physician, and the system enforces only what it is given (plan section 7,
/// "Clinical-rule boundary"; E-12, C-31, REC-3).
/// </para>
/// <para>
/// ASSUMPTION (plan F-4 point 2 / section 4 name the enum but not its members): blood pressure is
/// <b>two</b> metrics rather than one, because it is stored as two integers
/// (<c>VisitVitals.BpSystolic</c> / <c>BpDiastolic</c>) and the brainstorm's own example of an
/// implausible reading is <c>400/0</c> (E-12) - a high systolic and a low diastolic in the same
/// reading. One shared threshold could not express that.
/// </para>
/// <para>
/// Temperature carries no unit here: the clinic's unit is chosen once in
/// <c>ClinicProfile.TemperatureUnit</c> (E-24), so a threshold of 42 means 42 in whatever unit
/// this clinic uses. Changing the clinic unit does not convert existing thresholds, exactly as it
/// does not convert temperatures already recorded - the settings screen says so out loud.
/// </para>
/// </remarks>
public enum VitalMetric
{
    /// <summary>Never valid. Exists so a zero column can never masquerade as a real metric.</summary>
    Unspecified = 0,

    /// <summary>Body temperature, in the clinic's configured unit (E-24).</summary>
    Temperature = 1,

    /// <summary>Systolic blood pressure, always mmHg (plan F-4 point 1, Q-10).</summary>
    BloodPressureSystolic = 2,

    /// <summary>Diastolic blood pressure, always mmHg.</summary>
    BloodPressureDiastolic = 3,

    /// <summary>Pulse, beats per minute.</summary>
    PulseBpm = 4,
}
