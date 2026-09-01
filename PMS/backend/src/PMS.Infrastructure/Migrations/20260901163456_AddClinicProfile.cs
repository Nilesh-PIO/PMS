using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Migrations
{
    /// <summary>
    /// F-3. Creates the singleton ClinicProfile table
    /// (planning-pms-verification.md, F-3 point 2: migration "AddClinicProfile").
    /// </summary>
    /// <remarks>
    /// <c>CK_ClinicProfile_SingletonRow</c> is the load-bearing part. One physician, one clinic,
    /// one identity on the prescription - and because SQL Server via SSMS is a stated tool of this
    /// stack, a hand-run INSERT is a realistic way a second row would otherwise appear. The
    /// constraint means the database refuses it, not just the service. <c>Id</c> is deliberately
    /// not an identity column for the same reason.
    /// </remarks>
    public partial class AddClinicProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicProfile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ClinicName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLines = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DoctorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DoctorRegistrationNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SignatureImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PrescriptionFooter = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TemperatureUnit = table.Column<int>(type: "int", nullable: false),
                    IsSetupComplete = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicProfile", x => x.Id);
                    table.CheckConstraint("CK_ClinicProfile_SingletonRow", "[Id] = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicProfile");
        }
    }
}
