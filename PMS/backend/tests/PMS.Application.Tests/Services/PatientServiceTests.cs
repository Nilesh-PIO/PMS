using FluentAssertions;
using PMS.Application.Dtos.Patients;
using PMS.Application.Exceptions;
using PMS.Application.Services;
using PMS.Application.Tests.TestDoubles;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Tests.Services;

/// <summary>
/// F-5 backend unit tests (plan F-5 point 6): "normalisation, DOB-in-future rejection,
/// approx-age-without-date rejection, incomplete-profile flag".
/// </summary>
/// <remarks>
/// The four named cases are here. The suite goes further on the age rule specifically, because E-9
/// is the one failure in this feature that cannot be repaired afterwards - a bare age with no
/// recorded-on date destroys information that was never written down, so every way of reaching that
/// state is pinned rather than only the one the plan names.
/// <para>
/// <c>ClinicSettingsService</c> is used for real rather than stubbed, so "is this gender offered?"
/// is answered by F-4's actual list logic. A stub would let F-5's tests keep passing after a change
/// to F-4 that broke the integration between them.
/// </para>
/// </remarks>
public class PatientServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 10, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 7);

    private readonly FixedClock _clock = new(Now);

    private (PatientService Service, FakePatientRepository Patients) Build(
        FakePatientRepository? patients = null)
    {
        patients ??= new FakePatientRepository();

        var settings = new ClinicSettingsService(
            FakeClinicSettingsRepository.Seeded(),
            new StubClinicProfileService(setupComplete: true, temperatureUnit: TemperatureUnit.Celsius),
            _clock);

        return (new PatientService(patients, settings, _clock), patients);
    }

    private static CreatePatientRequest ARequest(Action<CreatePatientRequest>? tweak = null)
    {
        var request = new CreatePatientRequest { FullName = "Ravi Kumar" };
        tweak?.Invoke(request);
        return request;
    }

    private static async Task<IReadOnlyDictionary<string, string[]>> ErrorsFrom(Func<Task> act)
    {
        var thrown = await Assert.ThrowsAsync<ValidationFailedException>(act);
        return thrown.Errors;
    }

    // --- name (C-18, E-13, E-57, E-60) --------------------------------------

    [Fact]
    public async Task A_single_word_name_is_accepted()
    {
        // E-13. A required-surname design rejects real patients; mononyms are ordinary here.
        var (service, patients) = Build();

        var created = await service.CreateAsync(ARequest(r => r.FullName = "Meera"), default);

        created.FullName.Should().Be("Meera");
        patients.Saved.Should().ContainSingle().Which.NormalizedName.Should().Be("meera");
    }

    [Fact]
    public async Task A_blank_name_is_the_one_thing_that_is_rejected()
    {
        var (service, _) = Build();

        var errors = await ErrorsFrom(() => service.CreateAsync(ARequest(r => r.FullName = "   "), default));

        errors.Should().ContainKey(nameof(CreatePatientRequest.FullName));
    }

    [Fact]
    public async Task Whitespace_variants_of_one_name_normalize_to_the_same_key()
    {
        // Acceptance criterion 4, and the cheap half of the E-25 duplicate fix (E-60).
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(r => r.FullName = "  Ravi   Kumar  "), default);
        await service.CreateAsync(ARequest(r => r.FullName = "Ravi Kumar"), default);

        patients.Saved.Select(p => p.NormalizedName).Should().Equal("ravi kumar", "ravi kumar");
    }

    [Fact]
    public async Task The_entered_spelling_is_preserved_even_though_the_matching_key_is_folded()
    {
        // Normalisation adds a column; it never rewrites what the physician typed.
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(r => r.FullName = "  Ravi   Kumar  "), default);

        var saved = patients.Saved.Single();
        saved.FullName.Should().Be("Ravi Kumar", "surrounding and repeated whitespace is collapsed for display");
        saved.NormalizedName.Should().Be("ravi kumar");
    }

    [Theory]
    [InlineData("रवि कुमार")] // Devanagari
    [InlineData("李小龙")]                                 // Han
    [InlineData("François Müller")]                          // Latin with diacritics
    public async Task A_non_latin_name_is_stored_and_returned_unchanged(string name)
    {
        // E-57. Unicode end to end, and specifically *not* transliterated - folding these to ASCII
        // would collapse genuinely different people onto one matching key.
        var (service, patients) = Build();

        var created = await service.CreateAsync(ARequest(r => r.FullName = name), default);

        created.FullName.Should().Be(name);
        patients.Saved.Single().NormalizedName.Should().Be(name.ToLowerInvariant());
    }

    [Fact]
    public async Task A_name_split_by_a_non_breaking_space_matches_the_ordinary_one()
    {
        // The invisible near-duplicate: pasted from a document, identical to the eye.
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(r => r.FullName = "Ravi Kumar"), default);

        patients.Saved.Single().NormalizedName.Should().Be("ravi kumar");
    }

    // --- age: integrity rule 1 (C-19, E-9, E-11, E-21) ----------------------

    [Fact]
    public async Task An_approximate_age_without_a_recorded_date_is_completed_with_today_never_left_bare()
    {
        // E-9, the load-bearing one. The client normally omits the date; the server stamps it.
        // What must never happen is a stored age with no date attached to it.
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(r => r.ApproxAgeYears = 40), default);

        var saved = patients.Saved.Single();
        saved.ApproxAgeYears.Should().Be(40);
        saved.AgeRecordedOn.Should().Be(Today);
        saved.DateOfBirth.Should().BeNull();
    }

    [Fact]
    public async Task A_stored_approximate_age_never_loses_the_year_it_was_taken()
    {
        // Acceptance criterion 2's display half. "~40" alone would read years later as a fact.
        var (service, patients) = Build();
        var created = await service.CreateAsync(ARequest(r => r.ApproxAgeYears = 40), default);

        created.AgeDisplay.Should().Be("~40 (recorded 2026)");

        var detail = await service.GetAsync(patients.Saved.Single().Id, default);
        detail.AgeDisplay.Should().Be("~40 (recorded 2026)");
    }

    [Fact]
    public async Task An_approximate_age_recorded_in_2026_still_reads_as_2026_six_years_later()
    {
        // The whole point of C-19: the record does not silently age itself into a different claim.
        var (service, patients) = Build();
        await service.CreateAsync(ARequest(r => r.ApproxAgeYears = 40), default);
        var id = patients.Saved.Single().Id;

        _clock.Advance(TimeSpan.FromDays(365 * 6));

        var detail = await service.GetAsync(id, default);
        detail.AgeDisplay.Should().Be("~40 (recorded 2026)");
        detail.ApproxAgeYears.Should().Be(40);
    }

    [Fact]
    public async Task A_recorded_on_date_with_no_age_attached_to_it_is_rejected()
    {
        // The mirror image of the rule above: a date that describes nothing.
        var (service, _) = Build();

        var errors = await ErrorsFrom(() =>
            service.CreateAsync(ARequest(r => r.AgeRecordedOn = new DateOnly(2026, 1, 1)), default));

        errors.Should().ContainKey(nameof(CreatePatientRequest.ApproxAgeYears));
    }

    [Fact]
    public async Task A_date_of_birth_and_an_approximate_age_together_are_rejected()
    {
        // Two answers, no rule for which wins - so neither is stored.
        var (service, patients) = Build();

        var errors = await ErrorsFrom(() => service.CreateAsync(
            ARequest(r =>
            {
                r.DateOfBirth = new DateOnly(1985, 3, 2);
                r.ApproxAgeYears = 40;
            }),
            default));

        errors.Should().ContainKey(nameof(CreatePatientRequest.DateOfBirth));
        patients.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task A_future_date_of_birth_is_rejected()
    {
        // Acceptance criterion 3, second half.
        var (service, _) = Build();

        var errors = await ErrorsFrom(() => service.CreateAsync(
            ARequest(r => r.DateOfBirth = Today.AddDays(1)), default));

        errors.Should().ContainKey(nameof(CreatePatientRequest.DateOfBirth));
    }

    [Fact]
    public async Task A_date_of_birth_of_today_is_accepted_and_shows_an_age_in_days()
    {
        // Acceptance criterion 3, first half (E-11). A newborn seen on the day of birth.
        var (service, _) = Build();

        var created = await service.CreateAsync(ARequest(r => r.DateOfBirth = Today), default);

        created.AgeDisplay.Should().Be("0 days");
    }

    [Theory]
    [InlineData(3, "3 days")]
    [InlineData(1, "1 day")]
    [InlineData(30, "30 days")]
    [InlineData(31, "1 month")]
    [InlineData(400, "13 months")]
    public async Task An_exact_age_is_shown_in_the_unit_that_carries_information(int ageInDays, string expected)
    {
        // E-11. "0" is exactly what a paediatric dosing judgement must not be told.
        var (service, _) = Build();

        var created = await service.CreateAsync(
            ARequest(r => r.DateOfBirth = Today.AddDays(-ageInDays)), default);

        created.AgeDisplay.Should().Be(expected);
    }

    [Theory]
    [InlineData("2023-09-09", "2")] // birthday has not arrived yet this year
    [InlineData("2023-09-07", "3")] // birthday is today
    [InlineData("2023-09-06", "3")]
    [InlineData("1985-03-02", "41")]
    public async Task An_age_in_years_counts_whole_birthdays_and_not_elapsed_days(
        string dateOfBirth, string expected)
    {
        // Stated with real calendar dates rather than day offsets: 365 * 3 days is not three years
        // once a leap day is involved, and a test written that way asserts the wrong thing while
        // looking obviously right.
        var (service, _) = Build();

        var created = await service.CreateAsync(
            ARequest(r => r.DateOfBirth = DateOnly.Parse(dateOfBirth)), default);

        created.AgeDisplay.Should().Be(expected);
    }

    [Fact]
    public async Task No_age_at_all_is_allowed_and_says_so_rather_than_showing_zero()
    {
        // E-8. A sentinel number in a clinical field is the E-18 mistake in a different column.
        var (service, _) = Build();

        var created = await service.CreateAsync(ARequest(), default);

        created.AgeDisplay.Should().Be(PatientAgeFormatter.NotRecorded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(500)]
    public async Task An_impossible_approximate_age_is_rejected(int age)
    {
        var (service, _) = Build();

        var errors = await ErrorsFrom(() =>
            service.CreateAsync(ARequest(r => r.ApproxAgeYears = age), default));

        errors.Should().ContainKey(nameof(CreatePatientRequest.ApproxAgeYears));
    }

    // --- contact and the incomplete flag (Q-7, E-8, E-20, E-59) -------------

    [Fact]
    public async Task A_patient_with_a_name_and_nothing_else_is_saved_and_flagged_incomplete()
    {
        // Acceptance criterion 1, and E-8: allowed, but visibly thin rather than silently thin.
        var (service, patients) = Build();

        var created = await service.CreateAsync(ARequest(r => r.FullName = "Meera"), default);

        created.IsProfileIncomplete.Should().BeTrue();
        created.PhoneTail.Should().BeNull();

        var detail = await service.GetAsync(patients.Saved.Single().Id, default);
        detail.MissingFields.Should().BeEquivalentTo(["phone", "age", "gender"]);
        detail.PrimaryPhone.Should().BeNull();
    }

    [Fact]
    public async Task A_fully_completed_profile_is_not_flagged()
    {
        var (service, patients) = Build();

        await service.CreateAsync(
            ARequest(r =>
            {
                r.PrimaryPhone = "98765 43210";
                r.Gender = "Female";
                r.DateOfBirth = new DateOnly(1985, 3, 2);
            }),
            default);

        var detail = await service.GetAsync(patients.Saved.Single().Id, default);
        detail.IsProfileIncomplete.Should().BeFalse();
        detail.MissingFields.Should().BeEmpty();
    }

    [Fact]
    public async Task An_alternate_contact_alone_does_not_clear_the_incomplete_flag()
    {
        // Deliberate: alt contact is optional extra, so including it in the completeness set would
        // leave well-filled profiles permanently flagged and turn the warning into wallpaper.
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(r => r.AltContact = "Sister - 98765 43210"), default);

        var detail = await service.GetAsync(patients.Saved.Single().Id, default);
        detail.MissingFields.Should().NotContain("altContact");
        detail.IsProfileIncomplete.Should().BeTrue("phone, age and gender are still missing");
    }

    [Theory]
    [InlineData("+91 98765-43210", "919876543210", "3210")]
    [InlineData("098765 43210", "09876543210", "3210")]
    [InlineData("(020) 2612 3456", "02026123456", "3456")]
    [InlineData("98765 43210", "9876543210", "3210")]
    public async Task A_phone_is_stored_as_typed_and_matched_on_its_digits(
        string entered, string expectedDigits, string expectedTail)
    {
        // E-59. The formats the clinic uses are normalised, never rejected.
        //
        // Note what this deliberately does *not* claim: that "+91 98765 43210" and "098765 43210"
        // produce the same key. They do not - one keeps the country code, the other a trunk zero -
        // and E-59 asks only for a digits-only index. Deciding that those two are the same person
        // is a matching rule, which is F-6's to make and to state; encoding a guess about it here
        // would bury that decision in a normalizer.
        var (service, patients) = Build();

        var created = await service.CreateAsync(ARequest(r => r.PrimaryPhone = entered), default);

        var saved = patients.Saved.Single();
        saved.PrimaryPhone.Should().Be(entered, "the entered form is what is displayed and dialled");
        saved.NormalizedPhone.Should().Be(expectedDigits);
        created.PhoneTail.Should().Be(expectedTail);
    }

    [Fact]
    public async Task No_phone_normalizes_to_null_and_not_to_an_empty_string()
    {
        // If every phone-less patient shared one blank key they would all be offered to each other
        // as duplicates by F-6.
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(), default);

        patients.Saved.Single().NormalizedPhone.Should().BeNull();
    }

    // --- gender, via F-4's real list (C-20) ---------------------------------

    [Fact]
    public async Task A_gender_from_the_clinics_list_is_stored()
    {
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(r => r.Gender = "Female"), default);

        patients.Saved.Single().Gender.Should().Be("Female");
    }

    [Fact]
    public async Task A_gender_that_is_not_on_the_clinics_list_is_rejected()
    {
        // C-20. This is what stops "M", "Male" and "male" appearing in one column.
        var (service, patients) = Build();

        var errors = await ErrorsFrom(() =>
            service.CreateAsync(ARequest(r => r.Gender = "M"), default));

        errors.Should().ContainKey(nameof(CreatePatientRequest.Gender));
        patients.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task No_gender_at_all_is_allowed_and_is_different_from_choosing_Not_stated()
    {
        // "Nobody asked" and "the patient declined to say" are different facts.
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(r => r.Gender = null), default);
        await service.CreateAsync(ARequest(r => r.Gender = "Not stated"), default);

        patients.Saved[0].Gender.Should().BeNull();
        patients.Saved[1].Gender.Should().Be("Not stated");
    }

    // --- create idempotency: integrity rule 4 (E-43, E-46) ------------------

    [Fact]
    public async Task Replaying_a_submission_token_returns_the_same_patient_and_creates_no_second_row()
    {
        // Acceptance criterion 6. Double-clicking Save creates exactly one patient row.
        var (service, patients) = Build();
        var token = Guid.NewGuid();

        var first = await service.CreateAsync(ARequest(r => r.SubmissionId = token), default);
        var second = await service.CreateAsync(ARequest(r => r.SubmissionId = token), default);

        second.Id.Should().Be(first.Id);
        patients.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task A_lost_race_on_the_unique_index_returns_the_winner_rather_than_an_error()
    {
        // The window the read-first check cannot close: two clicks 20 ms apart both see nothing.
        // The database decides, and the loser must report the registration that did happen -
        // surfacing a 409 here would tell the physician a successful save had failed.
        var token = Guid.NewGuid();
        var patients = new FakePatientRepository();
        var (service, _) = Build(patients);

        var winner = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = "Ravi Kumar",
            NormalizedName = "ravi kumar",
            SubmissionId = token,
            RegisteredUtc = Now,
            Status = PatientStatus.Active,
        };
        patients.FailNextSaveWithSubmissionConflict = winner;

        var result = await service.CreateAsync(ARequest(r => r.SubmissionId = token), default);

        result.Id.Should().Be(winner.Id);
        patients.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task Two_different_submissions_of_the_same_details_both_create_a_patient()
    {
        // Not F-5's job to prevent: two genuinely separate registrations of a similar-looking
        // person is the duplicate-*detection* problem, and it is F-6's, deliberately as a warning
        // and never as a block. F-5 must not quietly swallow the second one.
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(r => r.SubmissionId = Guid.NewGuid()), default);
        await service.CreateAsync(ARequest(r => r.SubmissionId = Guid.NewGuid()), default);

        patients.Saved.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_request_with_no_submission_token_still_registers()
    {
        // Optional, not required: a caller that forgoes the guarantee is not rejected.
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(), default);

        patients.Saved.Should().ContainSingle().Which.SubmissionId.Should().BeNull();
    }

    // --- what the client may not set ---------------------------------------

    [Fact]
    public async Task Server_owned_fields_are_set_here_and_cannot_be_supplied_by_the_client()
    {
        // The DTO has no property for any of these, which is the real guarantee; this pins the
        // values the service writes so a later refactor cannot quietly hand one to the client.
        var (service, patients) = Build();

        await service.CreateAsync(ARequest(), default);

        var saved = patients.Saved.Single();
        saved.Id.Should().NotBe(Guid.Empty);
        saved.RegisteredUtc.Should().Be(Now);
        saved.Status.Should().Be(PatientStatus.Active);
        saved.InactiveReason.Should().BeNull();
        saved.MergedIntoPatientId.Should().BeNull("F-6 owns that pointer, not registration");
    }

    [Fact]
    public async Task All_form_errors_are_reported_at_once_rather_than_one_per_round_trip()
    {
        var (service, _) = Build();

        var errors = await ErrorsFrom(() => service.CreateAsync(
            new CreatePatientRequest
            {
                FullName = "  ",
                Gender = "M",
                DateOfBirth = Today.AddYears(1),
            },
            default));

        errors.Keys.Should().Contain([
            nameof(CreatePatientRequest.FullName),
            nameof(CreatePatientRequest.Gender),
            nameof(CreatePatientRequest.DateOfBirth),
        ]);
    }

    // --- read ---------------------------------------------------------------

    [Fact]
    public async Task An_unknown_id_is_a_not_found_rather_than_an_empty_profile()
    {
        var (service, _) = Build();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task A_created_patient_round_trips_through_the_profile_read()
    {
        var (service, patients) = Build();

        var created = await service.CreateAsync(
            ARequest(r =>
            {
                r.FullName = "Ravi Kumar";
                r.PrimaryPhone = "+91 98765-43210";
                r.Gender = "Male";
                r.DateOfBirth = new DateOnly(1985, 3, 2);
                r.AltContact = "Sister - 98765 00000";
            }),
            default);

        var detail = await service.GetAsync(patients.Saved.Single().Id, default);

        detail.Id.Should().Be(created.Id);
        detail.FullName.Should().Be("Ravi Kumar");
        detail.NormalizedName.Should().Be("ravi kumar");
        detail.PrimaryPhone.Should().Be("+91 98765-43210");
        detail.PhoneTail.Should().Be("3210");
        detail.Gender.Should().Be("Male");
        detail.DateOfBirth.Should().Be(new DateOnly(1985, 3, 2));
        detail.AgeDisplay.Should().Be("41");
        detail.AltContact.Should().Be("Sister - 98765 00000");
        detail.Status.Should().Be("Active");
        detail.RegisteredUtc.Should().Be(Now);
        detail.IsProfileIncomplete.Should().BeFalse();
    }
}
