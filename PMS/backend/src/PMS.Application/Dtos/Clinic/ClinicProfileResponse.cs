using PMS.Domain.Enums;

namespace PMS.Application.Dtos.Clinic;

/// <summary>
/// The wire shape of the clinic profile (planning-pms-verification.md, F-3 point 3).
/// A DTO, not the EF entity - no entity crosses the wire (section 2, API shape).
/// </summary>
/// <param name="ClinicName">Clinic name, printed as the prescription header's first line.</param>
/// <param name="AddressLines">Newline-separated postal address, printed verbatim.</param>
/// <param name="DoctorName">The physician's name as printed.</param>
/// <param name="DoctorRegistrationNo">Medical council registration number.</param>
/// <param name="PrescriptionFooter">Free text printed at the foot of every prescription.</param>
/// <param name="TemperatureUnit">The unit every temperature is recorded and shown in (E-24).</param>
/// <param name="SignatureImageDataUrl">
/// The signature as a <c>data:image/png;base64,...</c> URL, or <c>null</c> when none has been
/// uploaded. <b>ASSUMPTION</b> (plan F-3 point 3 names the DTO but not its fields): the bytes
/// are inlined rather than exposed through a fifth <c>GET /api/clinic-profile/signature</c>
/// route, because the plan's route table has exactly four entries and adding one would change
/// what F-14 builds against. The payload is bounded by the same 200 KB cap the upload enforces,
/// and the profile is read only on the two settings screens - never per patient or per visit.
/// <c>null</c> is a legitimate answer, and the caller must render a ruled signature area for it
/// rather than an image element with no source.
/// </param>
/// <param name="IsSetupComplete">
/// Whether the clinic identity is complete enough to print (E-1). Derived by the service from
/// the stored values on every read, so an out-of-band edit cannot leave it stale.
/// </param>
/// <param name="UpdatedUtc">When the profile was last saved.</param>
public sealed record ClinicProfileResponse(
    string ClinicName,
    string AddressLines,
    string DoctorName,
    string DoctorRegistrationNo,
    string? PrescriptionFooter,
    TemperatureUnit TemperatureUnit,
    string? SignatureImageDataUrl,
    bool IsSetupComplete,
    DateTimeOffset UpdatedUtc);
