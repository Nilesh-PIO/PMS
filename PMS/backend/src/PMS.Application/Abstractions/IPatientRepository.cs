using System.Linq.Expressions;
using PMS.Domain.Entities;

namespace PMS.Application.Abstractions;

/// <summary>
/// Everything a store needs in order to answer one search: what matches, in what order, how many
/// (planning-pms-verification.md, F-7 point 1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the rules travel as expression trees.</b> They are built by <c>PatientSearchRules</c> in
/// the application layer, where the C-22 search semantics are readable and unit-testable, and
/// executed by EF Core, which turns them into the <c>WHERE</c> and <c>ORDER BY</c> of a single SQL
/// statement. That is what makes the plan's "server-side <c>TOP 20</c>, and no client-side
/// filtering" literally true: the database does the matching, the ranking and the limiting, and
/// exactly <see cref="Take"/> rows come back over the wire.
/// </para>
/// <para>
/// The alternative — a repository method per search shape — would put the search definition in the
/// infrastructure layer and force the unit tests to re-state it in LINQ-to-Objects. Two copies of a
/// matching rule drift, and the drift shows up as "it finds them on my machine".
/// </para>
/// </remarks>
/// <param name="Match">The <c>WHERE</c> clause: selectable, and matching the query.</param>
/// <param name="Rank">The primary sort key. Lower sorts first.</param>
/// <param name="Take">Row limit, already clamped.</param>
public sealed record PatientSearchSpecification(
    Expression<Func<Patient, bool>> Match,
    Expression<Func<Patient, int>> Rank,
    int Take);

/// <summary>
/// A patient's id paired with the name used for matching — the whole payload of the fuzzy pass.
/// </summary>
/// <remarks>
/// Two narrow columns rather than whole entities, because the fallback scores every selectable name
/// and only a handful survive. Materialising 5,000 full patient rows in order to discard 4,990 of
/// them is the version of this that misses the plan's latency budget.
/// </remarks>
/// <param name="Id">The patient.</param>
/// <param name="NormalizedName">The value <c>PatientNormalizer.NormalizeName</c> produced on save.</param>
public sealed record PatientNameKey(Guid Id, string NormalizedName);

/// <summary>
/// The only way the application layer reaches the Patient table
/// (planning-pms-verification.md, section 2, Data access).
/// </summary>
/// <remarks>
/// Deliberately narrow at F-5: add, find by id, find by submission token, save. F-6 adds candidate
/// lookup, F-7 adds search and F-8 adds the update path. Each arrives with the feature that needs
/// it, so no method here is unused and unexercised.
/// </remarks>
public interface IPatientRepository
{
    /// <summary>The patient with this id, or null. Tracked, so F-8's edit path can save it.</summary>
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The patient created by this submission token, or null (E-43, E-46).
    /// </summary>
    /// <remarks>
    /// The fast path for a replayed submit. It is <em>not</em> the guarantee — two near-simultaneous
    /// clicks can both see null here — which is why the unique index exists and why
    /// <see cref="AddAsync"/>'s caller handles a constraint violation by asking this method again.
    /// </remarks>
    Task<Patient?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken cancellationToken);

    /// <summary>Stages a new patient for insert.</summary>
    Task AddAsync(Patient patient, CancellationToken cancellationToken);

    /// <summary>
    /// Commits staged changes. Throws <c>DbUpdateException</c> on a unique-index violation, which
    /// is the signal a concurrent submit replay produced.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// True if the exception represents a violation of the submission-token unique index.
    /// </summary>
    /// <remarks>
    /// Asking the infrastructure layer rather than pattern-matching a SQL Server error number in
    /// the application layer, which would tie this service to one database engine and be silently
    /// wrong on any other.
    /// </remarks>
    bool IsDuplicateSubmissionConflict(Exception exception);

    // --- F-7 -------------------------------------------------------------------

    /// <summary>
    /// The best <c>spec.Take</c> matches for a search, already filtered, ranked and limited by the
    /// store (plan F-7 point 1: server-side <c>TOP 20</c>, no client-side filtering).
    /// </summary>
    /// <remarks>
    /// Read-only. Nothing in F-7 writes, so the rows come back untracked — a change-tracking
    /// snapshot of twenty patients on every keystroke is pure cost, and an untracked read cannot
    /// accidentally persist a mutation made while formatting a DTO.
    /// </remarks>
    Task<IReadOnlyList<Patient>> SearchAsync(
        PatientSearchSpecification specification,
        CancellationToken cancellationToken);

    /// <summary>
    /// The <paramref name="take"/> patients most recently dealt with, most recent first.
    /// </summary>
    /// <remarks>
    /// Retired and merged records are never in this list, whatever the search toggle says: "recent"
    /// is a shortcut to get back to work, and the shortcut should not offer a record the clinic has
    /// already retired.
    /// </remarks>
    Task<IReadOnlyList<Patient>> GetRecentAsync(int take, CancellationToken cancellationToken);

    /// <summary>
    /// Every selectable patient's id and normalized name, for the fuzzy fallback (E-30).
    /// </summary>
    /// <remarks>
    /// Called only when an exact search has already returned nothing, so the cost lands on the path
    /// where the alternative outcome is a duplicate patient record.
    /// </remarks>
    Task<IReadOnlyList<PatientNameKey>> GetNameKeysAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    /// <summary>The patients with these ids, untracked and in no particular order.</summary>
    Task<IReadOnlyList<Patient>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
}
