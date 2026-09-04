using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// F-4. Maps the doctor-configured lookup lists and seeds them
/// (planning-pms-verification.md, section 4 and F-4 point 2: "including a seed of the option lists
/// above via <c>HasData</c>").
/// </summary>
public sealed class SettingOptionConfiguration : IEntityTypeConfiguration<SettingOption>
{
    /// <summary>
    /// Named so a failing INSERT in SSMS says what it violated. The unique index is the
    /// database-level half of C-20: one spelling per meaning, enforced where the service is not
    /// the only writer.
    /// </summary>
    public const string UniqueValueIndexName = "IX_SettingOption_Category_Value";

    /// <summary>Blank options cannot reach a dropdown, whatever writes the row.</summary>
    public const string ValueNotBlankConstraintName = "CK_SettingOption_ValueNotBlank";

    public void Configure(EntityTypeBuilder<SettingOption> builder)
    {
        builder.ToTable("SettingOption", table =>
            table.HasCheckConstraint(ValueNotBlankConstraintName, "LEN(LTRIM(RTRIM([Value]))) > 0"));

        builder.HasKey(x => x.Id);

        // Stored as int, not a string, for the reason F-3 gave for TemperatureUnit: the category is
        // a closed set the code branches on, and a typo'd 'gendre' in the column would silently
        // hide an entire list from the screen that reads it.
        builder.Property(x => x.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Value)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayOrder)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Case-insensitivity comes from the database's default collation, which is what makes this
        // index reject "male" alongside "Male" rather than merely reject an exact repeat. The
        // service checks the same rule first so the physician gets a field-level 400 instead of a
        // 500 from a constraint violation.
        builder.HasIndex(x => new { x.Category, x.Value })
            .IsUnique()
            .HasDatabaseName(UniqueValueIndexName);

        // The list a dropdown reads, in the order it reads it.
        builder.HasIndex(x => new { x.Category, x.IsActive, x.DisplayOrder });

        SeedLists(builder);
    }

    /// <summary>
    /// The seeded lists (plan F-4 point 1: Q-9 and Q-2 assumptions).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>These are seeds, not code.</b> Every one of them is editable on the settings screen, and
    /// nothing in the application branches on a particular value - the one exception is
    /// <c>Gender / Not stated</c>, which <c>ClinicSettingsService</c> refuses to let the physician
    /// remove because the alternative to "not stated" is a guess in a permanent record (E-23).
    /// </para>
    /// <para>
    /// <b>Both lists rest on stated assumptions, not on answered questions.</b> Q-9 (which gender
    /// values) and Q-10/Q-2 (vitals reasons) are open policy calls; the plan's F-4 point 1 states
    /// the default and this is it, in one place, so a different answer from the physician is one
    /// migration plus a settings edit rather than a hunt.
    /// </para>
    /// <para>
    /// Ids are explicit and permanent: 1-4 gender, 5-8 reasons. HasData needs fixed keys, and a
    /// later feature that ever references one by id must not find it renumbered.
    /// </para>
    /// </remarks>
    private static void SeedLists(EntityTypeBuilder<SettingOption> builder) =>
        builder.HasData(
            // ASSUMPTION (Q-9, plan F-4 point 1): gender options seeded Female / Male / Other /
            // Not stated, doctor-editable, "Not stated" never removable (E-23).
            new SettingOption
            {
                Id = 1,
                Category = SettingCategory.Gender,
                Value = "Female",
                DisplayOrder = 1,
                IsActive = true,
            },
            new SettingOption
            {
                Id = 2,
                Category = SettingCategory.Gender,
                Value = "Male",
                DisplayOrder = 2,
                IsActive = true,
            },
            new SettingOption
            {
                Id = 3,
                Category = SettingCategory.Gender,
                Value = "Other",
                DisplayOrder = 3,
                IsActive = true,
            },
            new SettingOption
            {
                Id = 4,
                Category = SettingCategory.Gender,
                Value = "Not stated",
                DisplayOrder = 4,
                IsActive = true,
            },

            // ASSUMPTION (Q-2, plan F-4 point 1, shared with F-11): the reasons a vital could not
            // be recorded (§5.2, REC-3, E-18). F-11's escape hatch reads this list.
            new SettingOption
            {
                Id = 5,
                Category = SettingCategory.VitalsNotRecordedReason,
                Value = "Equipment unavailable",
                DisplayOrder = 1,
                IsActive = true,
            },
            new SettingOption
            {
                Id = 6,
                Category = SettingCategory.VitalsNotRecordedReason,
                Value = "Patient declined",
                DisplayOrder = 2,
                IsActive = true,
            },
            new SettingOption
            {
                Id = 7,
                Category = SettingCategory.VitalsNotRecordedReason,
                Value = "Not clinically indicated",
                DisplayOrder = 3,
                IsActive = true,
            },
            new SettingOption
            {
                Id = 8,
                Category = SettingCategory.VitalsNotRecordedReason,
                Value = "Other",
                DisplayOrder = 4,
                IsActive = true,
            });
}
