using Microsoft.EntityFrameworkCore;
using PMS.Application.Abstractions;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IClinicProfileRepository"/>. The only type in the
/// solution that reads or writes the ClinicProfile table.
/// </summary>
public sealed class ClinicProfileRepository : IClinicProfileRepository
{
    private readonly PmsDbContext _db;

    public ClinicProfileRepository(PmsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<ClinicProfile?> GetAsync(CancellationToken cancellationToken) =>
        // Tracked, not AsNoTracking: ClinicProfileService mutates the returned entity and saves.
        // Queried by the singleton id rather than FirstOrDefault over the table, so that if a
        // second row ever did appear the read would still be deterministic rather than
        // order-dependent.
        _db.ClinicProfile
            .FirstOrDefaultAsync(p => p.Id == ClinicProfile.SingletonId, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(ClinicProfile profile, CancellationToken cancellationToken) =>
        await _db.ClinicProfile.AddAsync(profile, cancellationToken);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
