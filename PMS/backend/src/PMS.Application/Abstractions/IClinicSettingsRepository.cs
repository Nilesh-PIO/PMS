using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Abstractions;

/// <summary>
/// Persistence for the two doctor-configured settings tables, <c>SettingOption</c> and
/// <c>VitalRangeSetting</c> (planning-pms-verification.md, F-4 point 2).
/// </summary>
/// <remarks>
/// <para>
/// One repository for two tables, unlike F-2's and F-3's one-per-aggregate split. Reason: both
/// tables are the same aggregate from the physician's point of view - "the things I configured" -
/// and, more concretely, a list edit is a mixed batch of inserts and updates that must land
/// together or not at all. Reordering four options and deactivating a fifth is one intent; half of
/// it applying would leave two options claiming the same slot. A single
/// <see cref="SaveChangesAsync"/> over one context is the transaction boundary the plan's
/// section 2 asks for, and splitting the repository would put that boundary in the caller's hands.
/// </para>
/// </remarks>
public interface IClinicSettingsRepository
{
    /// <summary>
    /// Every option in a category, active and inactive, ordered by <c>DisplayOrder</c>. Tracked,
    /// because the service mutates what it reads.
    /// </summary>
    Task<List<SettingOption>> GetOptionsAsync(
        SettingCategory category,
        CancellationToken cancellationToken);

    /// <summary>Adds a new option row.</summary>
    Task AddOptionAsync(SettingOption option, CancellationToken cancellationToken);

    /// <summary>Every configured threshold. Metrics never configured simply have no row.</summary>
    Task<List<VitalRangeSetting>> GetVitalRangesAsync(CancellationToken cancellationToken);

    /// <summary>Adds a threshold row for a metric that has never been configured.</summary>
    Task AddVitalRangeAsync(VitalRangeSetting range, CancellationToken cancellationToken);

    /// <summary>
    /// Commits everything staged above in one transaction. There is deliberately no per-row save.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
