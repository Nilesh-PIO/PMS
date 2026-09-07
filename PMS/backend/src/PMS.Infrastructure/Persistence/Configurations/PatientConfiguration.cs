using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Patient"/> (planning-pms-verification.md, section 4 and F-5 point 2).
/// </summary>
public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    /// <summary>
    /// The check constraint enforcing the age rule. Named so a failure in SSMS or a log points
    /// straight at the rule rather than at a constraint number.
    /// </summary>
    public const string AgeCheckConstraintName = "CK_Patient_AgeShape";

    /// <summary>Filtered unique index behind the create-idempotency guarantee (E-43, E-46).</summary>
    public const string SubmissionIndexName = "UX_Patient_SubmissionId";

    /// <summary>
    /// F-6's phone-matching index. Named so the duplicate check's query plan is recognisable in a
    /// trace, and so a test can assert the index is genuinely in the shipped schema rather than
    /// merely in the model.
    /// </summary>
    public const string PhoneMatchKeyIndexName = "IX_Patient_PhoneMatchKey";

    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.NormalizedName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Gender).HasMaxLength(100);
        builder.Property(p => p.PrimaryPhone).HasMaxLength(40);
        builder.Property(p => p.NormalizedPhone).HasMaxLength(40);

        // F-6. At most PatientNormalizer.PhoneMatchDigits characters ever reach this column; 20 is
        // headroom so a change to that rule does not immediately need a migration to widen it.
        builder.Property(p => p.PhoneMatchKey).HasMaxLength(20);
        builder.Property(p => p.AltContact).HasMaxLength(200);
        builder.Property(p => p.InactiveReason).HasMaxLength(500);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.RegisteredUtc).IsRequired();

        // SQL Server rowversion. F-8's edit path reads it back and EF turns a stale value into a
        // DbUpdateConcurrencyException, which the existing middleware already maps to a 409 - two
        // tabs editing one patient must not silently last-write-wins.
        builder.Property(p => p.RowVersion).IsRowVersion();

        // Self-reference for F-6's non-destructive duplicate pointer (E-26). Restrict, not cascade:
        // deleting a survivor must never take the record that points at it along with it. In
        // practice nothing deletes a patient at all (E-33), and this makes the database agree.
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(p => p.MergedIntoPatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- indexes ---

        // F-6 and F-7 read these. Created here rather than in F-6 because they are what the
        // normalized columns are *for*, and an unindexed matching column invites a table scan on
        // every registration.
        builder.HasIndex(p => p.NormalizedName);
        builder.HasIndex(p => p.NormalizedPhone);

        // F-6's duplicate check seeks on this and nothing else. It is a separate index from
        // NormalizedPhone rather than a replacement for it: NormalizedPhone keeps every digit the
        // physician typed and is what F-7 will offer a last-four-digits search over, while this
        // column is the deliberately lossy matching key. Two questions, two indexes.
        builder.HasIndex(p => p.PhoneMatchKey)
            .HasDatabaseName(PhoneMatchKeyIndexName);

        // The real create-idempotency guarantee (E-43, E-46).
        //
        // Filtered on IS NOT NULL for a concrete SQL Server reason: a plain unique index treats
        // NULLs as equal to each other and would therefore permit exactly *one* patient without a
        // submission token in the whole table. The filter is load-bearing, not decorative.
        builder.HasIndex(p => p.SubmissionId)
            .IsUnique()
            .HasFilter("[SubmissionId] IS NOT NULL")
            .HasDatabaseName(SubmissionIndexName);

        // --- the age rule, at the database (E-9, C-19) ---
        //
        // The same rule PatientService enforces, restated where it cannot be bypassed. It is here
        // because this is the one F-5 rule that destroys information rather than inconveniencing
        // someone: an age stored without the date it was taken cannot be repaired by any later
        // migration, because the missing fact was never written down. A row inserted by a future
        // feature, a script, or someone in SSMS should fail rather than quietly corrupt the record.
        //
        // Two clauses:
        //   1. a date of birth and an approximate age are never both present - a record with two
        //      answers has no rule for which one wins;
        //   2. an approximate age and its recorded-on date are both present or both absent.
        //
        // Note this deliberately differs from the constraint sketched in plan F-5 point 2
        // ("DateOfBirth IS NOT NULL OR ApproxAgeYears IS NOT NULL OR both NULL"), which is a
        // tautology - it is true for every possible row and would enforce nothing. Implemented here
        // is the rule that section's own prose describes and that E-9 actually requires. Flagged
        // for the plan owner rather than silently substituted.
        builder.ToTable(t => t.HasCheckConstraint(
            AgeCheckConstraintName,
            """
            ([DateOfBirth] IS NULL OR [ApproxAgeYears] IS NULL)
            AND (
                ([ApproxAgeYears] IS NULL AND [AgeRecordedOn] IS NULL)
                OR ([ApproxAgeYears] IS NOT NULL AND [AgeRecordedOn] IS NOT NULL)
            )
            """));
    }
}
