using PMS.Application.Dtos.Clinic;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Services;

/// <summary>
/// Turns entered vitals plus the physician's own thresholds into soft warnings
/// (planning-pms-verification.md, F-4 point 1 and point 6; brainstorm E-12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure and static on purpose.</b> It touches no database, no clock and no configuration, so
/// the rule that matters most here - "a blank threshold produces no warning" - is provable by a
/// unit test with no fixture at all, and F-11 can evaluate a whole vitals card against one fetched
/// range set without a round trip per field.
/// </para>
/// <para>
/// <b>Two things this class cannot do, by construction: refuse a value, or invent a bound.</b> It
/// returns a list. An empty list is the answer for an unconfigured clinic, which is the default
/// state of this system and stays the default state forever unless the physician types a number
/// in (F-4 acceptance criteria 3 and 5).
/// </para>
/// </remarks>
public static class VitalRangeEvaluator
{
    /// <summary>
    /// The warnings <paramref name="values"/> produce against <paramref name="ranges"/>.
    /// </summary>
    /// <param name="values">
    /// Entered values by metric. A <c>null</c> value is skipped entirely - it means the vital was
    /// not recorded (F-11 stores absence as null plus a reason, never a sentinel), and warning
    /// about a value that does not exist would be nonsense.
    /// </param>
    /// <param name="ranges">Whatever thresholds exist. Metrics with no row are silent.</param>
    /// <param name="clinicTemperatureUnit">Used only to word the message (E-24).</param>
    public static IReadOnlyList<VitalWarning> Evaluate(
        IReadOnlyDictionary<VitalMetric, decimal?> values,
        IEnumerable<VitalRangeSetting> ranges,
        TemperatureUnit clinicTemperatureUnit)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(ranges);

        var byMetric = new Dictionary<VitalMetric, VitalRangeSetting>();
        foreach (var range in ranges)
        {
            // Last row wins if the table somehow holds two for one metric. A unique index makes
            // that unreachable through the application; this keeps the read deterministic anyway,
            // because SSMS can reach the table too.
            byMetric[range.Metric] = range;
        }

        var warnings = new List<VitalWarning>();

        // Iterated in display order rather than dictionary order, so a confirm dialog lists
        // temperature before pulse every time instead of in whatever order the client happened to
        // build its payload.
        foreach (var metric in VitalMetrics.All)
        {
            if (!values.TryGetValue(metric, out var value) || value is null)
            {
                continue;
            }

            if (!byMetric.TryGetValue(metric, out var range))
            {
                // The physician has never configured this metric. Silence is the correct answer,
                // and it is the only answer this codebase is allowed to have on its own.
                continue;
            }

            var entered = value.Value;
            var unit = VitalMetrics.UnitOf(metric, clinicTemperatureUnit);
            var label = VitalMetrics.LabelOf(metric);

            // Strictly outside. A value exactly on the bound is inside the range the physician
            // described - "warn above 40" means 40 is acceptable and 40.1 is not.
            if (range.WarnLow is { } low && entered < low)
            {
                warnings.Add(new VitalWarning(
                    metric.ToString(),
                    label,
                    entered,
                    low,
                    null,
                    Message(label, entered, unit, "below", "lower", low)));
            }
            else if (range.WarnHigh is { } high && entered > high)
            {
                warnings.Add(new VitalWarning(
                    metric.ToString(),
                    label,
                    entered,
                    null,
                    high,
                    Message(label, entered, unit, "above", "upper", high)));
            }
        }

        return warnings;
    }

    /// <summary>
    /// Wording matters here. The sentence attributes the bound to the physician ("you set") and
    /// ends by offering to save the value as entered, because that is what confirming does. It is
    /// deliberately not phrased as a clinical opinion - this software has none (E-12, C-31).
    /// </summary>
    private static string Message(
        string label,
        decimal value,
        string? unit,
        string direction,
        string boundName,
        decimal bound)
    {
        var suffix = string.IsNullOrEmpty(unit) ? string.Empty : $" {unit}";
        return $"{label} {Format(value)}{suffix} is {direction} the {boundName} limit of "
            + $"{Format(bound)}{suffix} you set for this clinic. Check the reading, or confirm to "
            + "save it as entered.";
    }

    /// <summary>Trims trailing zeros so a threshold of 40.00 reads as 40.</summary>
    private static string Format(decimal value) =>
        value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
