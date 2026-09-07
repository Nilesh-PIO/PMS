using Microsoft.EntityFrameworkCore;
using PMS.Application.Abstractions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IClinicSettingsRepository"/>. The only type in the
/// solution that reads or writes the SettingOption and VitalRangeSetting tables.
/// </summary>
public sealed class ClinicSettingsRepository : IClinicSettingsRepository
{
    private readonly PmsDbContext _db;

    public ClinicSettingsRepository(PmsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<List<SettingOption>> GetOptionsAsync(
        SettingCategory category,
        CancellationToken cancellationToken) =>
        // Tracked, not AsNoTracking: the service reorders and retires the rows it reads and then
        // saves them. Active and inactive both come back - the settings screen cannot re-activate
        // an option it was never shown, and filtering is the service's decision, not this layer's.
        _db.SettingOptions
            .Where(o => o.Category == category)
            .OrderBy(o => o.DisplayOrder)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddOptionAsync(SettingOption option, CancellationToken cancellationToken) =>
        await _db.SettingOptions.AddAsync(option, cancellationToken);

    /// <inheritdoc />
    public Task<List<VitalRangeSetting>> GetVitalRangesAsync(CancellationToken cancellationToken) =>
        _db.VitalRangeSettings
            .OrderBy(r => r.Metric)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddVitalRangeAsync(
        VitalRangeSetting range,
        CancellationToken cancellationToken) =>
        await _db.VitalRangeSettings.AddAsync(range, cancellationToken);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        // One call for a whole list edit. EF wraps a multi-statement SaveChanges in a transaction,
        // which is what stops a reorder from half-applying and leaving two options in one slot
        // (plan section 2: writes that span more than one table run in one SaveChangesAsync).
        _db.SaveChangesAsync(cancellationToken);
}
