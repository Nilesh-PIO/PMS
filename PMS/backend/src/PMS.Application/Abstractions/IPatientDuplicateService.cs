using PMS.Application.Dtos.Patients;

namespace PMS.Application.Abstractions;

/// <summary>
/// F-6. Finds patients who may already be the person in front of the physician, and records a
/// non-destructive duplicate pointer when they are
/// (planning-pms-verification.md, F-6; brainstorm REC-2, RSK-2, E-25, E-26, E-27, E-28, E-30).
/// </summary>
/// <remarks>
/// <b>Two methods, and neither of them deletes anything.</b> That is not an accident of scope —
/// plan F-6 point 5 states it as the feature's contract: "No destructive operation exists in this
/// feature's API surface at all." A duplicate is a thing to notice and annotate, never a thing to
/// resolve by removing a record, because the record being removed is the one carrying a patient's
/// visit history.
/// </remarks>
public interface IPatientDuplicateService
{
    /// <summary>
    /// Patients who match the identity rule, best match first. Empty when nothing matches — which
    /// is the normal answer.
    /// </summary>
    Task<IReadOnlyList<DuplicateCandidateResponse>> FindCandidatesAsync(
        DuplicateCheckRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Points <paramref name="patientId"/> at a surviving record and retires it. Returns the marked
    /// patient. Deletes nothing and rewrites nothing on either record.
    /// </summary>
    Task<PatientResponse> MarkMergedAsync(
        Guid patientId,
        MarkMergedRequest request,
        CancellationToken cancellationToken);
}
