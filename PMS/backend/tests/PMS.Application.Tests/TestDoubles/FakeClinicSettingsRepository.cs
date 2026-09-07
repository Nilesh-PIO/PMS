using PMS.Application.Abstractions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IClinicSettingsRepository"/> for the F-4 service tests.
/// </summary>
/// <remarks>
/// Models the two things the real repository does that the tests depend on: rows added before a
/// save are visible to the next read (so a save-then-read test exercises the update path rather
/// than accumulating duplicates), and ids are assigned on insert, because
/// <c>ClinicSettingsService</c> uses <c>Id == 0</c> to tell a brand-new row from a stored one.
/// </remarks>
public sealed class FakeClinicSettingsRepository : IClinicSettingsRepository
{
    private readonly List<SettingOption> _options = [];
    private readonly List<VitalRangeSetting> _ranges = [];
    private int _nextOptionId = 1;
    private int _nextRangeId = 1;

    /// <summary>How many times <see cref="SaveChangesAsync"/> was called.</summary>
    public int SaveCount { get; private set; }

    /// <summary>Everything stored, for assertions about what was actually persisted.</summary>
    public IReadOnlyList<SettingOption> StoredOptions => _options;

    /// <summary>Every stored threshold.</summary>
    public IReadOnlyList<VitalRangeSetting> StoredRanges => _ranges;

    /// <summary>Seeds a category exactly as the migration's <c>HasData</c> would.</summary>
    public FakeClinicSettingsRepository SeedOptions(
        SettingCategory category,
        params string[] values)
    {
        var order = 1;
        foreach (var value in values)
        {
            _options.Add(new SettingOption
            {
                Id = _nextOptionId++,
                Category = category,
                Value = value,
                DisplayOrder = order++,
                IsActive = true,
            });
        }

        return this;
    }

    /// <summary>The seeded lists as the real migration creates them (plan F-4 point 1).</summary>
    public static FakeClinicSettingsRepository Seeded() =>
        new FakeClinicSettingsRepository()
            .SeedOptions(SettingCategory.Gender, "Female", "Male", "Other", "Not stated")
            .SeedOptions(
                SettingCategory.VitalsNotRecordedReason,
                "Equipment unavailable",
                "Patient declined",
                "Not clinically indicated",
                "Other");

    /// <summary>Seeds a threshold, as saving one on the settings screen would.</summary>
    public FakeClinicSettingsRepository SeedRange(
        VitalMetric metric,
        decimal? warnLow,
        decimal? warnHigh)
    {
        _ranges.Add(new VitalRangeSetting
        {
            Id = _nextRangeId++,
            Metric = metric,
            WarnLow = warnLow,
            WarnHigh = warnHigh,
            UpdatedUtc = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero),
        });

        return this;
    }

    public Task<List<SettingOption>> GetOptionsAsync(
        SettingCategory category,
        CancellationToken cancellationToken) =>
        Task.FromResult(_options
            .Where(o => o.Category == category)
            .OrderBy(o => o.DisplayOrder)
            .ToList());

    public Task AddOptionAsync(SettingOption option, CancellationToken cancellationToken)
    {
        _options.Add(option);
        return Task.CompletedTask;
    }

    public Task<List<VitalRangeSetting>> GetVitalRangesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_ranges.OrderBy(r => r.Metric).ToList());

    public Task AddVitalRangeAsync(VitalRangeSetting range, CancellationToken cancellationToken)
    {
        _ranges.Add(range);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;

        // Ids are handed out at save time, matching SQL Server identity: before this point a new
        // row has Id 0, which is exactly what the service relies on to avoid retiring the option it
        // has just inserted.
        foreach (var option in _options.Where(o => o.Id == 0))
        {
            option.Id = _nextOptionId++;
        }

        foreach (var range in _ranges.Where(r => r.Id == 0))
        {
            range.Id = _nextRangeId++;
        }

        return Task.CompletedTask;
    }
}
