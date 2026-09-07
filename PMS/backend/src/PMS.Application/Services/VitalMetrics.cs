using PMS.Domain.Enums;

namespace PMS.Application.Services;

/// <summary>
/// How each <see cref="VitalMetric"/> is named and unitted for a human
/// (planning-pms-verification.md, F-4; brainstorm E-24).
/// </summary>
/// <remarks>
/// <b>Labels and units only. Not one clinical number lives here</b> - see the
/// "Clinical-rule boundary" in plan section 7. Kept server-side and echoed in every response so
/// the settings screen and, from F-11, the consultation page cannot drift into calling the same
/// metric two different things.
/// </remarks>
public static class VitalMetrics
{
    /// <summary>
    /// The metrics a threshold may be set against, in the order they are displayed - the order a
    /// physician reads a vitals card, not the enum's numeric order by accident.
    /// </summary>
    public static readonly IReadOnlyList<VitalMetric> All =
    [
        VitalMetric.Temperature,
        VitalMetric.BloodPressureSystolic,
        VitalMetric.BloodPressureDiastolic,
        VitalMetric.PulseBpm,
    ];

    /// <summary>The metric as the physician reads it.</summary>
    public static string LabelOf(VitalMetric metric) => metric switch
    {
        VitalMetric.Temperature => "Temperature",
        VitalMetric.BloodPressureSystolic => "Blood pressure (systolic)",
        VitalMetric.BloodPressureDiastolic => "Blood pressure (diastolic)",
        VitalMetric.PulseBpm => "Pulse",
        _ => metric.ToString(),
    };

    /// <summary>
    /// The unit a threshold for this metric is expressed in. Blood pressure is always mmHg and
    /// pulse always bpm (plan F-4 point 1); temperature takes the clinic's configured unit, which
    /// is why it is a parameter rather than a constant (E-24).
    /// </summary>
    /// <param name="clinicTemperatureUnit">
    /// The clinic's unit from <c>ClinicProfile</c>. <see cref="TemperatureUnit.Unspecified"/>
    /// yields <c>null</c> for temperature rather than a guessed symbol - F-3 made an unanswered
    /// unit representable precisely so nothing downstream invents an answer.
    /// </param>
    public static string? UnitOf(VitalMetric metric, TemperatureUnit clinicTemperatureUnit) =>
        metric switch
        {
            VitalMetric.Temperature => clinicTemperatureUnit switch
            {
                TemperatureUnit.Celsius => "°C",
                TemperatureUnit.Fahrenheit => "°F",
                _ => null,
            },
            VitalMetric.BloodPressureSystolic or VitalMetric.BloodPressureDiastolic => "mmHg",
            VitalMetric.PulseBpm => "bpm",
            _ => null,
        };

    /// <summary>
    /// Parses a metric name from a request. Returns <c>false</c> for null, blank, an unknown name
    /// and for <see cref="VitalMetric.Unspecified"/> - which is a name the enum has but no caller
    /// may legitimately send.
    /// </summary>
    public static bool TryParse(string? name, out VitalMetric metric)
    {
        metric = VitalMetric.Unspecified;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return Enum.TryParse(name.Trim(), ignoreCase: true, out metric)
            && metric != VitalMetric.Unspecified
            && Enum.IsDefined(metric);
    }
}
