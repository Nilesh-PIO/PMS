using PMS.Application.Abstractions;
using PMS.Domain.Entities;

namespace PMS.Application.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IAppUserRepository"/>. Keeps AuthService and InitialUserSeeder tests
/// free of a database while still exercising the exact calls they make.
/// </summary>
public sealed class FakeAppUserRepository : IAppUserRepository
{
    private readonly List<AppUser> _users = [];

    /// <summary>How many times <see cref="SaveChangesAsync"/> was called - a test asserts a write happened.</summary>
    public int SaveCount { get; private set; }

    public IReadOnlyList<AppUser> Users => _users;

    public FakeAppUserRepository(params AppUser[] seed) => _users.AddRange(seed);

    public Task<AppUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(u =>
            string.Equals(u.UserName, userName, StringComparison.Ordinal)));

    public Task<bool> AnyAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_users.Count > 0);

    public Task AddAsync(AppUser user, CancellationToken cancellationToken)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
