using PMS.Application.Abstractions;
using PMS.Application.Dtos.Clinic;
using PMS.Application.Exceptions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Services;

/// <summary>
/// F-3. Owns the clinic profile and the first-run setup gate
/// (planning-pms-verification.md, F-3; brainstorm E-1, C-32, RSK-4, REC-4).
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule this class exists to make unreachable (E-1).</b> A prescription printed with no
/// clinic name, no doctor name and no registration number is not a weak document - it is not a
/// document at all, and a pharmacy will refuse it. Rather than trusting each future print path to
/// remember to check, <see cref="EnsureSetupCompleteAsync"/> is a single call the prescription
/// endpoints make, and <c>IsSetupComplete</c> is <em>derived</em> here on every read and write
/// instead of being a flag anyone can set.
/// </para>
/// <para>
/// Deriving rather than storing-and-trusting is deliberate, and it is not paranoia in this stack:
/// SQL Server via SSMS is the project's stated database tool, so a hand-run UPDATE that blanks
/// <c>DoctorName</c> while leaving <c>IsSetupComplete = 1</c> is an ordinary Tuesday. The column
/// is still persisted, because F-14 and any reporting query need to read the answer without
/// re-implementing it - but this service recomputes it from the values themselves every time it
/// touches the row, so a stale flag self-corrects on the next read rather than silently arming a
/// broken print path.
/// </para>
/// </remarks>
public sealed class ClinicProfileService : IClinicProfileService
{
    /// <summary>
    /// Maximum accepted signature upload, in bytes (plan F-3 point 1: "an uploaded PNG &#8804; 200 KB").
    /// </summary>
    public const int MaxSignatureBytes = 200 * 1024;

    /// <summary>Maximum length of the free-text prescription footer (plan F-3 point 1).</summary>
    public const int MaxFooterLength = 500;

    /// <summary>Maximum length of the printed postal address.</summary>
    public const int MaxAddressLength = 500;

    private const int MaxNameLength = 200;
    private const int MaxRegistrationNoLength = 100;

    /// <summary>
    /// The eight-byte PNG signature (RFC 2083 section 3.1). Checked instead of the browser-supplied
    /// content type, which is a claim rather than a fact: a renamed .jpg would sail past a
    /// content-type check and then fail inside the PDF renderer at print time - the worst possible
    /// moment to discover it, with a patient waiting.
    /// </summary>
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly IClinicProfileRepository _profiles;
    private readonly IClock _clock;

    public ClinicProfileService(IClinicProfileRepository profiles, IClock clock)
    {
        _profiles = profiles;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ClinicProfileResponse?> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(cancellationToken);
        return profile is null ? null : ToResponse(profile);
    }

    /// <inheritdoc />
    public async Task<ClinicProfileResponse> UpsertAsync(
        UpsertClinicProfileRequest request,
        CancellationToken cancellationToken)
    {
        var validated = Validate(request);

        var profile = await _profiles.GetAsync(cancellationToken);
        var isNew = profile is null;

        profile ??= new ClinicProfile { Id = ClinicProfile.SingletonId };

        profile.ClinicName = validated.ClinicName;
        profile.AddressLines = validated.AddressLines;
        profile.DoctorName = validated.DoctorName;
        profile.DoctorRegistrationNo = validated.DoctorRegistrationNo;
        profile.PrescriptionFooter = validated.PrescriptionFooter;
        profile.TemperatureUnit = validated.TemperatureUnit;

        // The signature is untouched here on purpose: it has its own two routes, so re-saving the
        // header text can never quietly drop an image the physician uploaded weeks ago.
        profile.IsSetupComplete = Derive(profile);
        profile.UpdatedUtc = _clock.UtcNow;

        if (isNew)
        {
            await _profiles.AddAsync(profile, cancellationToken);
        }

        await _profiles.SaveChangesAsync(cancellationToken);

        return ToResponse(profile);
    }

    /// <inheritdoc />
    public async Task<ClinicProfileResponse> SetSignatureAsync(
        byte[] content,
        CancellationToken cancellationToken)
    {
        // Size first. Checking the magic bytes of a 40 MB upload would mean it is already in
        // memory, and "too large" is the answer regardless of what the file turns out to be.
        if (content.Length > MaxSignatureBytes)
        {
            throw new PayloadTooLargeException(
                $"The signature image must be {MaxSignatureBytes / 1024} KB or smaller.",
                MaxSignatureBytes);
        }

        if (content.Length == 0)
        {
            throw new ValidationFailedException("file", "Choose a signature image to upload.");
        }

        if (!IsPng(content))
        {
            throw new ValidationFailedException("file", "The signature image must be a PNG file.");
        }

        var profile = await RequireProfileAsync(cancellationToken);

        profile.SignatureImage = content;
        profile.UpdatedUtc = _clock.UtcNow;
        await _profiles.SaveChangesAsync(cancellationToken);

        return ToResponse(profile);
    }

    /// <inheritdoc />
    public async Task<ClinicProfileResponse> ClearSignatureAsync(CancellationToken cancellationToken)
    {
        var profile = await RequireProfileAsync(cancellationToken);

        // Removing the signature never affects IsSetupComplete: the plan's gate is clinic name,
        // doctor name, registration number and unit. A clinic that prints with a ruled signature
        // area for the physician to sign by hand is a supported way to work, not a broken setup.
        profile.SignatureImage = null;
        profile.UpdatedUtc = _clock.UtcNow;
        await _profiles.SaveChangesAsync(cancellationToken);

        return ToResponse(profile);
    }

