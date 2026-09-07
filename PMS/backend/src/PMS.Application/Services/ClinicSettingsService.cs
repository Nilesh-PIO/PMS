using PMS.Application.Abstractions;
using PMS.Application.Dtos.Clinic;
using PMS.Application.Exceptions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Services;

/// <summary>
/// F-4. Owns the doctor-configured lists and plausibility thresholds
/// (planning-pms-verification.md, F-4; brainstorm C-20, E-12, E-23, E-24).
/// </summary>
/// <remarks>
/// <para>
/// <b>Integrity rule 1 - one spelling per meaning (C-20).</b> Gender is the field the brainstorm
/// singles out: free text guarantees <c>M</c>, <c>Male</c> and <c>male</c> in one column, after
/// which every filter, count and export is quietly wrong. This service is the only writer of that
/// list and it matches submitted values case-insensitively, so re-sending <c>"male"</c> renames
/// nothing and creates nothing - it updates the existing <c>Male</c> row.
/// </para>
/// <para>
/// <b>Integrity rule 2 - an option is retired, never deleted (F-4 point 5).</b> Patient rows store
/// the gender <em>string</em>, not a foreign key, so a delete would leave 2026's records showing a
/// value that appears nowhere in settings. Every removal is <c>IsActive = false</c>. There is no
/// delete path on this class at all, which is a stronger guarantee than a delete path that is
/// careful.
/// </para>
/// <para>
/// <b>Integrity rule 3 - a list cannot be emptied of active options.</b> <c>Gender / Not stated</c>
/// is additionally protected outright (E-23): an unknown gender must always have somewhere to go
/// other than a guess. For the vitals reasons the rule is subtler but the same shape - F-11's
/// mandatory-or-reason escape hatch (REC-3, E-18) reads that list, so an empty list converts the
/// escape hatch back into the dead end it was added to remove, and a dead end is what makes
/// someone type a BP from memory.
/// </para>
/// <para>
/// <b>Integrity rule 4 - blank is not zero.</b> A threshold of <c>null</c> means "never warn me";
/// a threshold of <c>0</c> would warn on every reading. The distinction survives the whole path -
/// nullable columns, nullable DTO fields, and a frontend that converts an empty input to
/// <c>null</c> with a test pinning it.
/// </para>
/// <para>
/// <b>What this class deliberately does not contain: a single clinical number.</b> No default
/// range, no seeded threshold, no sanity bound on a temperature or a pulse. Plan section 7 makes
/// that a standing review item; the only numeric limits below are storage limits
/// (<see cref="MaxThresholdMagnitude"/>, <see cref="MaxThresholdDecimals"/>) and they are named as
/// such so a reviewer does not mistake them for medicine.
/// </para>
/// </remarks>
public sealed class ClinicSettingsService : IClinicSettingsService
{
    /// <summary>Longest accepted option value. Long enough for "Not clinically indicated".</summary>
    public const int MaxOptionValueLength = 100;

    /// <summary>Most options one list may hold, active and inactive together.</summary>
    public const int MaxOptionsPerCategory = 50;

    /// <summary>
    /// Storage limit, <b>not</b> a clinical one: the column is <c>decimal(6,2)</c>, so a threshold
    /// must be under 10000. Named this way because a reviewer scanning for authored clinical
    /// ranges should be able to see immediately that this is not one.
    /// </summary>
    public const decimal MaxThresholdMagnitude = 10000m;

    /// <summary>Storage limit again: <c>decimal(6,2)</c> holds two decimal places.</summary>
    public const int MaxThresholdDecimals = 2;

    /// <summary>
    /// Values that may never be deactivated or dropped from their list.
    /// <c>Gender / Not stated</c> is E-23: the alternative to "not stated" is a guess written into
    /// a permanent record.
    /// </summary>
    private static readonly IReadOnlyDictionary<SettingCategory, string[]> ProtectedValues =
        new Dictionary<SettingCategory, string[]>
        {
            [SettingCategory.Gender] = ["Not stated"],
            [SettingCategory.VitalsNotRecordedReason] = [],
        };

    private readonly IClinicSettingsRepository _settings;
    private readonly IClinicProfileService _clinicProfile;
    private readonly IClock _clock;

