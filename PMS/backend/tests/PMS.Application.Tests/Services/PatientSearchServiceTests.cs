using FluentAssertions;
using PMS.Application.Dtos.Patients;
using PMS.Application.Exceptions;
using PMS.Application.Services;
using PMS.Application.Tests.TestDoubles;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Tests.Services;

/// <summary>
/// F-7 backend unit tests (plan F-7 point 6): "last-4 phone match, ranking order, inactive
/// exclusion, min-length rejection".
/// </summary>
/// <remarks>
/// <para>
/// The four cases the plan names are all here, plus the ones the plan's own <em>assumption</em>
/// commits to and would otherwise ship unchecked: the C-22 search semantics (substring, minimum
/// length 2, ranking order, inactive/merged exclusion) and the E-30 fuzzy fallback with its
/// threshold.
/// </para>
/// <para>
/// These run the real <c>PatientSearchRules</c> expression trees — the fake repository compiles the
/// same trees EF Core turns into SQL — so a ranking assertion here is an assertion about the rule
/// that ships, not about a test-only copy of it.
/// </para>
/// </remarks>
public class PatientSearchServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    private readonly FakePatientRepository _patients = new();
    private readonly PatientSearchService _service;

    public PatientSearchServiceTests()
    {
        _service = new PatientSearchService(_patients, new FixedClock(Now));
    }

    /// <summary>
    /// Seeds a patient the way <c>PatientService</c> would have saved one — normalized columns
    /// derived, never hand-written — so search is exercised against realistic stored values.
    /// </summary>
    private Patient Seed(
        string fullName,
        string? phone = null,
        PatientStatus status = PatientStatus.Active,
        Guid? mergedInto = null,
        DateOnly? dateOfBirth = null,
        string? gender = "Female",
        int registeredDaysAgo = 0)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            NormalizedName = PatientNormalizer.NormalizeName(fullName),
            PrimaryPhone = phone,
            NormalizedPhone = PatientNormalizer.NormalizePhone(phone),
            DateOfBirth = dateOfBirth ?? new DateOnly(1985, 3, 2),
            Gender = gender,
            Status = status,
            MergedIntoPatientId = mergedInto,
            RegisteredUtc = Now.AddDays(-registeredDaysAgo),
        };

        _patients.Seed(patient);
        return patient;
    }

    // --- minimum query length (C-22/C-35) -----------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("r")]
    [InlineData("   r   ")]
    [InlineData(null)]
    public async Task A_query_shorter_than_the_minimum_is_rejected_rather_than_matching_everything(
        string? query)
    {
        // Answering a one-character query with the whole table is the name-only wall of rows
        // RSK-12 is about. It is also a full scan on every keystroke, for no information.
        Seed("Ravi Kumar");

        var act = async () => await _service.SearchAsync(query, false, null, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ValidationFailedException>();
        thrown.Which.Errors.Should().ContainKey("query");
    }

    [Fact]
    public async Task Query_length_is_measured_after_normalisation_not_before()
    {
        // "  r  " is five characters and one letter. Measuring the raw string would let it through
        // and then match half the table.
        Seed("Ravi Kumar");

        var act = async () => await _service.SearchAsync("  r  ", false, null, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task The_minimum_is_two_characters_and_two_characters_is_allowed()
    {
        PatientSearchRules.MinimumQueryLength.Should().Be(
            2,
            "plan F-7 point 1 fixes the minimum query length at 2 - this pins the C-22 assumption");

        Seed("Ravi Kumar");

        var results = await _service.SearchAsync("ra", false, null, CancellationToken.None);

        results.Should().HaveCount(1);
    }

    // --- phone matching, including the last-4 case (E-59, AC-1) -------------

    [Fact]
    public async Task Typing_the_last_four_digits_of_a_stored_phone_finds_that_patient()
    {
        // Acceptance criterion 1, and the case the brainstorm says a physician actually remembers.
        Seed("Ravi Kumar", "+91 98765-43210");
        Seed("Sunita Devi", "+91 91234-56789");

        var results = await _service.SearchAsync("3210", false, null, CancellationToken.None);

        results.Should().ContainSingle()
            .Which.FullName.Should().Be("Ravi Kumar");
    }

    [Fact]
    public async Task A_last_four_match_is_reported_as_a_phone_match_not_a_name_match()
    {
        Seed("Ravi Kumar", "+91 98765-43210");

        var results = await _service.SearchAsync("3210", false, null, CancellationToken.None);

        results.Single().MatchKind.Should().Be(PatientMatchKind.Phone);
    }

    [Theory]
    [InlineData("+91 98765-43210")]
    [InlineData("098765 43210")]
    [InlineData("9876543210")]
    [InlineData("(98765) 43210")]
    public async Task Any_stored_phone_format_is_found_by_its_digits(string storedFormat)
    {
        // E-59: the fix is a normalised digits-only index, never policing what the doctor types.
        Seed("Ravi Kumar", storedFormat);

        var results = await _service.SearchAsync("9876543210", false, null, CancellationToken.None);

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task A_query_typed_with_punctuation_still_matches_the_digits()
    {
        Seed("Ravi Kumar", "9876543210");

        var results = await _service.SearchAsync("43-210", false, null, CancellationToken.None);

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task A_patient_with_no_phone_is_never_matched_by_a_digit_query()
    {
        // NormalizedPhone is null rather than empty for exactly this reason: every phone-less
        // patient sharing one blank key would make them all match each other.
        Seed("Ravi Kumar", phone: null);

        var results = await _service.SearchAsync("3210", false, null, CancellationToken.None);

        results.Should().BeEmpty();
    }

    // --- ranking order (plan F-7 point 1) -----------------------------------

    [Fact]
    public async Task Results_are_ranked_exact_then_prefix_then_word_prefix_then_substring_then_phone()
    {
        // The plan's stated order. Five patients, each reachable only through one bucket, seeded in
        // deliberately wrong order so a pass cannot come from insertion order.
        Seed("Chandrakumar", registeredDaysAgo: 1);              // substring: contains "kumar"
        Seed("Ravi Kumar", registeredDaysAgo: 2);                // word prefix: " kumar"
        Seed("Nita Sharma", "9000012345", registeredDaysAgo: 3); // phone only
        Seed("Kumar", registeredDaysAgo: 4);                     // exact
        Seed("Kumaraswamy", registeredDaysAgo: 5);               // name prefix

        var results = await _service.SearchAsync("kumar", false, null, CancellationToken.None);

        results.Select(r => r.FullName).Should().Equal(
            "Kumar",
            "Kumaraswamy",
            "Ravi Kumar",
            "Chandrakumar");

        // The phone-only patient is absent because "kumar" has no digits in it - there is nothing
        // to match a phone against.
        results.Should().NotContain(r => r.FullName == "Nita Sharma");
    }

    [Fact]
    public async Task A_digit_query_ranks_an_exact_phone_above_a_suffix_match()
    {
        Seed("Exact Match", "3210");
        Seed("Suffix Match", "9876543210");

        var results = await _service.SearchAsync("3210", false, null, CancellationToken.None);

        results.Select(r => r.FullName).Should().Equal("Exact Match", "Suffix Match");
    }

    [Fact]
    public async Task Within_one_rank_the_most_recently_registered_patient_comes_first()
    {
        Seed("Ravi Kumar", registeredDaysAgo: 90);
        Seed("Ravi Kumari", registeredDaysAgo: 1);

        // Queried as "ravi", not "ravi kumar": the longer query would make "Ravi Kumar" an *exact*
        // match and put the two rows in different buckets, which would test the ranking again
        // rather than the tie-break. Both of these are plain name-prefix matches.
        var results = await _service.SearchAsync("ravi", false, null, CancellationToken.None);

        // Recency breaks the tie, because the person registered yesterday is more likely to be the
        // one being looked for today.
        results.Select(r => r.FullName).Should().Equal("Ravi Kumari", "Ravi Kumar");
    }

    [Fact]
    public void The_rank_constants_are_ordered_the_way_the_plan_states()
    {
        // Pins the plan's "exact-prefix -> prefix -> substring -> phone" as numbers, so reordering
        // a bucket is a failing test rather than a silently different picker.
        PatientSearchRules.RankExactName.Should().BeLessThan(PatientSearchRules.RankNamePrefix);
        PatientSearchRules.RankNamePrefix.Should().BeLessThan(PatientSearchRules.RankNameWordPrefix);
        PatientSearchRules.RankNameWordPrefix.Should().BeLessThan(PatientSearchRules.RankNameSubstring);
        PatientSearchRules.RankNameSubstring.Should().BeLessThan(PatientSearchRules.RankPhoneExact);
        PatientSearchRules.RankPhoneExact.Should().BeLessThan(PatientSearchRules.RankPhoneSuffix);
        PatientSearchRules.RankPhoneSuffix.Should().BeLessThan(PatientSearchRules.RankPhoneSubstring);
        PatientSearchRules.RankPhoneSubstring.Should().BeLessThan(PatientSearchRules.RankFuzzyName);
    }

    // --- name matching and normalisation (E-57, E-60) -----------------------

    [Fact]
    public async Task Search_is_case_insensitive_and_whitespace_insensitive()
    {
        // E-60. The stored name went through the same normaliser on save, so "  RAVI   KUMAR  "
        // and "Ravi Kumar" are one person on both sides of the comparison.
        Seed("  Ravi   Kumar  ");

        var results = await _service.SearchAsync("RAVI KUMAR", false, null, CancellationToken.None);

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task A_non_latin_name_is_searchable_in_its_own_script()
    {
        // E-57. Normalisation preserves Unicode rather than transliterating it, so this has to work
        // or the clinic cannot find half its patients.
        Seed("रवि कुमार");

        var results = await _service.SearchAsync("कुमार", false, null, CancellationToken.None);

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task A_mononym_is_findable()
    {
        // C-18/E-13: the registration form accepts a single-word name, so search must too.
        Seed("Meenakshi");

        var results = await _service.SearchAsync("meenakshi", false, null, CancellationToken.None);

        results.Should().ContainSingle();
    }

    // --- inactive and merged exclusion (plan F-7 point 1) -------------------

    [Fact]
    public async Task Inactive_patients_are_excluded_by_default()
    {
        Seed("Ravi Kumar");
        Seed("Ravi Kumar Retired", status: PatientStatus.Inactive);

        var results = await _service.SearchAsync("ravi", false, null, CancellationToken.None);

        results.Should().ContainSingle().Which.FullName.Should().Be("Ravi Kumar");
    }

    [Fact]
    public async Task Merged_patients_are_excluded_by_default_even_while_still_active()
    {
        // F-6 marks a duplicate by pointing it at a survivor. Status alone would not hide it, so
        // the merged pointer is checked independently.
        var survivor = Seed("Ravi Kumar");
        Seed("Ravi Kumar Duplicate", mergedInto: survivor.Id);

        var results = await _service.SearchAsync("ravi", false, null, CancellationToken.None);

        results.Should().ContainSingle().Which.FullName.Should().Be("Ravi Kumar");
    }

    [Fact]
    public async Task The_include_inactive_toggle_returns_both_and_flags_what_they_are()
    {
        var survivor = Seed("Ravi Kumar");
        Seed("Ravi Kumar Retired", status: PatientStatus.Inactive);
        Seed("Ravi Kumar Duplicate", mergedInto: survivor.Id);

        var results = await _service.SearchAsync("ravi", true, null, CancellationToken.None);

        results.Should().HaveCount(3);
        results.Single(r => r.FullName == "Ravi Kumar Retired").Status.Should().Be("Inactive");
        results.Single(r => r.FullName == "Ravi Kumar Duplicate").IsMerged.Should().BeTrue();

        // Being found is not the same as looking ordinary: a merged record arrives labelled, so it
        // can never be mistaken for a live one.
        results.Single(r => r.FullName == "Ravi Kumar").IsMerged.Should().BeFalse();
    }

    // --- never a name-only row, never auto-selected (E-28, RSK-12) ----------

    [Fact]
    public async Task Two_patients_sharing_a_name_and_an_age_are_still_distinguishable()
    {
        // Acceptance criterion 2, and the feature's dominant risk. Identical name, identical date
        // of birth - the row still has to carry something that tells them apart.
        var dob = new DateOnly(1985, 3, 2);
        Seed("Ravi Kumar", "+91 98765-43210", dateOfBirth: dob, registeredDaysAgo: 30);
        Seed("Ravi Kumar", "+91 91234-59999", dateOfBirth: dob, registeredDaysAgo: 2);

        var results = await _service.SearchAsync("ravi kumar", false, null, CancellationToken.None);

        results.Should().HaveCount(2);
        results.Select(r => r.FullName).Distinct().Should().ContainSingle("the names are identical");
        results.Select(r => r.AgeDisplay).Distinct().Should().ContainSingle("the ages are identical");

        // ...and yet every row is distinguishable, on two independent axes.
        results.Select(r => r.PhoneTail).Should().OnlyHaveUniqueItems();
        results.Select(r => r.RegisteredOn).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Every_row_carries_every_disambiguating_field_the_picker_needs()
    {
        Seed("Ravi Kumar", "+91 98765-43210");

        var row = (await _service.SearchAsync("ravi", false, null, CancellationToken.None)).Single();

        row.Id.Should().NotBeEmpty();
        row.FullName.Should().Be("Ravi Kumar");
        row.PhoneTail.Should().Be("3210");
        row.AgeDisplay.Should().NotBeNullOrWhiteSpace();
        row.Status.Should().Be("Active");
        row.RegisteredOn.Should().Be(DateOnly.FromDateTime(Now.UtcDateTime));
    }

    [Fact]
    public async Task A_single_match_is_still_returned_as_a_list_and_never_auto_selected()
    {
        // The one thing this service must never grow is an "if exactly one match, return it"
        // branch. A confident-looking single result is how the wrong record gets opened unread.
        Seed("Ravi Kumar", "+91 98765-43210");

        var results = await _service.SearchAsync("ravi", false, null, CancellationToken.None);

        results.Should().BeAssignableTo<IReadOnlyList<PatientSummaryResponse>>();
        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task Last_visit_date_is_null_until_F_10_and_the_registration_date_carries_the_gap()
    {
        // Pins the documented F-7 assumption. When F-10 lands and starts filling LastVisitDate in,
        // this test fails - which is the point: it is a reminder to revisit the picker's date
        // column, not a rule that the field must stay empty forever.
        Seed("Ravi Kumar");

        var row = (await _service.SearchAsync("ravi", false, null, CancellationToken.None)).Single();

        row.LastVisitDate.Should().BeNull("Visit is F-10 and does not exist on this branch");
        row.RegisteredOn.Should().Be(DateOnly.FromDateTime(Now.UtcDateTime));
    }

    [Fact]
    public async Task An_incomplete_profile_is_flagged_in_the_picker_the_same_way_it_is_on_the_profile()
    {
        // The picker and the profile must not disagree about whether a record is thin - the picker
        // is where someone decides to use it.
        Seed("No Contact", phone: null, gender: null);

        var row = (await _service.SearchAsync("no contact", false, null, CancellationToken.None)).Single();

        row.IsProfileIncomplete.Should().BeTrue();
        row.PhoneTail.Should().BeNull();
    }

    // --- take / TOP N -------------------------------------------------------

    [Fact]
    public async Task The_default_page_size_is_the_plans_top_20()
    {
        for (var i = 0; i < 30; i++)
        {
            Seed($"Kumar {i:D2}");
        }

        var results = await _service.SearchAsync("kumar", false, null, CancellationToken.None);

        results.Should().HaveCount(PatientSearchRules.DefaultTake);
        PatientSearchRules.DefaultTake.Should().Be(20);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-5, 20)]
    [InlineData(5, 5)]
    [InlineData(1000, 50)]
    public async Task Take_is_clamped_rather_than_rejected(int requested, int expected)
    {
        for (var i = 0; i < 60; i++)
        {
            Seed($"Kumar {i:D2}");
        }

        var results = await _service.SearchAsync("kumar", false, requested, CancellationToken.None);

        results.Should().HaveCount(expected);
    }

    // --- fuzzy fallback (E-30) ---------------------------------------------

    [Fact]
    public async Task A_typo_in_the_stored_name_is_still_found_by_the_similarity_fallback()
    {
        // E-30 exactly: the record was typed as "Ravi Kumr" months ago, the physician searches the
        // correct spelling, and an exact-substring search would return nothing - at which point
        // they register the patient a second time and the history splits.
        Seed("Ravi Kumr", "+91 98765-43210");

        var results = await _service.SearchAsync("Ravi Kumar", false, null, CancellationToken.None);

        results.Should().ContainSingle().Which.FullName.Should().Be("Ravi Kumr");
    }

    [Fact]
    public async Task A_fuzzy_hit_is_labelled_as_a_guess_and_never_as_an_exact_match()
    {
        // A suggestion rendered identically to a certainty is a new wrong-patient path.
        Seed("Suneeta Devi");

        var results = await _service.SearchAsync("Sunita Devi", false, null, CancellationToken.None);

        results.Should().ContainSingle().Which.MatchKind.Should().Be(PatientMatchKind.SimilarName);
    }

    [Fact]
    public async Task The_fallback_runs_only_when_the_exact_search_found_nothing()
    {
        // The fallback is the one path in this feature that touches more than `take` rows. It must
        // not run on the hot path, and this asserts it by counting the read rather than trusting
        // the code shape.
        Seed("Ravi Kumar");

        await _service.SearchAsync("ravi", false, null, CancellationToken.None);
        _patients.NameKeyReadCount.Should().Be(0, "an exact match means no fallback is needed");

        await _service.SearchAsync("zzqq", false, null, CancellationToken.None);
        _patients.NameKeyReadCount.Should().Be(1);
    }

    [Fact]
    public async Task An_unrelated_name_is_not_dragged_in_by_the_fallback()
    {
        Seed("Kavita Sharma");

        var results = await _service.SearchAsync("Ravi", false, null, CancellationToken.None);

        results.Should().BeEmpty("'Ravi' and 'Kavita Sharma' are not the same person");
    }

    [Fact]
    public async Task The_fallback_still_excludes_inactive_and_merged_records_by_default()
    {
        Seed("Ravi Kumr", status: PatientStatus.Inactive);

        var results = await _service.SearchAsync("Ravi Kumar", false, null, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task A_genuinely_unknown_name_returns_an_empty_list_rather_than_an_error()
    {
        // E-7. Empty is an answer; the client turns it into "register this typed name". Throwing
        // here would make the register action unreachable at exactly the moment it is needed.
        Seed("Ravi Kumar");

        var results = await _service.SearchAsync("Zenobia Xylophone", false, null, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public void The_fallback_threshold_is_pinned_at_the_documented_value()
    {
        // ASSUMPTION under C-22, deliberately looser than F-6's 0.85 duplicate-warning threshold -
        // the reasoning is in PatientNameSimilarity. Pinned so a change is a deliberate act.
        PatientNameSimilarity.SearchFallbackThreshold.Should().Be(0.7);

        PatientNameSimilarity.Score("ravi kumar", "ravi kumr").Should().BeGreaterThan(0.7);
        PatientNameSimilarity.Score("sunita devi", "suneeta devi").Should().BeGreaterThan(0.7);
        PatientNameSimilarity.Score("ravi", "kavita sharma").Should().BeLessThan(0.7);
    }

    [Fact]
    public void Similarity_scores_an_identical_name_as_one_and_an_empty_name_as_zero()
    {
        PatientNameSimilarity.Score("ravi kumar", "ravi kumar").Should().Be(1.0);
        PatientNameSimilarity.Score("ravi kumar", "").Should().Be(0);
        PatientNameSimilarity.Score("", "ravi kumar").Should().Be(0);
        PatientNameSimilarity.Score(null, "ravi kumar").Should().Be(0);
    }

    [Fact]
    public void Similarity_matches_a_single_word_against_a_full_name()
    {
        // Whole-string similarity between "kumr" and "ravi kumar" is well under the threshold; the
        // per-word pass is what makes a partially-remembered name findable.
        PatientNameSimilarity.Score("kumr", "ravi kumar").Should().BeGreaterThan(0.7);
    }

    // --- recent patients (E-2, BRD L159) ------------------------------------

    [Fact]
    public async Task Recent_patients_are_the_most_recently_registered_first()
    {
        Seed("Oldest", registeredDaysAgo: 30);
        Seed("Newest", registeredDaysAgo: 1);
        Seed("Middle", registeredDaysAgo: 10);

        var results = await _service.GetRecentAsync(null, CancellationToken.None);

        results.Select(r => r.FullName).Should().Equal("Newest", "Middle", "Oldest");
    }

    [Fact]
    public async Task Recent_patients_defaults_to_ten_and_clamps()
    {
        for (var i = 0; i < 60; i++)
        {
            Seed($"Patient {i:D2}", registeredDaysAgo: i);
        }

        (await _service.GetRecentAsync(null, CancellationToken.None)).Should().HaveCount(10);
        (await _service.GetRecentAsync(3, CancellationToken.None)).Should().HaveCount(3);
        (await _service.GetRecentAsync(999, CancellationToken.None)).Should().HaveCount(50);

        PatientSearchRules.DefaultRecentTake.Should().Be(10, "plan F-7 point 3 specifies take=10");
    }

    [Fact]
    public async Task Recent_patients_is_empty_and_not_an_error_on_a_fresh_install()
    {
        // E-2 / acceptance criterion 5. An empty list is the correct answer; the client renders an
        // empty state offering "register the first patient" rather than a blank panel.
        var results = await _service.GetRecentAsync(null, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Recent_patients_never_offers_a_retired_or_merged_record()
    {
        // No toggle here, deliberately: "recent" is a shortcut back to work, and a shortcut should
        // not offer a record the clinic has already retired.
        var survivor = Seed("Active One");
        Seed("Retired One", status: PatientStatus.Inactive);
        Seed("Merged One", mergedInto: survivor.Id);

        var results = await _service.GetRecentAsync(null, CancellationToken.None);

        results.Should().ContainSingle().Which.FullName.Should().Be("Active One");
    }

    [Fact]
    public async Task Recent_rows_carry_the_same_disambiguating_fields_as_search_rows()
    {
        // Same picker component renders both, so the same guarantee has to hold on both paths.
        Seed("Ravi Kumar", "+91 98765-43210");

        var row = (await _service.GetRecentAsync(null, CancellationToken.None)).Single();

        row.PhoneTail.Should().Be("3210");
        row.AgeDisplay.Should().NotBeNullOrWhiteSpace();
        row.MatchKind.Should().Be(PatientMatchKind.Recent);
    }
}
