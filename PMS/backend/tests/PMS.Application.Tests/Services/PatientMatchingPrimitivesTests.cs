using FluentAssertions;
using PMS.Application.Services;

namespace PMS.Application.Tests.Services;

/// <summary>
/// F-6's two matching primitives — the phone key that answers Q-13, and the name similarity that
/// decides how alike is alike enough (plan F-6 point 1).
/// </summary>
/// <remarks>
/// A file the plan does not name, alongside the <c>PatientDuplicateServiceTests.cs</c> it does. The
/// service tests prove the rule is applied; these prove the rule is what we think it is. Keeping
/// them apart matters because Q-13 is still open: when the physician answers it, these are the tests
/// that change, and they should be findable without reading a service suite to locate them.
/// </remarks>
public class PatientMatchingPrimitivesTests
{
    // --- the phone matching key (Q-13) --------------------------------------

    /// <summary>
    /// The case verification-pms raised against F-5, and the reason F-6 needed a phone rule at all.
    /// </summary>
    /// <remarks>
    /// F-5 stores digits only, so these three forms of one number persist as <c>919876543210</c>,
    /// <c>09876543210</c> and <c>9876543210</c> — three strings that never match each other. A
    /// physician typing the same number with a country code one week and a trunk zero the next would
    /// have produced two patient records with no warning at all, which is E-25 happening through the
    /// exact mechanism the duplicate check exists to catch.
    /// </remarks>
    [Theory]
    [InlineData("+91 98765 43210")]
    [InlineData("+919876543210")]
    [InlineData("91 98765-43210")]
    [InlineData("098765 43210")]
    [InlineData("0 98765 43210")]
    [InlineData("9876543210")]
    [InlineData("98765-43210")]
    [InlineData("00 91 98765 43210")]
    public void Every_way_of_writing_one_indian_mobile_number_produces_the_same_matching_key(
        string typed)
    {
        PatientNormalizer.PhoneMatchKey(typed).Should().Be("9876543210");
    }

    [Fact]
    public void The_country_code_and_the_trunk_zero_forms_match_each_other_specifically()
    {
        // Stated as its own assertion rather than left implied by the theory above, because this is
        // the precise pair that was reported as not matching. If this ever goes red, the feature has
        // regressed to F-5's behaviour.
        var withCountryCode = PatientNormalizer.PhoneMatchKey("+91 98765 43210");
        var withTrunkZero = PatientNormalizer.PhoneMatchKey("098765 43210");

        withCountryCode.Should().Be(withTrunkZero);
        withCountryCode.Should().NotBeNull();
    }

    [Fact]
    public void The_faithful_digits_column_is_not_changed_by_any_of_this()
    {
        // The matching key is lossy; NormalizedPhone is not, and must not become so. The number the
        // physician typed stays recoverable - PhoneMatchKey is an additional column, never a
        // replacement (E-59).
        PatientNormalizer.NormalizePhone("+91 98765 43210").Should().Be("919876543210");
        PatientNormalizer.NormalizePhone("098765 43210").Should().Be("09876543210");
        PatientNormalizer.NormalizePhone("9876543210").Should().Be("9876543210");
    }

    [Fact]
    public void Only_one_leading_zero_is_dropped()
    {
        // "007890123" is a nine-digit local number written with a trunk zero, not an invitation to
        // strip zeros until the result looks like a mobile number. Stripping greedily would fold
        // 07890123 and 7890123 onto the same key as well, and those are different numbers.
        PatientNormalizer.PhoneMatchKey("007890123").Should().Be("07890123");
    }

    [Theory]
    [InlineData("204")]
    [InlineData("12 34")]
    [InlineData("00")]
    [InlineData("0")]
    public void A_number_too_short_to_identify_anyone_produces_no_key_at_all(string typed)
    {
        // Null, not a short key. If a three-digit extension produced a key, every patient reachable
        // on "extension 204" would be a candidate duplicate of every other one - a warning that
        // fires constantly is a warning that is switched off in the reader's head.
        PatientNormalizer.PhoneMatchKey(typed).Should().BeNull();
    }

    [Fact]
    public void No_phone_produces_no_key_so_phoneless_patients_never_match_each_other()
    {
        PatientNormalizer.PhoneMatchKey(null).Should().BeNull();
        PatientNormalizer.PhoneMatchKey("").Should().BeNull();
        PatientNormalizer.PhoneMatchKey("   ").Should().BeNull();
        PatientNormalizer.PhoneMatchKey("no phone").Should().BeNull();
    }

    [Fact]
    public void A_landline_shorter_than_ten_digits_keeps_all_of_its_digits()
    {
        // Ten is a maximum, not a requirement. An eight-digit landline is matched on all eight
        // rather than padded or rejected.
        PatientNormalizer.PhoneMatchKey("2345 6789").Should().Be("23456789");
    }

    [Fact]
    public void Two_genuinely_different_numbers_do_not_share_a_key()
    {
        PatientNormalizer.PhoneMatchKey("98765 43210")
            .Should().NotBe(PatientNormalizer.PhoneMatchKey("98765 43211"));
    }

    [Fact]
    public void Devanagari_digits_are_read_as_the_number_they_are()
    {
        // E-57's script rule reaching the matching key: the same number typed in a different digit
        // script is the same number, and must not become a second patient.
        PatientNormalizer.PhoneMatchKey("९८७६५४३२१०").Should().Be("9876543210");
    }

    // --- name similarity ----------------------------------------------------

    [Fact]
    public void An_identical_name_scores_one()
    {
        NameSimilarity.Ratio("ravi kumar", "ravi kumar").Should().Be(1d);
    }

