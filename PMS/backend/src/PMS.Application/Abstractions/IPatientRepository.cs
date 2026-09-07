using PMS.Domain.Entities;

namespace PMS.Application.Abstractions;

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

    /// <summary>
    /// F-6. Every patient who shares <paramref name="phoneMatchKey"/> or
    /// <paramref name="dateOfBirth"/>, excluding <paramref name="excludePatientId"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the exact-equality half of the identity rule, and it is a complete pre-filter.</b>
    /// F-6's rule is <c>name similarity ≥ threshold AND (same phone OR same date of birth)</c>, so
    /// no candidate can ever be flagged without satisfying one of the two equality tests this method
    /// runs. That is what lets the expensive half — fuzzy name comparison, which SQL Server cannot
    /// express without a CLR function — run in memory over a handful of rows instead of over every
    /// patient in the clinic.
    /// </para>
    /// <para>
    /// Returns an empty list when both arguments are null: a registration with neither a phone nor a
    /// date of birth cannot satisfy the rule, so asking the database is pointless rather than merely
    /// slow.
    /// </para>
    /// <para>
    /// Inactive and already-merged patients <em>are</em> returned. A retired record is exactly the
    /// one a returning patient is about to be re-registered against (E-25), and hiding half of a
    /// duplicate cluster from the only person who can interpret it is not a safety feature.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<Patient>> FindDuplicateCandidatesAsync(
        string? phoneMatchKey,
        DateOnly? dateOfBirth,
        Guid? excludePatientId,
        CancellationToken cancellationToken);

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
}