    public ClinicSettingsService(
        IClinicSettingsRepository settings,
        IClinicProfileService clinicProfile,
        IClock clock)
    {
        _settings = settings;
        _clinicProfile = clinicProfile;
        _clock = clock;
    }

    // --- options ------------------------------------------------------------

    /// <inheritdoc />
    public async Task<IReadOnlyList<SettingOptionResponse>> GetOptionsAsync(
        string? category,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var parsed = ParseCategory(category);
        var options = await _settings.GetOptionsAsync(parsed, cancellationToken);

        return options
            .Where(o => includeInactive || o.IsActive)
            .OrderBy(o => o.DisplayOrder)
            .Select(o => ToResponse(parsed, o))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SettingOptionResponse>> SaveOptionsAsync(
        string? category,
        SettingOptionListRequest request,
        CancellationToken cancellationToken)
    {
        var parsed = ParseCategory(category);
        var submitted = ValidateOptions(parsed, request);

        var existing = await _settings.GetOptionsAsync(parsed, cancellationToken);

        // TryAdd rather than ToDictionary: the unique index on (Category, Value) makes a duplicate
        // unreachable through this application, but SSMS can reach the table too, and a 500 from a
        // dictionary collision would be a uselessly obscure way to report that.
        var byValue = new Dictionary<string, SettingOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in existing.OrderBy(o => o.DisplayOrder))
        {
            byValue.TryAdd(row.Value, row);
        }

        var touched = new HashSet<int>();

        var order = 0;
        foreach (var item in submitted)
        {
            order++;

            if (byValue.TryGetValue(item.Value, out var row))
            {
                // Matched case-insensitively but the stored spelling is left alone: the value is
                // already sitting on historical patient rows, and rewriting it here would change
                // what those rows appear to say without touching them (C-20).
                row.DisplayOrder = order;
                row.IsActive = item.IsActive;
                touched.Add(row.Id);
                continue;
            }

            var added = new SettingOption
            {
                Category = parsed,
                Value = item.Value,
                DisplayOrder = order,
                IsActive = item.IsActive,
            };

            await _settings.AddOptionAsync(added, cancellationToken);
            existing.Add(added);
        }

        // Anything the physician left out is retired, not removed - integrity rule 2. Retired rows
        // keep their relative order and sit after the live ones, so the settings screen shows a
        // stable "no longer offered" tail rather than reshuffling on every save.
        foreach (var row in existing.Where(o => o.Id != 0 && !touched.Contains(o.Id))
                     .OrderBy(o => o.DisplayOrder)
                     .ToList())
        {
            row.IsActive = false;
            row.DisplayOrder = ++order;
        }

        // One SaveChanges for the whole edit: inserts, reorders and retirements land together or
        // not at all. A half-applied reorder would leave two options claiming one slot.
        await _settings.SaveChangesAsync(cancellationToken);

        return existing
            .OrderBy(o => o.DisplayOrder)
            .Select(o => ToResponse(parsed, o))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> IsOfferedOptionAsync(
        SettingCategory category,
        string? value,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            // Not answered. This list governs which answers are spellable, not whether the
            // question is compulsory - that is the consuming feature's rule (F-5 for gender).
            return true;
        }

        var options = await _settings.GetOptionsAsync(category, cancellationToken);
        return options.Any(o =>
            o.IsActive && string.Equals(o.Value, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    // --- vital ranges -------------------------------------------------------

    /// <inheritdoc />
    public async Task<IReadOnlyList<VitalRangeResponse>> GetVitalRangesAsync(
        CancellationToken cancellationToken)
    {
        var rows = await _settings.GetVitalRangesAsync(cancellationToken);
        var unit = await ClinicTemperatureUnitAsync(cancellationToken);

        return Project(rows, unit);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VitalRangeResponse>> SaveVitalRangesAsync(
        VitalRangeListRequest request,
        CancellationToken cancellationToken)
    {
        var submitted = ValidateVitalRanges(request);

        var rows = await _settings.GetVitalRangesAsync(cancellationToken);
        var byMetric = rows.ToDictionary(r => r.Metric);
        var now = _clock.UtcNow;

        foreach (var (metric, low, high) in submitted)
        {
            if (byMetric.TryGetValue(metric, out var row))
            {
                row.WarnLow = low;
                row.WarnHigh = high;
                row.UpdatedUtc = now;
                continue;
            }

            var added = new VitalRangeSetting
            {
                Metric = metric,
                WarnLow = low,
                WarnHigh = high,
                UpdatedUtc = now,
            };

            await _settings.AddVitalRangeAsync(added, cancellationToken);
            rows.Add(added);
            byMetric[metric] = added;
        }

        await _settings.SaveChangesAsync(cancellationToken);

        var unit = await ClinicTemperatureUnitAsync(cancellationToken);
        return Project(rows, unit);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VitalWarning>> EvaluateVitalsAsync(
        IReadOnlyDictionary<VitalMetric, decimal?> values,
        CancellationToken cancellationToken)
    {
        var rows = await _settings.GetVitalRangesAsync(cancellationToken);
        var unit = await ClinicTemperatureUnitAsync(cancellationToken);

        return VitalRangeEvaluator.Evaluate(values, rows, unit);
    }

    // --- validation ---------------------------------------------------------

    private static SettingCategory ParseCategory(string? category)
    {
        if (!string.IsNullOrWhiteSpace(category)
            && Enum.TryParse<SettingCategory>(category.Trim(), ignoreCase: true, out var parsed)
            && parsed != SettingCategory.Unspecified
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        // A 400 naming the categories that do exist, rather than an empty 200. An empty list would
        // render as "you have configured nothing", and the physician would start typing.
        var known = string.Join(", ", Enum.GetValues<SettingCategory>()
            .Where(c => c != SettingCategory.Unspecified));

        throw new ValidationFailedException(
            "category",
            $"Unknown settings category. Expected one of: {known}.");
    }

    private sealed record ValidatedOption(string Value, bool IsActive);

    private static List<ValidatedOption> ValidateOptions(
        SettingCategory category,
        SettingOptionListRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        var items = request?.Items ?? [];
        var validated = new List<ValidatedOption>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (items.Count > MaxOptionsPerCategory)
        {
            errors[nameof(SettingOptionListRequest.Items)] =
                [$"A list may hold at most {MaxOptionsPerCategory} options."];
        }

        for (var i = 0; i < items.Count; i++)
        {
            var field = $"Items[{i}].Value";
            var value = items[i].Value?.Trim() ?? string.Empty;

            if (value.Length == 0)
            {
                errors[field] = ["Enter a value, or remove the row."];
                continue;
            }

            if (value.Length > MaxOptionValueLength)
            {
                errors[field] = [$"A value must be {MaxOptionValueLength} characters or fewer."];
                continue;
            }

            // The duplicate this table exists to prevent, caught before it can be stored (C-20).
            // Case-insensitive, so "Male" and "male" collide rather than coexist.
            if (seen.TryGetValue(value, out var firstIndex))
            {
                errors[field] =
                    [$"\"{value}\" is already in the list at position {firstIndex + 1}."];
                continue;
            }

            seen[value] = i;
            validated.Add(new ValidatedOption(value, items[i].IsActive));
        }

        if (errors.Count == 0)
        {
            // Integrity rule 3, checked only once the individual rows are known good so the
            // physician is not told about an empty list while a typo is the real problem.
            if (!validated.Any(v => v.IsActive))
            {
                errors[nameof(SettingOptionListRequest.Items)] =
                [
                    "Keep at least one option active. A list with nothing in it leaves the person "
                    + "filling in the form with no valid answer.",
                ];
            }

            foreach (var required in ProtectedValues[category])
            {
                var match = validated.FirstOrDefault(v =>
                    string.Equals(v.Value, required, StringComparison.OrdinalIgnoreCase));

                if (match is null || !match.IsActive)
                {
                    errors[nameof(SettingOptionListRequest.Items)] =
                    [
                        $"\"{required}\" must stay in the list and stay active. Without it, an "
                        + "unknown value has to be guessed, and the guess becomes permanent.",
                    ];
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        return validated;
    }

    private static List<(VitalMetric Metric, decimal? Low, decimal? High)> ValidateVitalRanges(
        VitalRangeListRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        var items = request?.Items ?? [];
        var validated = new List<(VitalMetric, decimal?, decimal?)>();
        var seen = new HashSet<VitalMetric>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (!VitalMetrics.TryParse(item.Metric, out var metric))
            {
                // Not ignored. A threshold the physician believes they saved and which was dropped
                // on the floor is the E-47 failure mode wearing a different hat.
                errors[$"Items[{i}].Metric"] =
                [
                    "Unknown vital. Expected one of: "
                    + string.Join(", ", VitalMetrics.All) + ".",
                ];
                continue;
            }

            if (!seen.Add(metric))
            {
                errors[$"Items[{i}].Metric"] =
                    [$"{VitalMetrics.LabelOf(metric)} appears twice in this submission."];
                continue;
            }

            var lowOk = ValidateThreshold(errors, $"Items[{i}].WarnLow", item.WarnLow);
            var highOk = ValidateThreshold(errors, $"Items[{i}].WarnHigh", item.WarnHigh);

            if (lowOk && highOk && item.WarnLow is { } low && item.WarnHigh is { } high
                && low > high)
            {
                // The one relational rule, and it is arithmetic rather than clinical: a range whose
                // floor is above its ceiling would warn on every value, including correct ones.
                errors[$"Items[{i}].WarnLow"] =
                    ["The lower limit must not be above the upper limit."];
                continue;
            }

            if (lowOk && highOk)
            {
                validated.Add((metric, item.WarnLow, item.WarnHigh));
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        return validated;
    }

    /// <summary>
    /// Storage-shape check only - magnitude and decimal places, so the value survives a
    /// <c>decimal(6,2)</c> column without SQL Server rounding it silently. <b>No clinical bound is
    /// applied and none may be added here</b> (plan section 7).
    /// </summary>
    private static bool ValidateThreshold(
        IDictionary<string, string[]> errors,
        string field,
        decimal? value)
    {
        if (value is null)
        {
            // Blank. The point of the feature - integrity rule 4.
            return true;
        }

        var entered = value.Value;

        if (Math.Abs(entered) >= MaxThresholdMagnitude)
        {
            errors[field] = [$"Enter a number below {MaxThresholdMagnitude:0}."];
            return false;
        }

        if (decimal.Round(entered, MaxThresholdDecimals) != entered)
        {
            errors[field] = [$"Use at most {MaxThresholdDecimals} decimal places."];
            return false;
        }

        return true;
    }

    // --- projection ---------------------------------------------------------

    private async Task<TemperatureUnit> ClinicTemperatureUnitAsync(CancellationToken cancellationToken)
    {
        // Read through F-3's service rather than the profile table directly, so there is exactly
        // one place that knows how the clinic profile is stored. No profile yet (first run) means
        // Unspecified, and Unspecified means the temperature unit renders as "not set" rather than
        // as a guessed symbol (E-24).
        var profile = await _clinicProfile.GetAsync(cancellationToken);
        return profile?.TemperatureUnit ?? TemperatureUnit.Unspecified;
    }

    private static List<VitalRangeResponse> Project(
        IEnumerable<VitalRangeSetting> rows,
        TemperatureUnit clinicTemperatureUnit)
    {
        var byMetric = rows
            .GroupBy(r => r.Metric)
            .ToDictionary(g => g.Key, g => g.Last());

        return VitalMetrics.All
            .Select(metric =>
            {
                byMetric.TryGetValue(metric, out var row);
                return new VitalRangeResponse(
                    metric.ToString(),
                    VitalMetrics.LabelOf(metric),
                    VitalMetrics.UnitOf(metric, clinicTemperatureUnit),
                    row?.WarnLow,
                    row?.WarnHigh,
                    row?.UpdatedUtc);
            })
            .ToList();
    }

    private static SettingOptionResponse ToResponse(SettingCategory category, SettingOption option) =>
        new(
            option.Id,
            category.ToString(),
            option.Value,
            option.DisplayOrder,
            option.IsActive,
            ProtectedValues[category]
                .Any(p => string.Equals(p, option.Value, StringComparison.OrdinalIgnoreCase)));
}
