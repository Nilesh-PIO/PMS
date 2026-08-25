using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserName)
            .IsRequired()
            .HasMaxLength(100);

        // Exactly one physician uses this system, but the unique index is what stops a second
        // row with the same name from ever existing, rather than trusting the seeding code.
        builder.HasIndex(x => x.UserName).IsUnique();

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.SecurityStamp)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FailedAttempts)
            .HasDefaultValue(0);
    }
}
