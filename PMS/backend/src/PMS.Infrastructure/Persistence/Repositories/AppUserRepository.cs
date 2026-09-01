using Microsoft.EntityFrameworkCore;
using PMS.Application.Abstractions;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAppUserRepository"/>. The only type in the solution
/// that reads or writes the AppUsers table.
/// </summary>
public sealed class AppUserRepository : IAppUserRepository
{
    private readonly PmsDbContext _db;

    public AppUserRepository(PmsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<AppUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken) =>
        // Tracked, not AsNoTracking: AuthService updates LastLoginUtc on the returned entity.
        _db.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyAsync(CancellationToken cancellationToken) =>
        _db.AppUsers.AnyAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(AppUser user, CancellationToken cancellationToken) =>
        await _db.AppUsers.AddAsync(user, cancellationToken);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
