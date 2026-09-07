using FluentAssertions;
using PMS.Application.Dtos.Clinic;
using PMS.Application.Exceptions;
using PMS.Application.Services;
using PMS.Application.Tests.TestDoubles;
using PMS.Domain.Enums;

namespace PMS.Application.Tests.Services;

/// <summary>
/// F-4 backend unit tests (plan F-4 point 6): "cannot delete <c>Not stated</c>; blank threshold
/// means no warning emitted".
/// </summary>
/// <remarks>
/// The two named cases are the load-bearing ones and they are here, but the suite deliberately
/// goes further on the same two rules - the duplicate that C-20 exists to prevent, and the
/// difference between a blank threshold and a zero one - because both are the kind of defect that
/// is invisible until a year of real data has gone through them.
/// </remarks>
public class ClinicSettingsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly FixedClock _clock = new(Now);

    private (ClinicSettingsService Service, FakeClinicSettingsRepository Repository) Build(
        FakeClinicSettingsRepository? repository = null,
        TemperatureUnit? clinicUnit = TemperatureUnit.Celsius)
    {
        repository ??= FakeClinicSettingsRepository.Seeded();
        var service = new ClinicSettingsService(
            repository,
            new StubClinicProfileService(setupComplete: true, temperatureUnit: clinicUnit),
            _clock);

        return (service, repository);
    }

    private static SettingOptionListRequest AList(params (string Value, bool IsActive)[] items) =>
        new()
        {
            Items = items
                .Select(i => new SettingOptionItemRequest { Value = i.Value, IsActive = i.IsActive })
                .ToList(),
        };

    private static SettingOptionListRequest TheSeededGenderList() =>
        AList(("Female", true), ("Male", true), ("Other", true), ("Not stated", true));

    // --- the seeded lists (plan F-4 point 1: Q-9 and Q-2 assumptions) -------

    [Fact]
    public async Task The_gender_list_is_seeded_with_the_four_assumed_values_in_order()
    {
        var (service, _) = Build();

        var options = await service.GetOptionsAsync("Gender", includeInactive: false, default);

        options.Select(o => o.Value).Should().Equal("Female", "Male", "Other", "Not stated");
        options.Select(o => o.DisplayOrder).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task The_vitals_reason_list_is_seeded_with_the_four_assumed_values()
    {
        var (service, _) = Build();

        var options = await service.GetOptionsAsync(
            "VitalsNotRecordedReason",
            includeInactive: false,
            default);

        options.Select(o => o.Value).Should().Equal(
            "Equipment unavailable",
            "Patient declined",
            "Not clinically indicated",
            "Other");
    }

    [Fact]
    public async Task Only_Not_stated_is_marked_protected()
    {
        var (service, _) = Build();

        var genders = await service.GetOptionsAsync("Gender", false, default);
        var reasons = await service.GetOptionsAsync("VitalsNotRecordedReason", false, default);

        genders.Where(o => o.IsProtected).Select(o => o.Value).Should().Equal("Not stated");
        reasons.Should().OnlyContain(o => !o.IsProtected,
            "F-11's escape hatch needs the list to be non-empty, but no single reason is sacred");
    }

    // --- "cannot delete Not stated" (plan F-4 point 6, E-23) ----------------

    [Fact]
    public async Task Omitting_Not_stated_from_the_list_is_rejected()
    {
        var (service, repository) = Build();

        var act = () => service.SaveOptionsAsync(
            "Gender",
            AList(("Female", true), ("Male", true), ("Other", true)),
            default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("Items");

        repository.SaveCount.Should().Be(0, "a rejected edit must not touch the table at all");
        repository.StoredOptions.Should().Contain(o => o.Value == "Not stated" && o.IsActive);
    }

    [Fact]
    public async Task Deactivating_Not_stated_is_rejected_too()
    {
        // Retiring it is the subtler version of deleting it, and it has the same effect on the
        // person filling in the form: no honest answer for "we did not ask".
        var (service, repository) = Build();

        var act = () => service.SaveOptionsAsync(
            "Gender",
            AList(("Female", true), ("Male", true), ("Other", true), ("Not stated", false)),
            default);

        await act.Should().ThrowAsync<ValidationFailedException>();
        repository.StoredOptions.Single(o => o.Value == "Not stated").IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Not_stated_may_be_reordered_even_though_it_may_not_be_removed()
    {
        // Protected is not frozen: the physician can still decide where it sits in the dropdown.
        var (service, _) = Build();

        var saved = await service.SaveOptionsAsync(
            "Gender",
            AList(("Not stated", true), ("Female", true), ("Male", true), ("Other", true)),
            default);

        saved.Select(o => o.Value).Should().Equal("Not stated", "Female", "Male", "Other");
    }

    [Fact]
    public async Task A_list_with_no_active_options_is_rejected()
    {
        // F-11's mandatory-or-reason escape hatch (REC-3, E-18) reads this list. Emptied, the
        // escape hatch becomes the dead end it exists to remove - and a dead end is what makes
        // someone write a BP down from memory.
        var (service, _) = Build();

        var act = () => service.SaveOptionsAsync(
            "VitalsNotRecordedReason",
            AList(("Equipment unavailable", false), ("Patient declined", false)),
            default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("Items");
    }

    // --- retire, never delete (plan F-4 point 5) ---------------------------

    [Fact]
    public async Task An_omitted_option_is_retired_rather_than_deleted()
    {
        var (service, repository) = Build();

        var saved = await service.SaveOptionsAsync(
            "Gender",
            AList(("Female", true), ("Male", true), ("Not stated", true)),
            default);

        // The row survives. A patient registered as "Other" in 2026 still displays "Other".
        // Filtered by category, because "Other" is legitimately a value in both seeded lists - and
        // retiring it as a gender must leave the vitals reason completely untouched.
        var other = repository.StoredOptions
            .Single(o => o.Category == SettingCategory.Gender && o.Value == "Other");
        other.IsActive.Should().BeFalse();
        repository.StoredOptions
            .Single(o => o.Category == SettingCategory.VitalsNotRecordedReason && o.Value == "Other")
            .IsActive.Should().BeTrue("editing one list must not reach into another");
        repository.StoredOptions.Should().HaveCount(8, "nothing is ever removed from this table");

        saved.Should().Contain(o => o.Value == "Other" && !o.IsActive);
        saved.Last().Value.Should().Be("Other", "retired options sit after the live ones");
    }

    [Fact]
    public async Task A_retired_option_is_hidden_from_dropdowns_but_visible_to_the_editor()
    {
        var (service, _) = Build();
        await service.SaveOptionsAsync(
            "Gender",
            AList(("Female", true), ("Male", true), ("Not stated", true)),
            default);

        var forADropdown = await service.GetOptionsAsync("Gender", includeInactive: false, default);
        var forTheEditor = await service.GetOptionsAsync("Gender", includeInactive: true, default);

        forADropdown.Select(o => o.Value).Should().NotContain("Other");
        forTheEditor.Select(o => o.Value).Should().Contain("Other",
            "the editor cannot bring back an option it was never shown");
    }

    [Fact]
    public async Task A_retired_option_can_be_brought_back()
    {
        var (service, repository) = Build();
        await service.SaveOptionsAsync(
            "Gender",
            AList(("Female", true), ("Male", true), ("Not stated", true)),
            default);

        await service.SaveOptionsAsync("Gender", TheSeededGenderList(), default);

        repository.StoredOptions
            .Single(o => o.Category == SettingCategory.Gender && o.Value == "Other")
            .IsActive.Should().BeTrue();
        repository.StoredOptions.Count(o => o.Value == "Other" && o.Category == SettingCategory.Gender)
            .Should().Be(1, "re-activating must not insert a second row with the same value");
    }

    [Fact]
    public async Task The_whole_edit_is_saved_once()
    {
        // Reordering plus retiring plus inserting is one intent. Two SaveChanges calls would mean
        // a window in which two options claim the same slot.
        var (service, repository) = Build();

        await service.SaveOptionsAsync(
            "Gender",
            AList(("Not stated", true), ("Male", true), ("Non-binary", true)),
            default);

        repository.SaveCount.Should().Be(1);
    }

    // --- one spelling per meaning (C-20) -----------------------------------

    [Fact]
    public async Task A_case_insensitive_duplicate_in_the_submission_is_rejected()
    {
        var (service, repository) = Build();

        var act = () => service.SaveOptionsAsync(
            "Gender",
            AList(("Male", true), ("male", true), ("Not stated", true)),
            default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("Items[1].Value");

        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Re_submitting_an_existing_value_in_a_different_case_updates_the_stored_row()
    {
        var (service, repository) = Build();

        await service.SaveOptionsAsync(
            "Gender",
            AList(("female", true), ("male", true), ("Other", true), ("Not stated", true)),
            default);

        repository.StoredOptions
            .Count(o => o.Category == SettingCategory.Gender)
            .Should().Be(4, "matching is case-insensitive, so no second spelling is created");

        repository.StoredOptions.Should().Contain(o => o.Value == "Female",
            "the stored spelling is left alone - it is already sitting on historical patient rows");
    }

    [Fact]
    public async Task A_new_option_is_appended_with_the_submitted_order()
    {
        var (service, repository) = Build();

        var saved = await service.SaveOptionsAsync(
            "Gender",
            AList(("Female", true), ("Male", true), ("Non-binary", true), ("Other", true),
                ("Not stated", true)),
            default);

        saved.Select(o => o.Value).Should().Equal(
            "Female", "Male", "Non-binary", "Other", "Not stated");
        repository.StoredOptions.Should().Contain(o => o.Value == "Non-binary" && o.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_option_value_is_rejected(string value)
    {
        var (service, _) = Build();

        var act = () => service.SaveOptionsAsync(
            "Gender",
            AList((value, true), ("Not stated", true)),
            default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("Items[0].Value");
    }

    [Fact]
    public async Task An_option_value_is_trimmed_before_it_is_stored()
    {
        var (service, repository) = Build();

        await service.SaveOptionsAsync(
            "Gender",
            AList(("  Non-binary  ", true), ("Not stated", true)),
            default);

        repository.StoredOptions.Should().Contain(o => o.Value == "Non-binary");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Genders")]
    [InlineData("Unspecified")]
    public async Task An_unknown_category_is_a_validation_failure_not_an_empty_list(string? category)
    {
        // An empty 200 would render as "you have configured nothing", and the physician would
        // start typing a list that already exists somewhere else.
        var (service, _) = Build();

        var act = () => service.GetOptionsAsync(category, false, default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("category");
    }

    [Fact]
    public async Task The_category_name_is_matched_case_insensitively()
    {
        var (service, _) = Build();

        var options = await service.GetOptionsAsync("gender", false, default);

        options.Should().NotBeEmpty();
        options.Should().OnlyContain(o => o.Category == "Gender", "the answer uses the canonical name");
    }

    // --- the F-5 seam (C-20) -----------------------------------------------

    [Fact]
    public async Task An_offered_value_is_recognised_and_a_free_text_one_is_not()
    {
        var (service, _) = Build();

        (await service.IsOfferedOptionAsync(SettingCategory.Gender, "Male", default))
            .Should().BeTrue();
        (await service.IsOfferedOptionAsync(SettingCategory.Gender, "male", default))
            .Should().BeTrue("the same meaning in a different case is the same option");
        (await service.IsOfferedOptionAsync(SettingCategory.Gender, "M", default))
            .Should().BeFalse("this is exactly the abbreviation C-20 is about");
    }

    [Fact]
    public async Task A_retired_value_is_no_longer_offered()
    {
        var (service, _) = Build();
        await service.SaveOptionsAsync(
            "Gender",
            AList(("Female", true), ("Male", true), ("Other", false), ("Not stated", true)),
            default);

        (await service.IsOfferedOptionAsync(SettingCategory.Gender, "Other", default))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task An_unanswered_value_is_not_rejected_by_the_list(string? value)
    {
        // This list governs which answers are spellable, not whether the question is compulsory.
        var (service, _) = Build();

        (await service.IsOfferedOptionAsync(SettingCategory.Gender, value, default))
            .Should().BeTrue();
    }

    // --- vital ranges: blank means silence (plan F-4 point 6, E-12) ---------

    [Fact]
    public async Task Every_metric_is_returned_even_though_none_is_configured()
    {
        var (service, _) = Build();

        var ranges = await service.GetVitalRangesAsync(default);

        ranges.Select(r => r.Metric).Should().Equal(
            "Temperature", "BloodPressureSystolic", "BloodPressureDiastolic", "PulseBpm");
        ranges.Should().OnlyContain(r => r.WarnLow == null && r.WarnHigh == null);
        ranges.Should().OnlyContain(r => r.UpdatedUtc == null, "none has ever been configured");
    }

    [Fact]
    public async Task With_nothing_configured_no_value_produces_a_warning()
    {
        // F-4 acceptance criterion 3, and the default state of every clinic on day one.
        var (service, _) = Build();

        var warnings = await service.EvaluateVitalsAsync(
            new Dictionary<VitalMetric, decimal?>
            {
                [VitalMetric.Temperature] = 45m,
                [VitalMetric.PulseBpm] = 300m,
                [VitalMetric.BloodPressureSystolic] = 400m,
                [VitalMetric.BloodPressureDiastolic] = 0m,
            },
            default);

        warnings.Should().BeEmpty(
            "the brainstorm's own implausible reading (E-12) must pass silently until the "
            + "physician has said what implausible means here");
    }

    [Fact]
    public async Task A_blank_bound_stays_silent_while_the_other_bound_fires()
    {
        var repository = FakeClinicSettingsRepository.Seeded()
            .SeedRange(VitalMetric.Temperature, warnLow: null, warnHigh: 42m);
        var (service, _) = Build(repository);

        var low = await service.EvaluateVitalsAsync(
            new Dictionary<VitalMetric, decimal?> { [VitalMetric.Temperature] = 12m }, default);
        var high = await service.EvaluateVitalsAsync(
            new Dictionary<VitalMetric, decimal?> { [VitalMetric.Temperature] = 42.5m }, default);

        low.Should().BeEmpty("no lower bound was set, so there is nothing to warn about");
        high.Should().ContainSingle().Which.Message.Should().Contain("42");
    }

    [Fact]
    public async Task A_zero_threshold_is_a_real_threshold_and_is_not_treated_as_blank()
    {
        // The distinction the whole feature rests on. If `0` were read as "unset", a physician who
        // deliberately set a floor of zero would silently get no warnings at all.
        var repository = FakeClinicSettingsRepository.Seeded()
            .SeedRange(VitalMetric.BloodPressureDiastolic, warnLow: 0m, warnHigh: null);
        var (service, _) = Build(repository);

        var warnings = await service.EvaluateVitalsAsync(
            new Dictionary<VitalMetric, decimal?> { [VitalMetric.BloodPressureDiastolic] = -1m },
            default);

        warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task A_value_exactly_on_the_bound_does_not_warn()
    {
        var repository = FakeClinicSettingsRepository.Seeded()
            .SeedRange(VitalMetric.PulseBpm, warnLow: 40m, warnHigh: 120m);
        var (service, _) = Build(repository);

        var warnings = await service.EvaluateVitalsAsync(
            new Dictionary<VitalMetric, decimal?> { [VitalMetric.PulseBpm] = 120m }, default);

        warnings.Should().BeEmpty("\"warn above 120\" means 120 is acceptable");
    }

    [Fact]
    public async Task A_vital_that_was_not_recorded_produces_no_warning()
    {
        // F-11 stores absence as null plus a reason, never a sentinel (E-18). Warning about a
        // value that does not exist would be nonsense, and worse, would push someone to type one.
        var repository = FakeClinicSettingsRepository.Seeded()
            .SeedRange(VitalMetric.PulseBpm, warnLow: 40m, warnHigh: 120m);
        var (service, _) = Build(repository);

        var warnings = await service.EvaluateVitalsAsync(
            new Dictionary<VitalMetric, decimal?> { [VitalMetric.PulseBpm] = null }, default);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task A_warning_names_the_bound_the_physician_set_and_offers_to_save_anyway()
    {
        var repository = FakeClinicSettingsRepository.Seeded()
            .SeedRange(VitalMetric.Temperature, warnLow: null, warnHigh: 42m);
        var (service, _) = Build(repository);

        var warning = (await service.EvaluateVitalsAsync(
            new Dictionary<VitalMetric, decimal?> { [VitalMetric.Temperature] = 45m },
            default)).Single();

        warning.Metric.Should().Be("Temperature");
        warning.Value.Should().Be(45m);
        warning.WarnHigh.Should().Be(42m);
        warning.Message.Should().Contain("°C", "every displayed temperature carries its unit (E-24)");
        warning.Message.Should().Contain("you set", "this is the physician's threshold, not ours");
        warning.Message.Should().Contain("confirm to save it as entered",
            "the warning is soft - it never blocks (E-12)");
    }

    [Fact]
    public async Task Evaluating_never_throws_however_far_outside_the_value_is()
    {
        // The single most important property of this path: it is advice. A refusal is how a real
        // reading gets rounded to something the software will accept.
        var repository = FakeClinicSettingsRepository.Seeded()
            .SeedRange(VitalMetric.Temperature, warnLow: 35m, warnHigh: 42m);
        var (service, _) = Build(repository);

        var act = () => service.EvaluateVitalsAsync(
            new Dictionary<VitalMetric, decimal?> { [VitalMetric.Temperature] = 999m }, default);

        await act.Should().NotThrowAsync();
        (await act()).Should().ContainSingle();
    }

    [Fact]
    public async Task Warnings_come_back_in_display_order_not_payload_order()
    {
        var repository = FakeClinicSettingsRepository.Seeded()
            .SeedRange(VitalMetric.Temperature, null, 42m)
            .SeedRange(VitalMetric.PulseBpm, null, 120m);
        var (service, _) = Build(repository);

        var warnings = await service.EvaluateVitalsAsync(
            new Dictionary<VitalMetric, decimal?>
            {
                [VitalMetric.PulseBpm] = 300m,
                [VitalMetric.Temperature] = 45m,
            },
            default);

        warnings.Select(w => w.Metric).Should().Equal("Temperature", "PulseBpm");
    }

    [Fact]
    public async Task A_temperature_threshold_carries_the_clinic_unit_and_says_so_when_it_is_unset()
    {
        var (celsius, _) = Build(clinicUnit: TemperatureUnit.Celsius);
        var (fahrenheit, _) = Build(clinicUnit: TemperatureUnit.Fahrenheit);
        var (noProfile, _) = Build(clinicUnit: null);

        (await celsius.GetVitalRangesAsync(default))[0].Unit.Should().Be("°C");
        (await fahrenheit.GetVitalRangesAsync(default))[0].Unit.Should().Be("°F");
        (await noProfile.GetVitalRangesAsync(default))[0].Unit.Should().BeNull(
            "an unanswered unit is reported as unanswered, never guessed (E-24)");

        (await celsius.GetVitalRangesAsync(default))
            .Single(r => r.Metric == "PulseBpm").Unit.Should().Be("bpm");
        (await celsius.GetVitalRangesAsync(default))
            .Single(r => r.Metric == "BloodPressureSystolic").Unit.Should().Be("mmHg");
    }

    // --- saving vital ranges ------------------------------------------------

    [Fact]
    public async Task A_saved_threshold_round_trips_and_stamps_the_clock()
    {
        var (service, repository) = Build();

        var saved = await service.SaveVitalRangesAsync(
            new VitalRangeListRequest
            {
                Items =
                [
                    new VitalRangeItemRequest { Metric = "Temperature", WarnLow = 35m, WarnHigh = 42m },
                ],
            },
            default);

        saved.Single(r => r.Metric == "Temperature").WarnHigh.Should().Be(42m);
        repository.StoredRanges.Single().UpdatedUtc.Should().Be(Now);
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Saving_null_bounds_clears_a_threshold_rather_than_zeroing_it()
    {
        var repository = FakeClinicSettingsRepository.Seeded()
            .SeedRange(VitalMetric.PulseBpm, 40m, 120m);
        var (service, _) = Build(repository);

        await service.SaveVitalRangesAsync(
            new VitalRangeListRequest
            {
                Items = [new VitalRangeItemRequest { Metric = "PulseBpm", WarnLow = null, WarnHigh = null }],
            },
            default);

        var stored = repository.StoredRanges.Single();
        stored.WarnLow.Should().BeNull();
        stored.WarnHigh.Should().BeNull();

        var warnings = await service.EvaluateVitalsAsync(
            new Dictionary<VitalMetric, decimal?> { [VitalMetric.PulseBpm] = 300m }, default);
        warnings.Should().BeEmpty("cleared means silent again");
    }

    [Fact]
    public async Task A_metric_that_is_not_submitted_keeps_what_it_had()
    {
        var repository = FakeClinicSettingsRepository.Seeded()
            .SeedRange(VitalMetric.PulseBpm, 40m, 120m);
        var (service, _) = Build(repository);

        await service.SaveVitalRangesAsync(
            new VitalRangeListRequest
            {
                Items = [new VitalRangeItemRequest { Metric = "Temperature", WarnHigh = 42m }],
            },
            default);

        repository.StoredRanges.Single(r => r.Metric == VitalMetric.PulseBpm)
            .WarnHigh.Should().Be(120m);
    }

    [Fact]
    public async Task Saving_the_same_metric_twice_updates_rather_than_inserting()
    {
        var (service, repository) = Build();

        await service.SaveVitalRangesAsync(
            new VitalRangeListRequest
            {
                Items = [new VitalRangeItemRequest { Metric = "Temperature", WarnHigh = 42m }],
            },
            default);
        await service.SaveVitalRangesAsync(
            new VitalRangeListRequest
            {
                Items = [new VitalRangeItemRequest { Metric = "Temperature", WarnHigh = 41m }],
            },
            default);

        repository.StoredRanges.Should().ContainSingle().Which.WarnHigh.Should().Be(41m);
    }

    [Fact]
    public async Task An_inverted_range_is_rejected()
    {
        var (service, repository) = Build();

        var act = () => service.SaveVitalRangesAsync(
            new VitalRangeListRequest
            {
                Items = [new VitalRangeItemRequest { Metric = "PulseBpm", WarnLow = 120m, WarnHigh = 40m }],
            },
            default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("Items[0].WarnLow");
        repository.SaveCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Weight")]
    [InlineData("Unspecified")]
    public async Task An_unknown_metric_is_rejected_rather_than_silently_ignored(string? metric)
    {
        // A threshold the physician believes they saved and which was dropped on the floor is the
        // E-47 failure ("it looked like it saved") wearing a different hat.
        var (service, _) = Build();

        var act = () => service.SaveVitalRangesAsync(
            new VitalRangeListRequest { Items = [new VitalRangeItemRequest { Metric = metric }] },
            default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("Items[0].Metric");
    }

    [Fact]
    public async Task The_same_metric_twice_in_one_submission_is_rejected()
    {
        var (service, _) = Build();

        var act = () => service.SaveVitalRangesAsync(
            new VitalRangeListRequest
            {
                Items =
                [
                    new VitalRangeItemRequest { Metric = "PulseBpm", WarnHigh = 120m },
                    new VitalRangeItemRequest { Metric = "PulseBpm", WarnHigh = 100m },
                ],
            },
            default);

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("Items[1].Metric");
    }

    [Fact]
    public async Task A_threshold_that_would_not_survive_the_column_is_rejected()
    {
        // Storage shape, not medicine: decimal(6,2). Rejected here rather than rounded silently by
        // SQL Server, which would store a threshold the physician did not enter.
        var (service, _) = Build();

        var tooBig = () => service.SaveVitalRangesAsync(
            new VitalRangeListRequest
            {
                Items = [new VitalRangeItemRequest { Metric = "PulseBpm", WarnHigh = 10000m }],
            },
            default);
        var tooPrecise = () => service.SaveVitalRangesAsync(
            new VitalRangeListRequest
            {
                Items = [new VitalRangeItemRequest { Metric = "Temperature", WarnHigh = 37.555m }],
            },
            default);

        (await tooBig.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("Items[0].WarnHigh");
        (await tooPrecise.Should().ThrowAsync<ValidationFailedException>())
            .Which.Errors.Should().ContainKey("Items[0].WarnHigh");
    }

    [Fact]
    public async Task One_decimal_place_is_accepted_because_temperatures_have_one()
    {
        var (service, _) = Build();

        var saved = await service.SaveVitalRangesAsync(
            new VitalRangeListRequest
            {
                Items = [new VitalRangeItemRequest { Metric = "Temperature", WarnHigh = 37.5m }],
            },
            default);

        saved.Single(r => r.Metric == "Temperature").WarnHigh.Should().Be(37.5m);
    }

    // --- no clinical range anywhere in the source (F-4 acceptance criterion 5)

    [Fact]
    public void No_clinical_range_is_compiled_into_the_settings_code()
    {
        // Acceptance criterion 5 asserted against the code itself, not against behaviour: the only
        // numeric constants this feature carries are storage and list limits, and they are named
        // as such. If someone ever adds `const decimal NormalTemperatureHigh = 37.5m`, this fails.
        //
        // `IsLiteral || IsInitOnly` rather than `IsLiteral` alone, and that is not a detail: a
        // `const decimal` is not a literal in IL - the compiler emits it as `static readonly` with
        // a DecimalConstantAttribute. Filtering on IsLiteral only would have silently skipped every
        // decimal constant in the class, which is precisely the type a clinical threshold would be
        // written as. (Found by this test failing against its own first draft.)
        var constants = typeof(ClinicSettingsService)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral || f.IsInitOnly)
            .Select(f => f.Name)
            .ToList();

        constants.Should().BeEquivalentTo(
            [
                nameof(ClinicSettingsService.MaxOptionValueLength),
                nameof(ClinicSettingsService.MaxOptionsPerCategory),
                nameof(ClinicSettingsService.MaxThresholdMagnitude),
                nameof(ClinicSettingsService.MaxThresholdDecimals),
            ],
            "every constant here must be a storage or list limit - no clinical value may be "
            + "compiled in (plan section 7, Clinical-rule boundary)");
    }
}
