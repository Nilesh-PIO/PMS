using PMS.Application.Dtos.Patients;

namespace PMS.Application.Abstractions;

/// <summary>
/// F-7 — finding a patient (planning-pms-verification.md, F-7; BRD L93, L158-159).
/// </summary>
/// <remarks>
/// Separate from <see cref="IPatientService"/> rather than bolted onto it. Registration and search
/// are different jobs with different collaborators — one writes a row and validates a form, the
/// other reads and ranks — and F-8's edit path, F-6's duplicate check and F-9's appointment picker
/// each want one of them and not the other.
/// </remarks>
public interface IPatientSearchService
{
    /// <summary>
    /// Patients matching <paramref name="query"/> by name or phone, best match first.
    /// </summary>
    /// <param name="query">The text as the physician typed it — never pre-normalized by a client.</param>
    /// <param name="includeInactive">
    /// When true, retired and merged records are offered too, each flagged as such.
    /// </param>
    /// <param name="take">How many rows at most. Clamped, never rejected.</param>
    /// <exception cref="Exceptions.ValidationFailedException">
    /// The query is blank or shorter than <c>PatientSearchRules.MinimumQueryLength</c>.
    /// </exception>
    Task<IReadOnlyList<PatientSummaryResponse>> SearchAsync(
        string? query,
        bool includeInactive,
        int? take,
        CancellationToken cancellationToken);

    /// <summary>
    /// The patients most recently dealt with, most recent first. Empty on a fresh install (E-2).
    /// </summary>
    Task<IReadOnlyList<PatientSummaryResponse>> GetRecentAsync(
        int? take,
        CancellationToken cancellationToken);
}
