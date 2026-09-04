using PMS.Application.Dtos.Clinic;
using PMS.Domain.Enums;

namespace PMS.Application.Abstractions;

/// <summary>
/// The doctor-configured settings: the gender list, the vitals not-recorded reasons, and the
/// plausibility thresholds (planning-pms-verification.md, F-4).
/// </summary>
public interface IClinicSettingsService
{
    /// <summary>
    /// The options in a category, in <c>DisplayOrder</c>.
    /// </summary>
    /// <param name="category">
    /// Category name as it appears in the route or query string. Unknown or missing is a
    /// validation failure, not an empty list - "you asked for a list that does not exist" and
    /// "that list is empty" are different answers and the client renders them differently.
    /// </param>
    /// <param name="includeInactive">
    /// <c>false</c> (the default) returns only options offered for new entries - what a dropdown
    /// needs. <c>true</c> also returns retired options, which is what the settings editor needs to
    /// be able to bring one back. Defaulting to active-only is deliberate: a future feature that
    /// forgets this parameter gets the safe answer rather than a dropdown quietly offering a
    /// retired option.
    /// </param>
    Task<IReadOnlyList<SettingOptionResponse>> GetOptionsAsync(
        string? category,
        bool includeInactive,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a category's list with the submitted one: order becomes <c>DisplayOrder</c>, new
    /// values are inserted, and <b>omitted values are deactivated rather than deleted</b>
    /// (F-4 point 5).
    /// </summary>
    Task<IReadOnlyList<SettingOptionResponse>> SaveOptionsAsync(
        string? category,
        SettingOptionListRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every metric's threshold, configured or not. Never a short list - see
    /// <see cref="VitalRangeResponse"/>.
    /// </summary>
    Task<IReadOnlyList<VitalRangeResponse>> GetVitalRangesAsync(CancellationToken cancellationToken);

    /// <summary>Saves the submitted thresholds. Metrics not submitted are left alone.</summary>
    Task<IReadOnlyList<VitalRangeResponse>> SaveVitalRangesAsync(
        VitalRangeListRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// The soft warnings the physician's own thresholds produce for a set of entered values
    /// (E-12). Empty when nothing is configured, which is the default state of the system.
    /// </summary>
    /// <remarks>
    /// F-11 calls this from the consultation flow. It returns warnings; it never throws and never
    /// refuses, because a plausibility warning is advice and a refusal is how fabricated vitals
    /// get into permanent history.
    /// </remarks>
    Task<IReadOnlyList<VitalWarning>> EvaluateVitalsAsync(
        IReadOnlyDictionary<VitalMetric, decimal?> values,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="value"/> is an option currently offered in
    /// <paramref name="category"/>. The seam F-5 uses to keep free-text gender out of the patient
    /// table (C-20); <c>null</c> or blank is <c>true</c>, because "not answered" is a legitimate
    /// state that this list does not govern (E-23 is served by an explicit "Not stated" option,
    /// not by rejecting blanks).
    /// </summary>
    Task<bool> IsOfferedOptionAsync(
        SettingCategory category,
        string? value,
        CancellationToken cancellationToken);
}
