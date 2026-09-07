using PMS.Application.Abstractions;
using PMS.Application.Services;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IPatientRepository"/> for the F-5 and F-7 service tests.
/// </summary>
/// <remarks>
/// <para>
/// Models the two behaviours <c>PatientService</c> actually leans on: a row added before a save is
/// not visible to a read until <see cref="SaveChangesAsync"/> runs (matching EF's staged-insert
/// semantics), and the submission-token uniqueness is enforced at save time by throwing - because
/// in production that decision is made by a database index, not by the service.
/// </para>
/// <para>
/// <see cref="FailNextSaveWithSubmissionConflict"/> exists so a test can reproduce the double-click
/// race deterministically: two requests both read nothing, both try to insert, one loses. That
/// window is real and about 20 ms wide, so it cannot be reproduced by timing and has to be
/// injected.
/// </para>
/// </remarks>
public sealed class FakePatientRepository : IPatientRepository
{
    private readonly List<Patient> _saved = [];
    private readonly List<Patient> _pending = [];

    /// <summary>Everything committed, for assertions about what was actually persisted.</summary>
    public IReadOnlyList<Patient> Saved => _saved;

    /// <summary>How many times <see cref="SaveChangesAsync"/> was called.</summary>
    public int SaveCount { get; private set; }

    /// <summary>
    /// When set, the next save throws as the unique index would, and the row that "won" is
    /// committed first - exactly what the losing request of a double-click sees.
    /// </summary>
    public Patient? FailNextSaveWithSubmissionConflict { get; set; }

    /// <summary>Commits a patient directly, standing in for a row that already existed.</summary>
    public FakePatientRepository Seed(Patient patient)
    {
        _saved.Add(patient);
        return this;
    }

    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_saved.FirstOrDefault(p => p.Id == id));

    public Task<Patient?> GetBySubmissionIdAsync(
        Guid submissionId,
        CancellationToken cancellationToken) =>
        Task.FromResult(_saved.FirstOrDefault(p => p.SubmissionId == submissionId));

    /// <summary>
    /// F-6. The same exact-equality pre-filter the EF implementation runs, in memory.
    /// </summary>
    /// <remarks>
    /// Kept faithful to the production query in the two ways the service actually depends on: it
    /// returns rows matching <em>either</em> key rather than both, and it does not filter out
    /// inactive or already-merged patients. A fake that quietly hid those would let a test pass here
    /// that the database would fail.
    /// </remarks>
    public Task<IReadOnlyList<Patient>> FindDuplicateCandidatesAsync(
        string? phoneMatchKey,
        DateOnly? dateOfBirth,
        Guid? excludePatientId,
        CancellationToken cancellationToken)
    {
        if (phoneMatchKey is null && dateOfBirth is null)
        {
            return Task.FromResult<IReadOnlyList<Patient>>([]);
        }

        var matches = _saved
            .Where(p => excludePatientId is null || p.Id != excludePatientId)
            .Where(p =>
                (phoneMatchKey is not null
                    && string.Equals(p.PhoneMatchKey, phoneMatchKey, StringComparison.Ordinal))
                || (dateOfBirth is not null && p.DateOfBirth == dateOfBirth))
            .OrderBy(p => p.RegisteredUtc)
            .ThenBy(p => p.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<Patient>>(matches);
    }

    public Task AddAsync(Patient patient, CancellationToken cancellationToken)
    {
        _pending.Add(patient);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;

        if (FailNextSaveWithSubmissionConflict is { } winner)
        {
            FailNextSaveWithSubmissionConflict = null;
            _pending.Clear();

            // The winner is committed first, then the loser's insert is rejected - the order the
            // database produces it in.
            _saved.Add(winner);
            throw new InvalidOperationException(
                $"Violation of UNIQUE KEY constraint 'UX_Patient_SubmissionId'.");
        }

        _saved.AddRange(_pending);
        _pending.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Recognises the exception <see cref="SaveChangesAsync"/> throws. The production
    /// implementation inspects a SqlException number and the index name; this one matches on the
    /// same index name, so the service's branch is exercised without a database.
    /// </summary>
    public bool IsDuplicateSubmissionConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("UX_Patient_SubmissionId", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // --- F-7 -------------------------------------------------------------------

    /// <summary>How many times <see cref="GetNameKeysAsync"/> ran — the fuzzy fallback's tell.</summary>
    /// <remarks>
    /// Exposed so a test can assert the expensive path is <em>not</em> taken when the exact search
    /// already found something. "The fallback only runs on an empty result" is a performance
    /// promise in the plan, and a promise nothing checks is a promise that quietly stops holding.
    /// </remarks>
    public int NameKeyReadCount { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>This runs the production search rules, not a re-implementation of them.</b> The
    /// specification arrives carrying the same <c>PatientSearchRules</c> expression trees that EF
    /// Core turns into SQL; here they are simply compiled and run against a list. That is what
    /// makes a unit test of ranking meaningful — a hand-written LINQ-to-Objects copy of the rule
    /// would only ever prove that the copy agrees with itself.
    /// </para>
    /// <para>
    /// The sort chain after the rank is duplicated from <c>PatientRepository.SearchAsync</c>,
    /// because that part genuinely does live in the repository. <c>PatientSearchEndpointTests</c>
    /// asserts the real SQL ordering against a seeded set, so the two are checked against each
    /// other rather than assumed equal.
    /// </para>
    /// </remarks>
    public Task<IReadOnlyList<Patient>> SearchAsync(
        PatientSearchSpecification specification,
        CancellationToken cancellationToken)
    {
        var match = specification.Match.Compile();
        var rank = specification.Rank.Compile();

        IReadOnlyList<Patient> results = _saved
            .Where(match)
            .OrderBy(rank)
            .ThenByDescending(p => p.RegisteredUtc)
            .ThenBy(p => p.FullName, StringComparer.Ordinal)
            .ThenBy(p => p.Id)
            .Take(specification.Take)
            .ToList();

        return Task.FromResult(results);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Patient>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        IReadOnlyList<Patient> results = _saved
            .Where(p => p.Status == PatientStatus.Active && p.MergedIntoPatientId is null)
            .OrderByDescending(p => p.RegisteredUtc)
            .ThenBy(p => p.FullName, StringComparer.Ordinal)
            .ThenBy(p => p.Id)
            .Take(take)
            .ToList();

        return Task.FromResult(results);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PatientNameKey>> GetNameKeysAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        NameKeyReadCount++;

        var selectable = PatientSearchRules.IsSelectable(includeInactive).Compile();

        IReadOnlyList<PatientNameKey> results = _saved
            .Where(selectable)
            .Select(p => new PatientNameKey(p.Id, p.NormalizedName))
            .ToList();

        return Task.FromResult(results);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Patient>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Patient> results = _saved.Where(p => ids.Contains(p.Id)).ToList();
        return Task.FromResult(results);
    }
}