    [Fact]
    public void A_single_typo_stays_above_the_threshold()
    {
        // "Ravi Kumaar" - one inserted character. This is E-30's case: the returning patient whose
        // name was mistyped once, who search then fails to find, who is then registered again.
        var score = NameSimilarity.Ratio("ravi kumar", "ravi kumaar");

        score.Should().BeApproximately(1d - (1d / 11d), 1e-9);
        score.Should().BeGreaterThanOrEqualTo(NameSimilarity.DefaultThreshold);
    }

    [Fact]
    public void Two_edits_in_a_short_name_fall_below_the_threshold()
    {
        // The other side of the boundary, asserted with its exact value so the threshold's effect is
        // visible rather than inferred: two substitutions in a ten-character name is 0.8, and 0.8 is
        // below 0.85. These are two different people and the check says so.
        var score = NameSimilarity.Ratio("ravi kumar", "ravi kumbh");

        score.Should().BeApproximately(0.8d, 1e-9);
        score.Should().BeLessThan(NameSimilarity.DefaultThreshold);
    }

    [Fact]
    public void Reversing_the_word_order_is_not_treated_as_a_different_name()
    {
        // Given-name-first versus family-name-first is not a spelling difference, and this
        // application deliberately does not model a name as first + last (C-18, E-13), so it cannot
        // reorder them structurally. Character-by-character these two score 0.36; the word-sorted
        // comparison recognises them.
        NameSimilarity.Ratio("ravi kumar", "kumar ravi").Should().Be(1d);
    }

    [Fact]
    public void Unrelated_names_score_far_below_the_threshold()
    {
        NameSimilarity.Ratio("ravi kumar", "priya menon")
            .Should().BeLessThan(NameSimilarity.DefaultThreshold);
    }

    [Fact]
    public void A_missing_name_scores_zero_rather_than_something_small()
    {
        // Zero, not a near-miss. On a very short name a small non-zero floor could drift over the
        // threshold and make a blank match a real patient.
        NameSimilarity.Ratio("", "ravi kumar").Should().Be(0d);
        NameSimilarity.Ratio(null, "ravi kumar").Should().Be(0d);
        NameSimilarity.Ratio("ravi kumar", null).Should().Be(0d);
    }

    [Fact]
    public void Distance_is_measured_in_readable_characters_not_utf16_code_units()
    {
        // A Devanagari name and the same name with one changed matra. Measured in UTF-16 units the
        // strings look longer than they read, which would hold non-Latin names to a quietly stricter
        // threshold than Latin ones (E-57). Measured in grapheme clusters, one changed character in
        // a four-character name is one edit.
        var score = NameSimilarity.Ratio("रवि कुमार", "रवि कुमारी");

        score.Should().BeGreaterThan(0.5d);
    }

    [Fact]
    public void Similarity_is_symmetric()
    {
        // Not decorative: the service compares the typed name against each stored name, and if the
        // score depended on argument order the same pair of patients would match or not depending on
        // which of them registered first.
        NameSimilarity.Ratio("ravi kumar", "ravi kumaar")
            .Should().Be(NameSimilarity.Ratio("ravi kumaar", "ravi kumar"));
    }

    [Fact]
    public void The_threshold_is_the_number_the_plan_states()
    {
        // Pinned so that a change to Q-13's answer is a deliberate edit to a test, not a quiet drift
        // in a constant nobody is watching.
        NameSimilarity.DefaultThreshold.Should().Be(0.85d);
    }

    // --- one function, two thresholds (F-6 + F-7 merge) ---------------------

    [Fact]
    public void The_two_features_share_one_algorithm_and_keep_two_thresholds()
    {
        // F-6 and F-7 were built in parallel and each shipped its own copy of this algorithm. They
        // are one type now, and that is not cosmetic: two implementations left to drift would mean
        // the duplicate check and the search disagreeing about who is the same person.
        //
        // The *thresholds* stay two numbers on purpose (plan F-7 point 1 says "the same similarity
        // function", not the same threshold). F-6 interrupts a registration, so it is strict; F-7
        // offers an extra row on an already-empty screen after finding nothing, where a false
        // negative costs a duplicate record and a split history (E-30). Asserting the ordering here
        // means "just make them the same number" fails a test with a reason attached.
        NameSimilarity.SearchFallbackThreshold.Should().Be(0.7d);
        NameSimilarity.SearchFallbackThreshold
            .Should().BeLessThan(NameSimilarity.DefaultThreshold);
    }

    [Fact]
    public void The_search_score_is_built_on_the_duplicate_checks_ratio()
    {
        // Score is Ratio plus a word-level pass, so it can never report two whole names as *less*
        // alike than Ratio does. If someone reimplemented Score independently, this is the
        // assertion that notices.
        foreach (var (query, name) in new[]
                 {
                     ("ravi kumar", "ravi kumaar"),
                     ("ravi kumar", "kumar ravi"),
                     ("sunita devi", "suneeta devi"),
                     ("ravi kumar", "priya menon"),
                 })
        {
            NameSimilarity.Score(query, name)
                .Should().BeGreaterThanOrEqualTo(NameSimilarity.Ratio(query, name));
        }
    }

    [Fact]
    public void The_search_score_reaches_a_full_name_from_one_typed_word()
    {
        // The reason Score exists at all rather than F-7 calling Ratio: one side is a fragment the
        // physician typed. Whole-string Ratio between "kumr" and "ravi kumar" is far below either
        // threshold, and E-30's duplicate gets created.
        NameSimilarity.Ratio("kumr", "ravi kumar")
            .Should().BeLessThan(NameSimilarity.SearchFallbackThreshold);

        NameSimilarity.Score("kumr", "ravi kumar")
            .Should().BeGreaterThanOrEqualTo(NameSimilarity.SearchFallbackThreshold);
    }
}
