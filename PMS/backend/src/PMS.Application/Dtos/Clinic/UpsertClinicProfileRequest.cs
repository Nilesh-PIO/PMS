using PMS.Domain.Enums;

namespace PMS.Application.Dtos.Clinic;

/// <summary>
/// Request body for <c>PUT /api/clinic-profile</c> (planning-pms-verification.md, F-3 point 3).
/// </summary>
/// <remarks>
/// Deliberately carries neither <c>IsSetupComplete</c> nor the signature bytes. The gate that
/// stands between the clinic and an unusable printed prescription (E-1) must not be something a
/// client can simply assert, and the signature has its own two routes because a multipart upload
/// and a JSON form save fail in different ways and should not be able to fail together.
/// </remarks>
public sealed class UpsertClinicProfileRequest
{
    public string? ClinicName { get; set; }

    public string? AddressLines { get; set; }

    public string? DoctorName { get; set; }

    public string? DoctorRegistrationNo { get; set; }

    public string? PrescriptionFooter { get; set; }

    /// <summary>
    /// Nullable so that "the field was absent from the body" and "the physician chose Celsius"
    /// are distinguishable. An absent or <see cref="TemperatureUnit.Unspecified"/> value is a
    /// validation failure, never a silent default (E-24).
    /// </summary>
    public TemperatureUnit? TemperatureUnit { get; set; }
}
