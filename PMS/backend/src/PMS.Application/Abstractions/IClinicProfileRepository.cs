using PMS.Domain.Entities;

namespace PMS.Application.Abstractions;

/// <summary>
/// The only way the application layer reaches the ClinicProfile table
/// (planning-pms-verification.md, section 2 Data access).
/// </summary>
/// <remarks>
/// There is no <c>FindById</c> and no <c>Delete</c>. The row is a singleton pinned to
/// <see cref="ClinicProfile.SingletonId"/>, so an id parameter would only create the
/// possibility of asking for a row that must not exist; and deleting the clinic identity would
/// silently disarm the E-1 print gate for every future prescription.
/// </remarks>
public interface IClinicProfileRepository
{
    /// <summary>The clinic profile, or <c>null</c> when first-run setup has never been saved.</summary>
    Task<ClinicProfile?> GetAsync(CancellationToken cancellationToken);

    /// <summary>Adds the singleton row. Called once, on the first successful save.</summary>
    Task AddAsync(ClinicProfile profile, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
