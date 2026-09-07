namespace PMS.Application.Dtos.Clinic;

/// <summary>
/// One vital's plausibility threshold, as the API returns it
/// (planning-pms-verification.md, F-4 point 3).
/// </summary>
/// <remarks>
/// <b>Every metric is always returned, configured or not.</b> A metric the physician has never
/// touched comes back with both bounds <c>null</c> and <c>UpdatedUtc = null</c>, so the settings
/// screen renders the complete list without having to know the metric vocabulary itself, and
/// "never configured" is visibly different from "configured and then cleared" only in
/// <c>UpdatedUtc</c> - both mean the same thing to the warning logic, which is silence.
/// </remarks>
/// <param name="Metric">
/// The metric <b>name</b> (<c>"Temperature"</c>, <c>"BloodPressureSystolic"</c>,
/// <c>"BloodPressureDiastolic"</c>, <c>"PulseBpm"</c>) - see the note on
/// <see cref="SettingOptionResponse.Category"/> for why names rather than numbers.
/// </param>
/// <param name="Label">
/// The metric as the physician reads it, e.g. <c>"Blood pressure (systolic)"</c>. Server-supplied
/// so the label and the metric can never drift apart across the two clients that render them
/// (the settings screen and, from F-11, the consultation page).
/// </param>
/// <param name="Unit">
/// The unit this threshold is expressed in - <c>"mmHg"</c>, <c>"bpm"</c>, or the clinic's
/// configured temperature unit symbol (E-24). <c>null</c> only if the clinic has not chosen a
/// temperature unit yet, which F-3's setup gate makes unreachable in practice.
/// </param>
/// <param name="WarnLow">Warn below this. <c>null</c> means no lower warning (E-12).</param>
/// <param name="WarnHigh">Warn above this. <c>null</c> means no upper warning.</param>
/// <param name="UpdatedUtc">
/// When this threshold was last saved, or <c>null</c> if it has never been configured.
/// </param>
public sealed record VitalRangeResponse(
    string Metric,
    string Label,
    string? Unit,
    decimal? WarnLow,
    decimal? WarnHigh,
    DateTimeOffset? UpdatedUtc);
