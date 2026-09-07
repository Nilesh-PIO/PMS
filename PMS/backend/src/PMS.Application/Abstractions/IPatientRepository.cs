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
