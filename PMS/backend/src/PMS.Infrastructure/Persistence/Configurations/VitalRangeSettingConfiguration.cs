using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// F-4. Maps the doctor-defined plausibility thresholds
/// (planning-pms-verification.md, section 4 and F-4).
/// </summary>
/// <remarks>
/// <b>This table is deliberately seeded with nothing.</b> Plan F-4 point 1: "plausibility
/// thresholds are empty by default and entered by the doctor". An empty table is what makes
/// acceptance criterion 3 true by construction - with nothing configured, no vital can produce a
/// warning, because there is no row to compare against. Compare
/// <see cref="SettingOptionConfiguration"/>, which does seed: a dropdown with no options is
/// unusable, whereas a warning system with no thresholds is exactly right.
/// </remarks>
public sealed class VitalRangeSettingConfiguration : IEntityTypeConfiguration<VitalRangeSetting>
{
    /// <summary>One threshold row per metric, enforced by the database as well as the service.</summary>
    public const string UniqueMetricIndexName = "IX_VitalRangeSetting_Metric";

    /// <summary>
    /// A range whose floor sits above its ceiling would warn on every reading, including correct
    /// ones - which trains the physician to click through warnings. Enforced here as well as in
    /// the service because SSMS is a stated tool of this stack.
    /// </summary>
    public const string LowNotAboveHighConstraintName = "CK_VitalRangeSetting_LowNotAboveHigh";

    public void Configure(EntityTypeBuilder<VitalRangeSetting> builder)
    {
        builder.ToTable("VitalRangeSetting", table =>
            table.HasCheckConstraint(
                LowNotAboveHighConstraintName,
                "[WarnLow] IS NULL OR [WarnHigh] IS NULL OR [WarnLow] <= [WarnHigh]"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Metric)
            .HasConversion<int>()
            .IsRequired();

        // Nullable, and that is the load-bearing part: NULL means "never warn me about this"
        // (E-12). A NOT NULL column with a 0 default would arm a warning on every reading the
        // clinic ever enters, which is the opposite of the intended default.
        //
        // decimal(6,2) is a storage choice, not a clinical one: one decimal place is needed for a
        // temperature such as 37.5, and two leaves room without letting SQL Server round a
        // submitted value silently. The service rejects anything that would not survive the column.
        builder.Property(x => x.WarnLow)
            .HasColumnType("decimal(6,2)");

        builder.Property(x => x.WarnHigh)
            .HasColumnType("decimal(6,2)");

        builder.Property(x => x.UpdatedUtc)
            .IsRequired();

        builder.HasIndex(x => x.Metric)
            .IsUnique()
            .HasDatabaseName(UniqueMetricIndexName);
    }
}
