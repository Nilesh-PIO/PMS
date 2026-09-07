using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Migrations
{
    /// <summary>
    /// F-6 (plan F-6 point 2). Adds the phone matching key and its index.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two notes for the plan owner, both flagged rather than done quietly.</b>
    /// </para>
    /// <para>
    /// <b>1. The migration's name says "Indexes" and it adds a column.</b> The name is the plan's
    /// and is kept verbatim so the migration history matches the document. Plan F-6 point 2 expected
    /// this migration to add indexes on <c>NormalizedName</c> and <c>NormalizedPhone</c>, but F-5
    /// already created both — its own tracker entry records the decision to build them where the
    /// columns were introduced. What F-6 actually needs, and what this adds, is a
    /// <em>third</em> column: the lossy matching key that answers Q-13. It has to be stored rather
    /// than computed, because a matching rule expressed as <c>RIGHT(NormalizedPhone, 10)</c> in a
    /// WHERE clause cannot be seeked on, and the duplicate check would scan every patient in the
    /// clinic while the physician waits.
    /// </para>
    /// <para>
    /// <b>2. The backfill below is load-bearing, not housekeeping.</b> Without it every patient
    /// registered under F-5 would carry a null key and be permanently invisible to the duplicate
    /// check — the feature would appear to work while silently protecting only patients registered
    /// after it shipped, which is the worst of the available outcomes because nothing would look
    /// broken.
    /// </para>
    /// <para>
    /// The T-SQL restates <c>PatientNormalizer.PhoneMatchKey</c>, and a restatement is a chance to
    /// drift. That risk is closed by a test rather than by care:
    /// <c>PhoneMatchKeyBackfillTests</c> runs this exact statement against seeded rows and asserts
    /// the result equals what the C# produces for the same inputs, so the two cannot disagree
    /// without a red test.
    /// </para>
    /// </remarks>
    public partial class AddPatientDuplicateIndexes : Migration
    {
        /// <summary>
        /// The backfill, exposed as a constant so the test that proves it agrees with the C# runs
        /// the statement that actually shipped rather than a copy of it.
        /// </summary>
        /// <remarks>
        /// Mirrors the C# step for step: drop one leading trunk zero, keep the last ten digits, and
        /// produce NULL rather than a short key below the minimum length — so numbers too short to
        /// identify anyone are absent from matching instead of all matching each other.
        /// </remarks>
        public const string BackfillPhoneMatchKeySql = """
            UPDATE p
            SET [PhoneMatchKey] =
                CASE
                    WHEN LEN(s.[Stripped]) < 6 THEN NULL
                    WHEN LEN(s.[Stripped]) > 10 THEN RIGHT(s.[Stripped], 10)
                    ELSE s.[Stripped]
                END
            FROM [Patients] AS p
            CROSS APPLY (
                SELECT CASE
                    WHEN LEFT(p.[NormalizedPhone], 1) = '0' AND LEN(p.[NormalizedPhone]) > 1
                        THEN SUBSTRING(p.[NormalizedPhone], 2, LEN(p.[NormalizedPhone]) - 1)
                    ELSE p.[NormalizedPhone]
                END AS [Stripped]
            ) AS s
            WHERE p.[NormalizedPhone] IS NOT NULL;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneMatchKey",
                table: "Patients",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Backfilled before the index is built, so the index is created over the finished data
            // rather than over a column of nulls that is then entirely rewritten under it.
            migrationBuilder.Sql(BackfillPhoneMatchKeySql);

            migrationBuilder.CreateIndex(
                name: "IX_Patient_PhoneMatchKey",
                table: "Patients",
                column: "PhoneMatchKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patient_PhoneMatchKey",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PhoneMatchKey",
                table: "Patients");
        }
    }
}
