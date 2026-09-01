using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the clinic database.
/// F-1 establishes it with no entity sets beyond <see cref="AppUser"/>
/// (planning-pms-verification.md, F-1 point 2); later features add their own sets and a
/// named migration each - schema never changes implicitly.
/// </summary>
public class PmsDbContext : DbContext
{
    public PmsDbContext(DbContextOptions<PmsDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    /// <summary>
    /// F-3. Singular by name because it is a singleton: exactly one row, always
    /// <see cref="ClinicProfile.SingletonId"/>. A plural name would invite a
    /// <c>FirstOrDefault()</c> over an unordered table somewhere down the line.
    /// </summary>
    public DbSet<ClinicProfile> ClinicProfile => Set<ClinicProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Every IEntityTypeConfiguration<T> in this assembly is picked up automatically, so a
        // new feature adds a configuration file and nothing else.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
