using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// F-3. Maps the singleton clinic profile (planning-pms-verification.md, section 4 and F-3).
/// </summary>
public sealed class ClinicProfileConfiguration : IEntityTypeConfiguration<ClinicProfile>
{
    /// <summary>Named so a failing INSERT in SSMS says what it violated, not just "CK_...".</summary>
    public const string SingletonConstraintName = "CK_ClinicProfile_SingletonRow";

    public void Configure(EntityTypeBuilder<ClinicProfile> builder)
    {
        builder.ToTable("ClinicProfile", table =>
            // The clinic has exactly one identity. Without this, a second row is a plain INSERT
            // away - and SSMS is a stated tool of this stack, so that is a realistic path, not a
            // theoretical one. "Which clinic name goes on the prescription?" must not have two
            // answers. Enforced by the database rather than only by the service, because the
            // service is not the only thing that can reach this table.
            table.HasCheckConstraint(SingletonConstraintName, "[Id] = 1"));

        builder.HasKey(x => x.Id);

        // No identity column: the key is a fixed constant, not a sequence. Letting SQL Server
        // generate it would hand out 2 on the next insert and defeat the constraint above.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ClinicName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AddressLines)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.DoctorName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DoctorRegistrationNo)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.PrescriptionFooter)
            .HasMaxLength(500);

        // varbinary(max): a PNG is opaque bytes. The 200 KB cap is a business rule enforced in
        // ClinicProfileService, where it can produce a 413 the physician can act on, rather than a
        // truncation the database would perform silently.
        builder.Property(x => x.SignatureImage)
            .HasColumnType("varbinary(max)");

        // Stored as int, not a string: TemperatureUnit is a closed set the print path branches on,
        // and a typo'd 'celcius' in the column would be a silently wrong prescription header.
        builder.Property(x => x.TemperatureUnit)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IsSetupComplete)
            .IsRequired();

        builder.Property(x => x.UpdatedUtc)
            .IsRequired();
    }
}
