using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Abstractions;
using PMS.Domain.Entities;
using PMS.Infrastructure.Persistence.Configurations;

namespace PMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPatientRepository"/>. The only type in the solution that
/// reads or writes the Patients table.
/// </summary>
public sealed class PatientRepository : IPatientRepository
{
    /// <summary>SQL Server error numbers for a unique index or unique constraint violation.</summary>
    /// <remarks>
    /// 2601 is "cannot insert duplicate key row in object ... with unique index"; 2627 is the
    /// unique-constraint equivalent. Both are checked because which one is raised depends on how
    /// the uniqueness was declared, and this code should not depend on that detail.
    /// </remarks>
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private readonly PmsDbContext _db;

    public PatientRepository(PmsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        // Tracked: F-8 loads a patient through this method in order to edit and save it, and an
        // AsNoTracking read would silently do nothing on save.
        _db.Patients.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Patient?> GetBySubmissionIdAsync(
        Guid submissionId,
        CancellationToken cancellationToken) =>
        _db.Patients.FirstOrDefaultAsync(p => p.SubmissionId == submissionId, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Patient patient, CancellationToken cancellationToken) =>
        await _db.Patients.AddAsync(patient, cancellationToken);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Lives in the infrastructure layer because recognising a unique-index violation means knowing
    /// SQL Server error numbers, and the application layer must not. Swapping the database engine
    /// changes this method and nothing above it.
    /// </para>
    /// <para>
    /// <b>Matched on the index name, not merely on "some unique violation".</b> The Patients table
    /// grows more unique indexes as F-6 and F-7 land, and treating any of them as a replayed submit
    /// would make the service return the wrong patient. A conflict on a different index is not this
    /// one, and is left to surface.
    /// </para>
    /// </remarks>
    public bool IsDuplicateSubmissionConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql
                && (sql.Number == UniqueIndexViolation || sql.Number == UniqueConstraintViolation)
                && sql.Message.Contains(
                    PatientConfiguration.SubmissionIndexName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
