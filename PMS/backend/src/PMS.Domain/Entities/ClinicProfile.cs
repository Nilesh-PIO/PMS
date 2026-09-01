using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// The clinic's own identity: the header, footer and signature that turn a list of medicines
/// into a prescription somebody can dispense against
/// (planning-pms-verification.md, section 4 and F-3; brainstorm C-32 / RSK-4 / REC-4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Singleton.</b> One physician, one clinic: exactly one row, always <c>Id == 1</c>. This is
/// enforced three ways so it cannot drift - the service only ever reads and writes
/// <see cref="SingletonId"/>, the column is not an identity column, and the table carries a
/// check constraint pinning <c>Id = 1</c>. The constraint matters because SSMS is a first-class
/// tool in this project's stack: a hand-run INSERT is a realistic way a second row would
/// otherwise appear, and "which clinic name does the prescription use?" is not a question that
/// should ever have two answers.
/// </para>
/// <para>
/// <b>E-1.</b> <see cref="IsSetupComplete"/> is what stands between the clinic and a printed
/// prescription bearing no clinic identity - an unusable clinical document. It is never assigned
/// from a request; the service derives it from the stored values. See
/// <c>ClinicProfileService</c>.
/// </para>
/// </remarks>
public class ClinicProfile
{
    /// <summary>The only primary key this table ever holds.</summary>
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>Clinic name, printed as the first line of the prescription header.</summary>
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>
    /// The clinic's postal address. Newline-separated free text rather than structured fields:
    /// it is printed verbatim and never queried, parsed or validated by locality.
    /// </summary>
    public string AddressLines { get; set; } = string.Empty;

    /// <summary>The physician's name as it appears on the prescription.</summary>
    public string DoctorName { get; set; } = string.Empty;

    /// <summary>Medical council registration number, printed under the signature.</summary>
    public string DoctorRegistrationNo { get; set; } = string.Empty;

    /// <summary>
    /// The uploaded signature image (PNG, at most 200 KB), or <c>null</c> when the physician has
    /// not supplied one. Null is a supported end state, not a missing value: the printed footer
    /// renders a ruled signature area instead, never a broken-image placeholder (plan F-3 point 1).
    /// </summary>
    public byte[]? SignatureImage { get; set; }

    /// <summary>Free text printed at the foot of every prescription. At most 500 characters.</summary>
    public string? PrescriptionFooter { get; set; }

    /// <summary>The unit every temperature in this clinic is recorded and shown in (E-24).</summary>
    public TemperatureUnit TemperatureUnit { get; set; } = TemperatureUnit.Unspecified;

    /// <summary>
    /// Whether the clinic identity is complete enough to print (E-1). Derived by the service from
    /// the four columns above - never taken from a request body.
    /// </summary>
    public bool IsSetupComplete { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }
}
