using PMS.Application.Dtos.Patients;

namespace PMS.Application.Abstractions;

/// <summary>
/// F-5's application service — registration and profile read
/// (planning-pms-verification.md, F-5).
/// </summary>
public interface IPatientService
{
    /// <summary>
    /// Registers a patient, or returns the one this submission token already created (E-43, E-46).
    /// </summary>
    /// <param name="request">The registration form.</param>
    /// <param name="confirmDuplicate">
    /// F-6. <c>false</c> — the default — runs the duplicate check first and refuses with
    /// <see cref="Exceptions.DuplicatePatientException"/> if the person may already be on file.
    /// <c>true</c> is the physician having looked at the candidates and said "register them anyway",
    /// and is always honoured (REC-2: warn, never block).
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <exception cref="Exceptions.ValidationFailedException">
    /// The name is missing, the date of birth is in the future, the age fields are in a shape that
    /// would corrupt the record later (E-9), or the gender is not an offered option.
    /// </exception>
    /// <exception cref="Exceptions.DuplicatePatientException">
    /// F-6. A patient already on file matches the identity rule and
    /// <paramref name="confirmDuplicate"/> is false. Thrown <em>before</em> anything is written.
    /// </exception>
    Task<PatientResponse> CreateAsync(
        CreatePatientRequest request,
        bool confirmDuplicate,
        CancellationToken cancellationToken);

    /// <summary>The full profile.</summary>
    /// <exception cref="Exceptions.NotFoundException">No patient has this id.</exception>
    Task<PatientDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken);
}
