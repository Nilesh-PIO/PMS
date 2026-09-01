using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Migrations
{
    /// <summary>
    /// The migration planning-pms-verification.md names for F-2 ("Migration: <c>AddAppUser</c>").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Intentionally empty.</b> F-1's plan section states the context is created "with no
    /// entity sets beyond <c>AppUser</c>", so the <c>AppUsers</c> table, its unique index on
    /// <c>UserName</c> and every column in the plan's section 4 shape
    /// (<c>Id</c>, <c>UserName</c>, <c>PasswordHash</c>, <c>SecurityStamp</c>,
    /// <c>FailedAttempts</c>, <c>LockoutEndUtc</c>, <c>LastLoginUtc</c>) were already created
    /// by <c>20260825170916_InitialCreate</c>. F-1's own tracker entry flagged exactly this:
    /// "AppUser is created by InitialCreate rather than by F-2's AddAppUser, so AddAppUser will
    /// be an alter rather than a create."
    /// </para>
    /// <para>
    /// Confirmed before writing this, not assumed:
    /// <c>dotnet ef migrations has-pending-model-changes</c> reports "No changes have been made
    /// to the model since the last migration". F-2 needs nothing added to the table - password
    /// hashing writes to the existing <c>PasswordHash</c>, and lockout stays unimplemented
    /// because F-21 is Blocked on C-44.
    /// </para>
    /// <para>
    /// It is kept rather than deleted so the schema history contains the migration the plan
    /// names and records that F-2 examined this table and found it already correct. A later
    /// change to <c>AppUser</c> gets its own named migration, per section 2's "every schema
    /// change is a named migration".
    /// </para>
    /// </remarks>
    public partial class AddAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema change: see the remarks above.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo.
        }
    }
}