    /// <inheritdoc />
    public async Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(cancellationToken);
        return profile is not null && Derive(profile);
    }

    /// <inheritdoc />
    public async Task EnsureSetupCompleteAsync(CancellationToken cancellationToken)
    {
        if (await IsSetupCompleteAsync(cancellationToken))
        {
            return;
        }

        throw new DomainRuleException(
            SetupIncompleteRuleType,
            "Clinic setup is incomplete. Add the clinic name, doctor name, registration number "
            + "and temperature unit before printing a prescription.");
    }

    /// <summary>
    /// Machine-readable slug on the 409 (plan F-3 acceptance criterion 3). Public so the API layer
    /// and the tests name the same string rather than two copies of a literal that can drift.
    /// </summary>
    public const string SetupIncompleteRuleType = "setup-incomplete";

    /// <summary>
    /// The gate itself, in one place (plan F-3 point 2): clinic name, doctor name and registration
    /// number all non-empty, and a temperature unit actually chosen (E-24).
    /// </summary>
    /// <remarks>
    /// The address and the footer are excluded on purpose - a home-visit practice may legitimately
    /// print no address, and neither field is what makes a prescription dispensable. The signature
    /// is excluded for the reason given in <see cref="ClearSignatureAsync"/>.
    /// </remarks>
    private static bool Derive(ClinicProfile profile) =>
        !string.IsNullOrWhiteSpace(profile.ClinicName)
        && !string.IsNullOrWhiteSpace(profile.DoctorName)
        && !string.IsNullOrWhiteSpace(profile.DoctorRegistrationNo)
        && profile.TemperatureUnit != TemperatureUnit.Unspecified;

    /// <summary>
    /// Entity type reported on the 404 when no profile has been saved. Shared with the controller
    /// so both 404s from this feature read identically.
    /// </summary>
    public const string EntityType = "Clinic profile";

    private async Task<ClinicProfile> RequireProfileAsync(CancellationToken cancellationToken) =>
        await _profiles.GetAsync(cancellationToken)
        // A signature upload before the profile exists is a client sequencing error, not a
        // physician error: the setup form saves the header text before it offers the upload.
        ?? throw new NotFoundException(EntityType, ClinicProfile.SingletonId.ToString());

    private static bool IsPng(byte[] content) =>
        content.Length >= PngMagic.Length && content.AsSpan(0, PngMagic.Length).SequenceEqual(PngMagic);

    private sealed record ValidatedProfile(
        string ClinicName,
        string AddressLines,
        string DoctorName,
        string DoctorRegistrationNo,
        string? PrescriptionFooter,
        TemperatureUnit TemperatureUnit);

    private static ValidatedProfile Validate(UpsertClinicProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var clinicName = Trim(request.ClinicName);
        var addressLines = Trim(request.AddressLines);
        var doctorName = Trim(request.DoctorName);
        var registrationNo = Trim(request.DoctorRegistrationNo);
        var footer = Trim(request.PrescriptionFooter);

        Require(errors, nameof(request.ClinicName), clinicName, "Enter the clinic name.", MaxNameLength);
        Require(errors, nameof(request.DoctorName), doctorName, "Enter the doctor's name.", MaxNameLength);
        Require(
            errors,
            nameof(request.DoctorRegistrationNo),
            registrationNo,
            "Enter the doctor's registration number.",
            MaxRegistrationNoLength);

        if (addressLines.Length > MaxAddressLength)
        {
            errors[nameof(request.AddressLines)] =
                [$"The address must be {MaxAddressLength} characters or fewer."];
        }

        if (footer.Length > MaxFooterLength)
        {
            errors[nameof(request.PrescriptionFooter)] =
                [$"The footer must be {MaxFooterLength} characters or fewer."];
        }

        // E-24. An absent unit is rejected rather than defaulted. Guessing Celsius here would put
        // an unanswered question into the header of every prescription the clinic ever prints.
        var unit = request.TemperatureUnit ?? TemperatureUnit.Unspecified;
        if (unit == TemperatureUnit.Unspecified || !Enum.IsDefined(unit))
        {
            errors[nameof(request.TemperatureUnit)] = ["Choose the temperature unit this clinic uses."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        return new ValidatedProfile(
            clinicName,
            addressLines,
            doctorName,
            registrationNo,
            footer.Length == 0 ? null : footer,
            unit);
    }

    private static void Require(
        IDictionary<string, string[]> errors,
        string field,
        string value,
        string missingMessage,
        int maxLength)
    {
        if (value.Length == 0)
        {
            errors[field] = [missingMessage];
        }
        else if (value.Length > maxLength)
        {
            errors[field] = [$"{field} must be {maxLength} characters or fewer."];
        }
    }

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;

    private static ClinicProfileResponse ToResponse(ClinicProfile profile) => new(
        profile.ClinicName,
        profile.AddressLines,
        profile.DoctorName,
        profile.DoctorRegistrationNo,
        profile.PrescriptionFooter,
        profile.TemperatureUnit,
        profile.SignatureImage is { Length: > 0 }
            ? $"data:image/png;base64,{Convert.ToBase64String(profile.SignatureImage)}"
            : null,
        // Derived, not read from the column - see the class remarks.
        Derive(profile),
        profile.UpdatedUtc);
}
