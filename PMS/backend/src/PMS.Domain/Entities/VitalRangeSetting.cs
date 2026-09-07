using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// A plausibility threshold the physician has defined for one vital
/// (planning-pms-verification.md, section 4 and F-4; brainstorm E-12, REC-3, C-31).
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule this type is built around: the system authors no clinical range.</b> Plan section 7
/// ("Clinical-rule boundary") states it as a review item on every change touching F-11 or F-13, and
/// F-4 point 1 states it again - "this plan does not author any clinical range; the system enforces
/// only what the doctor enters". So there is no default here, no seeded row, and no constant
/// anywhere in this codebase holding a temperature, pulse or blood-pressure bound.
/// </para>
/// <para>
/// <b><see cref="WarnLow"/> and <see cref="WarnHigh"/> are independently nullable, and null means
/// silence.</b> A blank threshold fires no warning at all - it is not zero, and it is not
/// "unbounded" in a way that could trip a comparison. Half-configured is a legitimate state: a
/// physician who cares about a 42-degree temperature and not about a low one sets only the upper
/// bound.
/// </para>
/// <para>
/// <b>Warnings are soft (E-12).</b> Nothing on this entity or in the service that reads it can
/// refuse a save. A hard block on an implausible-looking value is how a real reading gets rounded
/// to something the software will accept, which is worse than the odd typo: the typo is visible,
/// the fabrication is not.
/// </para>
/// </remarks>
public class VitalRangeSetting
{
    public int Id { get; set; }

    /// <summary>Which measurement this threshold applies to.</summary>
    public VitalMetric Metric { get; set; } = VitalMetric.Unspecified;

    /// <summary>
    /// Warn below this value. <c>null</c> means no lower warning - see the class remarks. In the
    /// clinic's configured temperature unit for <see cref="VitalMetric.Temperature"/>, mmHg for
    /// blood pressure, bpm for pulse.
    /// </summary>
    public decimal? WarnLow { get; set; }

    /// <summary>Warn above this value. <c>null</c> means no upper warning.</summary>
    public decimal? WarnHigh { get; set; }

    /// <summary>
    /// When the physician last changed this threshold. Kept per row rather than per table so the
    /// settings screen can say when a specific threshold was last touched.
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
