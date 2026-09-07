using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Abstractions;
using PMS.Application.Services;
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
    /// <remarks>
    /// <para>
    /// <b>Three query shapes rather than one predicate with null guards inside it.</b> A single
    /// <c>Where(p =&gt; (key != null &amp;&amp; p.PhoneMatchKey == key) || (dob != null &amp;&amp;
    /// p.DateOfBirth == dob))</c> reads more neatly and translates to SQL that carries both
    /// comparisons with a null-valued parameter in one of them - which SQL Server cannot seek on, so
    /// the duplicate check degrades to a scan of every patient in the clinic at exactly the moment
    /// the physician is waiting on it. Branching in C# leaves each shape a plain equality the index
    /// can serve.
    /// </para>
    /// <para>
    /// <c>AsNoTracking</c> because these rows are read, projected and discarded. Tracking fifty
    /// patients on every registration would put them in the change tracker, where the
    /// <c>SaveChanges</c> that follows on the registration path would then consider writing them.
    /// </para>
    /// <para>
    /// Ordered before it is limited, so the rows returned are a defined set rather than whatever
    /// the storage engine happened to offer first.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<Patient>> FindDuplicateCandidatesAsync(
        string? phoneMatchKey,
        DateOnly? dateOfBirth,
        Guid? excludePatientId,
        CancellationToken cancellationToken)
    {
        if (phoneMatchKey is null && dateOfBirth is null)
        {
            return [];
        }

        var query = _db.Patients.AsNoTracking();

        if (excludePatientId is { } exclude)
        {
            query = query.Where(p => p.Id != exclude);
        }

        if (phoneMatchKey is not null && dateOfBirth is { } bothDob)
        {
            query = query.Where(p => p.PhoneMatchKey == phoneMatchKey || p.DateOfBirth == bothDob);
        }
        else if (phoneMatchKey is not null)
        {
            query = query.Where(p => p.PhoneMatchKey == phoneMatchKey);
        }
        else
        {
            var dob = dateOfBirth!.Value;
            query = query.Where(p => p.DateOfBirth == dob);
        }

        return await query
            .OrderBy(p => p.RegisteredUtc)
            .ThenBy(p => p.Id)
            .Take(PatientDuplicateService.CandidateLimit)
            .ToListAsync(cancellationToken);
    }

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
