using PMS.Application.Dtos.Clinic;

namespace PMS.Application.Abstractions;

/// <summary>
/// F-3's application service: the clinic profile, and the first-run setup gate that depends on it
/// (planning-pms-verification.md, F-3).
/// </summary>
public interface IClinicProfileService
{
    /// <summary>
    /// The clinic profile.
    /// </summary>
    /// <returns><c>null</c> when first-run setup has never been saved - which is the state
    /// <c>GET /api/clinic-profile</c> reports as a 404 and the client renders as a blank
    /// first-run form.</returns>
    Task<ClinicProfileResponse?> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates or updates the singleton profile.
    /// </summary>
    /// <exception cref="Exceptions.ValidationFailedException">
    /// Any required field is blank, over length, or the temperature unit was not chosen.
    /// </exception>
    Task<ClinicProfileResponse> UpsertAsync(
        UpsertClinicProfileRequest request,
        CancellationToken cancellationToken);

    /// <summary>Stores an uploaded PNG signature.</summary>
    /// <param name="content">The raw uploaded bytes.</param>
    /// <exception cref="Exceptions.NotFoundException">No profile has been saved yet.</exception>
    /// <exception cref="Exceptions.ValidationFailedException">Not a PNG, or empty.</exception>
    /// <exception cref="Exceptions.PayloadTooLargeException">Larger than the 200 KB cap.</exception>
    Task<ClinicProfileResponse> SetSignatureAsync(
        byte[] content,
        CancellationToken cancellationToken);

    /// <summary>Removes the stored signature. The printed footer falls back to a ruled area.</summary>
    /// <exception cref="Exceptions.NotFoundException">No profile has been saved yet.</exception>
    Task<ClinicProfileResponse> ClearSignatureAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether the clinic identity is complete enough to print (E-1). Used by
    /// <c>AuthService</c> to populate <c>SessionResponse.setupComplete</c>, which is what drives
    /// the client's redirect to <c>/setup</c>.
    /// </summary>
    Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Throws unless the clinic identity is complete. The server-side half of E-1: the check the
    /// prescription endpoints run before they will produce a document.
    /// </summary>
    /// <exception cref="Exceptions.DomainRuleException">
    /// Setup is incomplete. Surfaces as a 409 with <c>ruleType = "setup-incomplete"</c>.
    /// </exception>
    Task EnsureSetupCompleteAsync(CancellationToken cancellationToken);
}
