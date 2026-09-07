using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Abstractions;
using PMS.Application.Services;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
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

    // --- F-7 -------------------------------------------------------------------

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>One statement, and the database does all three jobs.</b> The match, the ranking and the
    /// row limit are composed into a single <c>SELECT TOP (n) ... WHERE ... ORDER BY</c>, so exactly
    /// the rows that will be shown cross the wire. Plan F-7 point 1 is explicit that the 2-second
    /// budget is met by "server-side <c>TOP 20</c>, and no client-side filtering" — this method is
    /// where that is either true or merely a comment.
    /// </para>
    /// <para>
    /// <b>Both matching columns are already indexed.</b> F-5's configuration created the
    /// nonclustered indexes on <c>NormalizedName</c> and <c>NormalizedPhone</c> (plan F-7 point 2
    /// attributes them to F-6; F-5 created them early, with a comment saying so, which is why F-7
    /// adds no migration of its own). A prefix predicate seeks them; a substring or suffix predicate
    /// cannot, by definition, and scans the narrow index rather than the table — which at the plan's
    /// 5,000-patient design point is a few hundred kilobytes.
    /// <c>PatientSearchEndpointTests</c> measures this against a seeded 5,000-row set rather than
    /// assuming it.
    /// </para>
    /// <para>
    /// <b>Tie-breaks after the rank are deliberate and total.</b> Most recent registration first
    /// (the person most likely to be the one being looked for), then name, then id. The id is what
    /// makes the order <em>total</em>: without it, two rows equal on every other key could come back
    /// in either order, and a <c>TOP 20</c> over a non-deterministic sort silently returns a
    /// different twenty on different runs.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<Patient>> SearchAsync(
        PatientSearchSpecification specification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specification);

        return await _db.Patients
            .AsNoTracking()
            .Where(specification.Match)
            .OrderBy(specification.Rank)
            .ThenByDescending(p => p.RegisteredUtc)
            .ThenBy(p => p.FullName)
            .ThenBy(p => p.Id)
            .Take(specification.Take)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>ASSUMPTION (plan F-7 point 3; BRD L159 "View recent patients") — flagged for the plan
    /// owner.</b> Neither the BRD nor the plan defines whether "recent" means recently
    /// <em>registered</em>, recently <em>viewed</em>, or recently <em>seen in consultation</em>;
    /// <c>modules/05-search-navigation.md</c> names this gap explicitly. Plan F-7 point 2 settles it
    /// as far as it can by stating F-7 adds <b>no new entities</b>, which rules out a
    /// recently-viewed access log, and <c>Visit</c> is F-10 and does not exist yet, which rules out
    /// recently-seen. So this orders by <c>RegisteredUtc</c> descending — the most recently
    /// registered patients, which for a clinic starting out is the same set as the people currently
    /// being dealt with.
    /// </para>
    /// <para>
    /// <b>The intended end state is "most recent clinical activity".</b> When F-10 lands this
    /// becomes <c>ORDER BY COALESCE(last visit date, RegisteredUtc) DESC</c> — a change to this one
    /// method, with no change to the endpoint, the DTO, or any client. If the physician instead
    /// wants recently <em>viewed</em>, that needs a new entity and is therefore a plan amendment,
    /// not a change here.
    /// </para>
    /// <para>
    /// Retired and merged records are excluded unconditionally, whatever the search toggle says:
    /// this list is a shortcut back to work, and a shortcut should not offer a record the clinic has
    /// already retired.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<Patient>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken) =>
        await _db.Patients
            .AsNoTracking()
            .Where(p => p.Status == PatientStatus.Active && p.MergedIntoPatientId == null)
            .OrderByDescending(p => p.RegisteredUtc)
            // Total order again: two patients registered in the same millisecond must not swap
            // places between page loads.
            .ThenBy(p => p.FullName)
            .ThenBy(p => p.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Projected to two columns in SQL, so the fuzzy pass reads a few dozen bytes a patient rather
    /// than a full row with its rowversion and its check-constrained age columns. At the plan's
    /// 5,000-patient design point that is the difference between this list being free and being the
    /// slowest thing in the request.
    /// </remarks>
    public async Task<IReadOnlyList<PatientNameKey>> GetNameKeysAsync(
        bool includeInactive,
        CancellationToken cancellationToken) =>
        await _db.Patients
            .AsNoTracking()
            .Where(PatientSearchRules.IsSelectable(includeInactive))
            .Select(p => new PatientNameKey(p.Id, p.NormalizedName))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Patient>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            // Short-circuit rather than emit `WHERE Id IN ()`, which still costs a round trip to
            // learn what the caller already knows.
            return [];
        }

        return await _db.Patients
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }
}
