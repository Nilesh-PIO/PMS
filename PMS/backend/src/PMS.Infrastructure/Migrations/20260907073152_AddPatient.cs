using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    ApproxAgeYears = table.Column<int>(type: "int", nullable: true),
                    AgeRecordedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrimaryPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    NormalizedPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AltContact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegisteredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InactiveReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MergedIntoPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.CheckConstraint("CK_Patient_AgeShape", "([DateOfBirth] IS NULL OR [ApproxAgeYears] IS NULL)\nAND (\n    ([ApproxAgeYears] IS NULL AND [AgeRecordedOn] IS NULL)\n    OR ([ApproxAgeYears] IS NOT NULL AND [AgeRecordedOn] IS NOT NULL)\n)");
                    table.ForeignKey(
                        name: "FK_Patients_Patients_MergedIntoPatientId",
                        column: x => x.MergedIntoPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_MergedIntoPatientId",
                table: "Patients",
                column: "MergedIntoPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_NormalizedName",
                table: "Patients",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_NormalizedPhone",
                table: "Patients",
                column: "NormalizedPhone");

            migrationBuilder.CreateIndex(
                name: "UX_Patient_SubmissionId",
                table: "Patients",
                column: "SubmissionId",
                unique: true,
                filter: "[SubmissionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Patients");
        }
    }
}
