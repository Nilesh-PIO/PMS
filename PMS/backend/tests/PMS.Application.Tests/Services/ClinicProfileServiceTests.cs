using FluentAssertions;
using PMS.Application.Dtos.Clinic;
using PMS.Application.Exceptions;
using PMS.Application.Services;
using PMS.Application.Tests.TestDoubles;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Tests.Services;

/// <summary>
/// F-3 backend unit tests (plan F-3 point 6): "IsSetupComplete transitions, oversize-signature
/// rejection".
/// </summary>
public class ClinicProfileServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FixedClock _clock = new(Now);

    private static UpsertClinicProfileRequest AValidRequest() => new()
    {
        ClinicName = "Sunrise Clinic",
        AddressLines = "12 Station Road\nPune 411001",
        DoctorName = "Dr A. Mehta",
        DoctorRegistrationNo = "MMC-99215",
        PrescriptionFooter = "Please bring this prescription to your next visit.",
        TemperatureUnit = TemperatureUnit.Celsius,
    };

    /// <summary>A real 1x1 PNG: eight-byte magic, IHDR, IDAT, IEND.</summary>
    private static byte[] APng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    private (ClinicProfileService Service, FakeClinicProfileRepository Repository) Build(
        ClinicProfile? seed = null)
    {
        var repository = new FakeClinicProfileRepository(seed);
        return (new ClinicProfileService(repository, _clock), repository);
    }

    // --- IsSetupComplete transitions (plan F-3 point 2, E-1) ----------------

    [Fact]
    public async Task An_empty_table_is_not_set_up()
    {
        var (service, _) = Build();

        (await service.IsSetupCompleteAsync(default)).Should().BeFalse();
        (await service.GetAsync(default)).Should().BeNull(
            "no profile has ever been saved, and that is the state GET reports as a 404");
    }

    [Fact]
    public async Task Saving_the_four_gate_fields_completes_setup()
    {
        var (service, repository) = Build();

        var response = await service.UpsertAsync(AValidRequest(), default);

        response.IsSetupComplete.Should().BeTrue();
        repository.Stored!.IsSetupComplete.Should().BeTrue("the derived answer is persisted too");
        repository.AddCount.Should().Be(1);
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task A_second_save_updates_the_singleton_rather_than_inserting_a_second_row()
    {
        var (service, repository) = Build();

        await service.UpsertAsync(AValidRequest(), default);
        var second = AValidRequest();
        second.ClinicName = "Sunrise Family Clinic";
        await service.UpsertAsync(second, default);

        repository.AddCount.Should().Be(1, "the clinic has exactly one identity");
        repository.Stored!.ClinicName.Should().Be("Sunrise Family Clinic");
        repository.Stored.Id.Should().Be(ClinicProfile.SingletonId);
    }

    [Theory]
    [InlineData("", "Dr A. Mehta", "MMC-99215", TemperatureUnit.Celsius)]
    [InlineData("Sunrise Clinic", "", "MMC-99215", TemperatureUnit.Celsius)]
    [InlineData("Sunrise Clinic", "Dr A. Mehta", "", TemperatureUnit.Celsius)]
    [InlineData("Sunrise Clinic", "Dr A. Mehta", "MMC-99215", TemperatureUnit.Unspecified)]
    public async Task Setup_is_incomplete_whenever_any_gate_field_is_missing_in_the_stored_row(
        string clinicName,
        string doctorName,
        string registrationNo,
        TemperatureUnit unit)
    {
        // Seeded directly, bypassing UpsertAsync on purpose. This is the SSMS case: someone edits
        // the table by hand and leaves IsSetupComplete = 1 behind. The service must derive the
        // answer from the values rather than trusting the flag, or the E-1 print gate is disarmed
        // by an UPDATE nobody reviewed.
        var seed = FakeClinicProfileRepository.ACompleteProfile();
        seed.ClinicName = clinicName;
        seed.DoctorName = doctorName;
        seed.DoctorRegistrationNo = registrationNo;
        seed.TemperatureUnit = unit;
        seed.IsSetupComplete = true;

        var (service, _) = Build(seed);

        (await service.IsSetupCompleteAsync(default)).Should().BeFalse();
        (await service.GetAsync(default))!.IsSetupComplete.Should().BeFalse(
            "the response reports the derived answer, not the stale column");
    }

    [Fact]
    public async Task Whitespace_is_not_a_clinic_name()
    {
        var seed = FakeClinicProfileRepository.ACompleteProfile();
        seed.ClinicName = "   ";
        var (service, _) = Build(seed);

        (await service.IsSetupCompleteAsync(default)).Should().BeFalse();
    }

    [Fact]
    public async Task Neither_the_address_nor_the_footer_nor_the_signature_gates_setup()
    {
        // A home-visit practice may print no address, and a physician who signs by hand needs no
        // uploaded image. Neither is what makes a prescription dispensable, so neither blocks.
        var request = AValidRequest();
        request.AddressLines = null;
        request.PrescriptionFooter = null;

        var (service, _) = Build();

        var response = await service.UpsertAsync(request, default);

        response.IsSetupComplete.Should().BeTrue();
        response.SignatureImageDataUrl.Should().BeNull();
    }

    // --- the print gate (acceptance criterion 3) ----------------------------

    [Fact]
    public async Task Ensuring_setup_throws_a_409_rule_when_the_clinic_is_unconfigured()
    {
        var (service, _) = Build();

        var act = () => service.EnsureSetupCompleteAsync(default);

        var thrown = await act.Should().ThrowAsync<DomainRuleException>();
        thrown.Which.RuleType.Should().Be("setup-incomplete");
        thrown.Which.Message.Should().Contain("Clinic setup is incomplete");
    }

    [Fact]
    public async Task Ensuring_setup_passes_once_the_clinic_is_configured()
    {
        var (service, _) = Build(FakeClinicProfileRepository.ACompleteProfile());

        var act = () => service.EnsureSetupCompleteAsync(default);

        await act.Should().NotThrowAsync();
    }

    // --- validation (400) ---------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_clinic_name_is_rejected(string? clinicName)
    {
        var request = AValidRequest();
        request.ClinicName = clinicName;
        var (service, repository) = Build();

        var act = () => service.UpsertAsync(request, default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey(nameof(UpsertClinicProfileRequest.ClinicName));
        repository.SaveCount.Should().Be(0, "a rejected save must not half-write the row");
    }

    [Fact]
    public async Task Every_missing_gate_field_is_reported_at_once_rather_than_one_per_round_trip()
    {
        var (service, _) = Build();

        var act = () => service.UpsertAsync(new UpsertClinicProfileRequest(), default);

        var errors = (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors;
        errors.Keys.Should().BeEquivalentTo(
            nameof(UpsertClinicProfileRequest.ClinicName),
            nameof(UpsertClinicProfileRequest.DoctorName),
            nameof(UpsertClinicProfileRequest.DoctorRegistrationNo),
            nameof(UpsertClinicProfileRequest.TemperatureUnit));
    }

    [Fact]
    public async Task An_unchosen_temperature_unit_is_rejected_rather_than_defaulted()
    {
        // E-24. "37" and "98.6" are the same fever in different units. Guessing Celsius here would
        // put an unanswered question into the header of every prescription the clinic prints.
        var request = AValidRequest();
        request.TemperatureUnit = null;
        var (service, _) = Build();

        var act = () => service.UpsertAsync(request, default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey(nameof(UpsertClinicProfileRequest.TemperatureUnit));
    }

    [Fact]
    public async Task An_undefined_temperature_unit_value_is_rejected()
    {
        var request = AValidRequest();
        request.TemperatureUnit = (TemperatureUnit)97;
        var (service, _) = Build();

        var act = () => service.UpsertAsync(request, default);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task An_over_length_footer_is_rejected()
    {
        var request = AValidRequest();
        request.PrescriptionFooter = new string('x', ClinicProfileService.MaxFooterLength + 1);
        var (service, _) = Build();

        var act = () => service.UpsertAsync(request, default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey(nameof(UpsertClinicProfileRequest.PrescriptionFooter));
    }

    [Fact]
    public async Task A_footer_of_exactly_the_limit_is_accepted()
    {
        var request = AValidRequest();
        request.PrescriptionFooter = new string('x', ClinicProfileService.MaxFooterLength);
        var (service, _) = Build();

        var response = await service.UpsertAsync(request, default);

        response.PrescriptionFooter.Should().HaveLength(ClinicProfileService.MaxFooterLength);
    }

    [Fact]
    public async Task Values_are_trimmed_and_an_empty_footer_is_stored_as_null()
    {
        var request = AValidRequest();
        request.ClinicName = "  Sunrise Clinic  ";
        request.PrescriptionFooter = "   ";
        var (service, repository) = Build();

        await service.UpsertAsync(request, default);

        repository.Stored!.ClinicName.Should().Be("Sunrise Clinic");
        repository.Stored.PrescriptionFooter.Should().BeNull(
            "an empty footer is 'no footer', not a line of spaces on the printed page");
    }

    [Fact]
    public async Task Saving_stamps_the_update_time_from_the_clock()
    {
        var (service, repository) = Build();

        await service.UpsertAsync(AValidRequest(), default);

        repository.Stored!.UpdatedUtc.Should().Be(Now);
    }

    // --- signature upload (plan F-3 point 6: oversize rejection) ------------

    [Fact]
    public async Task An_oversize_signature_is_rejected_as_413_not_truncated()
    {
        var (service, repository) = Build(FakeClinicProfileRepository.ACompleteProfile());
        var oversize = new byte[ClinicProfileService.MaxSignatureBytes + 1];
        APng().CopyTo(oversize, 0);

        var act = () => service.SetSignatureAsync(oversize, default);

        var thrown = await act.Should().ThrowAsync<PayloadTooLargeException>();
        thrown.Which.LimitBytes.Should().Be(ClinicProfileService.MaxSignatureBytes);
        thrown.Which.Message.Should().Contain("200 KB");
        repository.Stored!.SignatureImage.Should().BeNull(
            "a rejected upload must leave the previous state untouched, not a truncated image");
    }

    [Fact]
    public async Task A_signature_of_exactly_the_cap_is_accepted()
    {
        var (service, repository) = Build(FakeClinicProfileRepository.ACompleteProfile());
        var atLimit = new byte[ClinicProfileService.MaxSignatureBytes];
        APng().CopyTo(atLimit, 0);

        await service.SetSignatureAsync(atLimit, default);

        repository.Stored!.SignatureImage.Should().HaveCount(ClinicProfileService.MaxSignatureBytes);
    }

    [Fact]
    public async Task A_non_png_upload_is_rejected_by_its_bytes_not_its_file_name()
    {
        // A renamed .jpg passes any content-type check and then fails inside the PDF renderer at
        // print time - with a patient waiting. Checking the magic bytes moves that failure to the
        // settings screen, where it costs nothing.
        var (service, _) = Build(FakeClinicProfileRepository.ACompleteProfile());
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

        var act = () => service.SetSignatureAsync(jpeg, default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("file");
    }

    [Fact]
    public async Task An_empty_upload_is_rejected()
    {
        var (service, _) = Build(FakeClinicProfileRepository.ACompleteProfile());

        var act = () => service.SetSignatureAsync([], default);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task Uploading_a_signature_before_any_profile_exists_is_a_404()
    {
        var (service, _) = Build();

        var act = () => service.SetSignatureAsync(APng(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task A_stored_signature_comes_back_as_a_png_data_url()
    {
        var (service, _) = Build(FakeClinicProfileRepository.ACompleteProfile());

        var response = await service.SetSignatureAsync(APng(), default);

        response.SignatureImageDataUrl.Should().StartWith("data:image/png;base64,");
        response.SignatureImageDataUrl.Should().Contain(Convert.ToBase64String(APng()));
    }

    [Fact]
    public async Task Re_saving_the_header_text_does_not_drop_an_uploaded_signature()
    {
        // The signature has its own routes, so a PUT of the header must not quietly discard an
        // image the physician uploaded weeks earlier.
        var (service, repository) = Build(FakeClinicProfileRepository.ACompleteProfile());
        await service.SetSignatureAsync(APng(), default);

        var response = await service.UpsertAsync(AValidRequest(), default);

        response.SignatureImageDataUrl.Should().NotBeNull();
        repository.Stored!.SignatureImage.Should().NotBeNull();
    }

    // --- signature removal --------------------------------------------------

    [Fact]
    public async Task Clearing_the_signature_leaves_setup_complete()
    {
        // Plan F-3 point 1: with no signature the printed footer shows a ruled signature area.
        // That is a supported way to work, so removing the image must not un-arm the clinic.
        var (service, repository) = Build(FakeClinicProfileRepository.ACompleteProfile());
        await service.SetSignatureAsync(APng(), default);

        var response = await service.ClearSignatureAsync(default);

        response.SignatureImageDataUrl.Should().BeNull();
        response.IsSetupComplete.Should().BeTrue();
        repository.Stored!.SignatureImage.Should().BeNull();
    }

    [Fact]
    public async Task Clearing_a_signature_before_any_profile_exists_is_a_404()
    {
        var (service, _) = Build();

        var act = () => service.ClearSignatureAsync(default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
