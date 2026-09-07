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
    /// <exception cref="Exceptions.ValidationFailedException">
    /// The name is missing, the date of birth is in the future, the age fields are in a shape that
    /// would corrupt the record later (E-9), or the gender is not an offered option.
    /// </exception>
    Task<PatientResponse> CreateAsync(CreatePatientRequest request, CancellationToken cancellationToken);

    /// <summary>The full profile.</summary>
    /// <exception cref="Exceptions.NotFoundException">No patient has this id.</exception>
    Task<PatientDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken);
}
