using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PMS.Infrastructure.Migrations
{
    /// <summary>
    /// F-4. Creates the two doctor-configured settings tables and seeds the option lists
    /// (planning-pms-verification.md, F-4 point 2: migration "AddClinicSettings", "including a
    /// seed of the option lists above via HasData").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SettingOption is seeded; VitalRangeSetting deliberately is not.</b> A dropdown with no
    /// options is unusable, so the gender list and the vitals not-recorded reasons arrive with the
    /// plan's stated Q-9 / Q-2 defaults - all editable afterwards on the settings screen. A warning
    /// system with no thresholds, by contrast, is exactly right: plan F-4 point 1 says thresholds
    /// are "empty by default and entered by the doctor", and an empty table is what makes F-4
    /// acceptance criterion 3 (nothing configured means no warning) true by construction rather
    /// than by a code path someone could later "fix".
    /// </para>
    /// <para>
    /// <b>The two load-bearing constraints.</b> <c>IX_SettingOption_Category_Value</c> is the
    /// database half of C-20 - one spelling per meaning, so "Male" and "male" cannot both exist
    /// (the default collation makes the unique index case-insensitive).
    /// <c>CK_VitalRangeSetting_LowNotAboveHigh</c> refuses an inverted range, which would warn on
    /// every reading including correct ones. Both are enforced here and not only in the service,
    /// because SQL Server via SSMS is a stated tool of this stack and the service is not the only
    /// thing that can reach these tables.
    /// </para>
    /// <para>
    /// <b>No clinical value appears in this migration</b>, and none may be added: the seeded rows
    /// are vocabulary (list entries), not medicine. Plan section 7 makes that a standing review
    /// item for anything touching F-4, F-11 or F-13.
    /// </para>
    /// </remarks>
    public partial class AddClinicSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettingOption",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingOption", x => x.Id);
                    table.CheckConstraint("CK_SettingOption_ValueNotBlank", "LEN(LTRIM(RTRIM([Value]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "VitalRangeSetting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Metric = table.Column<int>(type: "int", nullable: false),
                    WarnLow = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    WarnHigh = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalRangeSetting", x => x.Id);
                    table.CheckConstraint("CK_VitalRangeSetting_LowNotAboveHigh", "[WarnLow] IS NULL OR [WarnHigh] IS NULL OR [WarnLow] <= [WarnHigh]");
                });

            migrationBuilder.InsertData(
                table: "SettingOption",
                columns: new[] { "Id", "Category", "DisplayOrder", "IsActive", "Value" },
                values: new object[,]
                {
                    { 1, 1, 1, true, "Female" },
                    { 2, 1, 2, true, "Male" },
                    { 3, 1, 3, true, "Other" },
                    { 4, 1, 4, true, "Not stated" },
                    { 5, 2, 1, true, "Equipment unavailable" },
                    { 6, 2, 2, true, "Patient declined" },
                    { 7, 2, 3, true, "Not clinically indicated" },
                    { 8, 2, 4, true, "Other" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettingOption_Category_IsActive_DisplayOrder",
                table: "SettingOption",
                columns: new[] { "Category", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SettingOption_Category_Value",
                table: "SettingOption",
                columns: new[] { "Category", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VitalRangeSetting_Metric",
                table: "VitalRangeSetting",
                column: "Metric",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettingOption");

            migrationBuilder.DropTable(
                name: "VitalRangeSetting");
        }
    }
}
