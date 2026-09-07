using FluentAssertions;
using PMS.Application.Dtos.Patients;
using PMS.Application.Exceptions;
using PMS.Application.Services;
using PMS.Application.Tests.TestDoubles;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Tests.Services;

/// <summary>
/// F-6 backend unit tests (plan F-6 point 6): "name-similarity boundary either side of the
/// threshold, shared household phone returns all matches (E-27), self-exclusion on edit".
/// </summary>
/// <remarks>
/// The three named cases are here. The suite goes further on <c>mark-merged</c> than the plan's
/// single named case does, because that is the one method in this feature that writes anything, and
/// plan F-6 point 5 makes a strong claim about it — "no destructive operation exists in this
/// feature's API surface at all". A claim like that is worth pinning from several directions rather
/// than trusting to the shape of the code.
/// </remarks>
public class PatientDuplicateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 10, 30, 0, TimeSpan.Zero);

    private readonly FixedClock _clock = new(Now);
    private readonly FakePatientRepository _patients = new();

    private PatientDuplicateService Build() => new(_patients, _clock);

    /// <summary>
    /// Seeds a patient the way <c>PatientService</c> would have written one — derived columns
    /// included.
    /// </summary>
    /// <remarks>
    /// Deriving <c>NormalizedName</c> and <c>PhoneMatchKey</c> here rather than accepting them as
    /// arguments is deliberate: a test that hand-wrote a matching key could seed a row the
    /// application could never produce, and would then prove something about a database state that
    /// cannot occur.
    /// </remarks>
    private Patient Seed(
        string fullName,
        string? phone = null,
        DateOnly? dateOfBirth = null,
        PatientStatus status = PatientStatus.Active,
        Guid? mergedInto = null,
        int registeredDaysAgo = 30)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            NormalizedName = PatientNormalizer.NormalizeName(fullName),
            PrimaryPhone = phone,
            NormalizedPhone = PatientNormalizer.NormalizePhone(phone),
            PhoneMatchKey = PatientNormalizer.PhoneMatchKey(phone),
            DateOfBirth = dateOfBirth,
            RegisteredUtc = Now.AddDays(-registeredDaysAgo),
            Status = status,
            MergedIntoPatientId = mergedInto,
        };

        _patients.Seed(patient);
        return patient;
    }

    private static DuplicateCheckRequest ACheck(
        string fullName = "Ravi Kumar",
        string? phone = null,
        DateOnly? dateOfBirth = null,
        Guid? exclude = null) => new()
        {
            FullName = fullName,
            Phone = phone,
            DateOfBirth = dateOfBirth,
            ExcludePatientId = exclude,
        };

    // --- the identity rule (plan F-6 point 1, Q-13) --------------------------

    [Fact]
    public async Task The_same_name_and_the_same_phone_is_flagged_as_a_likely_duplicate()
    {
        var existing = Seed("Ravi Kumar", phone: "98765 43210");

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210"), default);

        found.Should().ContainSingle();
        found[0].Id.Should().Be(existing.Id);
        found[0].IsLikelyDuplicate.Should().BeTrue();
        found[0].MatchReason.Should().Be(DuplicateMatchReason.Phone);
    }

    [Fact]
    public async Task A_country_code_and_a_trunk_zero_are_the_same_phone_number()
    {
        // Q-13, and the specific gap verification-pms reported against F-5. The record was written
        // with a country code and the new registration types a trunk zero; under F-5's digits-only
        // equality these never met, and this patient's history would have split in two with no
        // warning shown to anybody.
        var existing = Seed("Ravi Kumar", phone: "+91 98765 43210");

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "098765 43210"), default);

        found.Should().ContainSingle().Which.Id.Should().Be(existing.Id);
        found[0].IsLikelyDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task The_same_name_and_the_same_date_of_birth_is_flagged_without_any_phone()
    {
        // The second branch of the rule. A patient with no phone on either record is still
        // detectable when the date of birth is known (E-8, E-20 - phone is optional here).
        var dob = new DateOnly(1985, 3, 2);
        var existing = Seed("Ravi Kumar", dateOfBirth: dob);

        var found = await Build().FindCandidatesAsync(ACheck("Ravi Kumar", dateOfBirth: dob), default);

        found.Should().ContainSingle().Which.Id.Should().Be(existing.Id);
        found[0].MatchReason.Should().Be(DuplicateMatchReason.DateOfBirth);
        found[0].IsLikelyDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task Matching_on_both_phone_and_date_of_birth_is_reported_as_the_strongest_signal()
    {
        var dob = new DateOnly(1985, 3, 2);
        Seed("Ravi Kumar", phone: "98765 43210", dateOfBirth: dob);

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210", dateOfBirth: dob), default);

        found.Should().ContainSingle().Which.MatchReason
            .Should().Be(DuplicateMatchReason.PhoneAndDateOfBirth);
    }

    [Fact]
    public async Task A_one_character_typo_in_the_name_is_still_flagged()
    {
        // E-30 end to end through the service: the returning patient whose stored name has a typo.
        Seed("Ravi Kumaar", phone: "98765 43210");

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210"), default);

        found.Should().ContainSingle().Which.IsLikelyDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task A_name_below_the_similarity_threshold_is_returned_but_not_flagged()
    {
        // The boundary the plan names, seen from the service. "Ravi Kumbh" scores 0.8 against
        // "Ravi Kumar" - two different people who happen to share a phone. The row is still
        // returned, because sharing a phone is worth showing (E-27), but it is not a suspected
        // duplicate and it will not interrupt a registration.
        Seed("Ravi Kumbh", phone: "98765 43210");

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210"), default);

        found.Should().ContainSingle();
        found[0].IsLikelyDuplicate.Should().BeFalse();
        found[0].NameSimilarity.Should().BeLessThan(NameSimilarity.DefaultThreshold);
    }

    [Fact]
    public async Task A_matching_name_alone_is_never_enough()
    {
        // E-28: two patients with the same name is an ordinary occurrence in one clinic. Without a
        // shared phone or date of birth there is nothing to distinguish "same person" from "same
        // name", and warning on it would make the check noise.
        Seed("Ravi Kumar", phone: "22222 22222");

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210"), default);

        found.Should().BeEmpty();
    }

    [Fact]
    public async Task A_registration_with_neither_a_phone_nor_a_date_of_birth_finds_nothing()
    {
        // The rule's known blind spot, pinned so it is a recorded limitation rather than a surprise.
        // Two records for one person with no phone and no date of birth on either are not detected.
        Seed("Ravi Kumar");

        var found = await Build().FindCandidatesAsync(ACheck("Ravi Kumar"), default);

        found.Should().BeEmpty();
    }

    [Fact]
    public async Task A_blank_name_finds_nothing_rather_than_matching_everyone()
    {
        Seed("Ravi Kumar", phone: "98765 43210");

        var found = await Build().FindCandidatesAsync(
            ACheck("   ", phone: "98765 43210"), default);

        found.Should().BeEmpty();
    }

    [Fact]
    public async Task Two_patients_who_share_nothing_do_not_match()
    {
        Seed("Priya Menon", phone: "22222 22222", dateOfBirth: new DateOnly(1990, 1, 1));

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210", dateOfBirth: new DateOnly(1985, 3, 2)),
            default);

        found.Should().BeEmpty();
    }

    // --- E-27: one phone number for a whole family --------------------------

    [Fact]
    public async Task A_phone_shared_by_three_family_members_returns_all_three()
    {
        // Plan acceptance criterion 3, and E-27's rule that a phone number identifies a household
        // rather than a person. All three are returned; the one who is actually a suspected
        // duplicate is marked, and the other two are context - not hidden, and not warned about.
        const string householdPhone = "98765 43210";

        Seed("Ravi Kumar", phone: householdPhone, registeredDaysAgo: 90);
        Seed("Sunita Kumar", phone: householdPhone, registeredDaysAgo: 60);
        Seed("Anil Kumar", phone: householdPhone, registeredDaysAgo: 30);

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: householdPhone), default);

        found.Should().HaveCount(3);
        found.Select(c => c.FullName)
            .Should().BeEquivalentTo(["Ravi Kumar", "Sunita Kumar", "Anil Kumar"]);

        found.Where(c => c.IsLikelyDuplicate).Should().ContainSingle()
            .Which.FullName.Should().Be("Ravi Kumar");
    }

    [Fact]
    public async Task No_candidate_is_marked_as_the_answer()
    {
        // Acceptance criterion 3's second half: "none is auto-selected". There is no field on the
        // response that could express a selection, and the service returns a list rather than a best
        // match - the only ranking is the order, and order is a reading aid.
        const string householdPhone = "98765 43210";
        Seed("Ravi Kumar", phone: householdPhone);
        Seed("Ravi Kumar", phone: householdPhone);

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: householdPhone), default);

        found.Should().HaveCount(2);
        found.Should().OnlyContain(c => c.IsLikelyDuplicate);
    }

    // --- E-28 / REC-12: every row is disambiguating --------------------------

    [Fact]
    public async Task Every_candidate_carries_the_four_disambiguating_fields()
    {
        // Acceptance criterion 4, and RSK-12 - "wrong-patient selection from a name-only picker",
        // rated Critical. The physician is being asked "is this the same person?" about a list of
        // near-identical names, which is the exact screen where a name-only row does damage.
        Seed("Ravi Kumar", phone: "98765 43210", dateOfBirth: new DateOnly(1985, 3, 2));

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210"), default);

        var candidate = found.Should().ContainSingle().Subject;

        candidate.FullName.Should().Be("Ravi Kumar");
        candidate.PhoneTail.Should().Be("3210");
        candidate.AgeDisplay.Should().NotBeNullOrWhiteSpace();
        candidate.DateOfBirth.Should().Be(new DateOnly(1985, 3, 2));

        // The fourth field is on the contract and rendered; it stays null until F-10 builds the
        // Visit entity. Asserted rather than ignored so that F-10 has a test telling it to fill it.
        candidate.LastVisitDate.Should().BeNull("no Visit entity exists until F-10");
    }

    [Fact]
    public async Task The_full_phone_number_is_never_returned_on_a_candidate_row()
    {
        // Only the tail. This list can appear on a screen visible from the waiting side of the desk,
        // and it is shown before the physician has chosen to open anybody's record.
        Seed("Ravi Kumar", phone: "98765 43210");

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210"), default);

        found[0].PhoneTail.Should().Be("3210");
        typeof(DuplicateCandidateResponse).GetProperty("PrimaryPhone").Should().BeNull();
        typeof(DuplicateCandidateResponse).GetProperty("NormalizedPhone").Should().BeNull();
    }

    // --- self-exclusion on edit (plan F-6 point 6) --------------------------

    [Fact]
    public async Task A_patient_is_never_offered_as_a_duplicate_of_themselves()
    {
        // F-8 re-runs this check when a name or phone is edited. Without the exclusion, every edit
        // would report the patient as their own duplicate - and a warning that is always wrong is a
        // warning that gets dismissed without reading, including on the day it is right.
        var existing = Seed("Ravi Kumar", phone: "98765 43210");

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210", exclude: existing.Id), default);

        found.Should().BeEmpty();
    }

    [Fact]
    public async Task Excluding_one_patient_does_not_hide_a_real_duplicate()
    {
        var beingEdited = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 90);
        var other = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 10);

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210", exclude: beingEdited.Id), default);

        found.Should().ContainSingle().Which.Id.Should().Be(other.Id);
    }

    // --- inactive and already-merged records are still visible ---------------

    [Fact]
    public async Task A_retired_patient_is_still_offered_as_a_candidate()
    {
        // A returning patient whose record was deactivated is precisely the person about to be
        // registered a second time (E-25). Hiding retired records would make the check blind to the
        // case it exists for.
        var retired = Seed("Ravi Kumar", phone: "98765 43210", status: PatientStatus.Inactive);

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210"), default);

        found.Should().ContainSingle().Which.Id.Should().Be(retired.Id);
        found[0].Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task An_already_merged_record_is_returned_and_says_so()
    {
        // Half a duplicate cluster is worse than all of it: the physician needs to see that this
        // record already points somewhere, or they will mark it as a duplicate a second time.
        var survivor = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 200);
        var merged = Seed("Ravi Kumar", phone: "98765 43210", mergedInto: survivor.Id);

        var found = await Build().FindCandidatesAsync(
            ACheck("Ravi Kumar", phone: "98765 43210"), default);

        found.Should().HaveCount(2);
        found.Single(c => c.Id == merged.Id).MergedIntoPatientId.Should().Be(survivor.Id);
        found.Single(c => c.Id == survivor.Id).MergedIntoPatientId.Should().BeNull();
    }

    // --- the check writes nothing -------------------------------------------

    [Fact]
    public async Task Finding_candidates_never_writes_anything()
    {
        // The check is a question. This is the property that lets it be called on every blur of the
        // name and phone fields without the physician's half-filled form having consequences.
        Seed("Ravi Kumar", phone: "98765 43210");

        await Build().FindCandidatesAsync(ACheck("Ravi Kumar", phone: "98765 43210"), default);

        _patients.SaveCount.Should().Be(0);
        _patients.Saved.Should().ContainSingle();
    }

    // --- mark-merged: the pointer (plan F-6 point 5, E-26, E-33) -------------

    [Fact]
    public async Task Marking_a_record_as_merged_writes_a_pointer_and_retires_it()
    {
        var survivor = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 200);
        var duplicate = Seed("Ravi Kumar", phone: "98765 43210");

        var result = await Build().MarkMergedAsync(
            duplicate.Id,
            new MarkMergedRequest { MergedIntoPatientId = survivor.Id, Note = "Same person." },
            default);

        result.Id.Should().Be(duplicate.Id);
        result.Status.Should().Be("Inactive");

        duplicate.MergedIntoPatientId.Should().Be(survivor.Id);
        duplicate.Status.Should().Be(PatientStatus.Inactive);
        duplicate.InactiveReason.Should().Contain("Ravi Kumar").And.Contain("Same person.");
    }

    [Fact]
    public async Task Marking_a_record_as_merged_deletes_nothing_and_changes_nothing_on_the_survivor()
    {
        // Acceptance criteria 5 and 6. The survivor is read to validate the pointer and is otherwise
        // untouched - not renamed, not re-flagged, not given the duplicate's phone number. Both rows
        // are still there afterwards, which is the whole reason this is a pointer rather than a
        // merge (E-26).
        var survivor = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 200);
        var duplicate = Seed("Ravi Kumar", phone: "98765 43210");

        var survivorBefore = (survivor.FullName, survivor.Status, survivor.PrimaryPhone,
            survivor.InactiveReason, survivor.MergedIntoPatientId);

        await Build().MarkMergedAsync(
            duplicate.Id, new MarkMergedRequest { MergedIntoPatientId = survivor.Id }, default);

        (survivor.FullName, survivor.Status, survivor.PrimaryPhone, survivor.InactiveReason,
            survivor.MergedIntoPatientId).Should().Be(survivorBefore);

        _patients.Saved.Should().HaveCount(2);
        _patients.Saved.Select(p => p.Id).Should().Contain([survivor.Id, duplicate.Id]);
    }

    [Fact]
    public async Task The_marked_record_keeps_its_own_name_phone_and_age()
    {
        // Nothing is copied from the survivor. The losing record stays exactly as it was recorded,
        // because it is still the record its own visits are attached to.
        var survivor = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 200);
        var duplicate = Seed("Ravi Kumaar", phone: "098765 43210",
            dateOfBirth: new DateOnly(1985, 3, 2));

        await Build().MarkMergedAsync(
            duplicate.Id, new MarkMergedRequest { MergedIntoPatientId = survivor.Id }, default);

        duplicate.FullName.Should().Be("Ravi Kumaar");
        duplicate.PrimaryPhone.Should().Be("098765 43210");
        duplicate.DateOfBirth.Should().Be(new DateOnly(1985, 3, 2));
    }

    [Fact]
    public async Task A_note_is_optional_and_the_reason_still_names_the_survivor()
    {
        var survivor = Seed("Priya Menon", phone: "98765 43210", registeredDaysAgo: 200);
        var duplicate = Seed("Priya Menon", phone: "98765 43210");

        await Build().MarkMergedAsync(
            duplicate.Id, new MarkMergedRequest { MergedIntoPatientId = survivor.Id }, default);

        duplicate.InactiveReason.Should().Contain("Priya Menon");
    }

    [Fact]
    public async Task Marking_a_record_into_itself_is_rejected()
    {
        var patient = Seed("Ravi Kumar", phone: "98765 43210");

        var thrown = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Build().MarkMergedAsync(
                patient.Id, new MarkMergedRequest { MergedIntoPatientId = patient.Id }, default));

        thrown.Errors.Should().ContainKey(nameof(MarkMergedRequest.MergedIntoPatientId));
        patient.MergedIntoPatientId.Should().BeNull();
        patient.Status.Should().Be(PatientStatus.Active);
    }

    [Fact]
    public async Task An_empty_target_is_rejected()
    {
        var patient = Seed("Ravi Kumar", phone: "98765 43210");

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Build().MarkMergedAsync(
                patient.Id, new MarkMergedRequest { MergedIntoPatientId = Guid.Empty }, default));

        patient.Status.Should().Be(PatientStatus.Active);
    }

    [Fact]
    public async Task A_target_that_does_not_exist_is_rejected_without_touching_the_record()
    {
        var patient = Seed("Ravi Kumar", phone: "98765 43210");

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Build().MarkMergedAsync(
                patient.Id, new MarkMergedRequest { MergedIntoPatientId = Guid.NewGuid() }, default));

        patient.MergedIntoPatientId.Should().BeNull();
        patient.Status.Should().Be(PatientStatus.Active);
        _patients.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Marking_a_patient_who_does_not_exist_is_a_not_found()
    {
        var survivor = Seed("Ravi Kumar", phone: "98765 43210");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Build().MarkMergedAsync(
                Guid.NewGuid(),
                new MarkMergedRequest { MergedIntoPatientId = survivor.Id },
                default));
    }

    // --- mark-merged: cycles -------------------------------------------------

    [Fact]
    public async Task A_direct_cycle_is_rejected()
    {
        // The plan's named integration case, proven here at the service level too. A points at B; B
        // must not be allowed to point back at A, or neither record is identifiable as the current
        // one and the history this pointer exists to reunite becomes unreachable from either end.
        var a = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 200);
        var b = Seed("Ravi Kumar", phone: "98765 43210");

        await Build().MarkMergedAsync(
            a.Id, new MarkMergedRequest { MergedIntoPatientId = b.Id }, default);

        var thrown = await Assert.ThrowsAsync<DomainRuleException>(() =>
            Build().MarkMergedAsync(
                b.Id, new MarkMergedRequest { MergedIntoPatientId = a.Id }, default));

        thrown.RuleType.Should().Be("merge-cycle");
        b.MergedIntoPatientId.Should().BeNull();
        b.Status.Should().Be(PatientStatus.Active);
    }

    [Fact]
    public async Task An_indirect_cycle_through_a_third_record_is_rejected()
    {
        // A -> B -> C already exists; C must not point back at A. A single-step check would miss
        // this, which is why the guard walks the chain rather than comparing two ids.
        var a = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 300);
        var b = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 200);
        var c = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 100);

        var service = Build();

        await service.MarkMergedAsync(
            a.Id, new MarkMergedRequest { MergedIntoPatientId = b.Id }, default);
        await service.MarkMergedAsync(
            b.Id, new MarkMergedRequest { MergedIntoPatientId = c.Id }, default);

        var thrown = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.MarkMergedAsync(
                c.Id, new MarkMergedRequest { MergedIntoPatientId = a.Id }, default));

        thrown.RuleType.Should().Be("merge-cycle");
        c.MergedIntoPatientId.Should().BeNull();
    }

    [Fact]
    public async Task A_chain_that_does_not_loop_is_allowed()
    {
        // The guard rejects cycles, not chains. A -> B -> C is a legitimate history of two separate
        // decisions and must keep working.
        var a = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 300);
        var b = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 200);
        var c = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 100);

        var service = Build();

        await service.MarkMergedAsync(
            a.Id, new MarkMergedRequest { MergedIntoPatientId = b.Id }, default);
        await service.MarkMergedAsync(
            b.Id, new MarkMergedRequest { MergedIntoPatientId = c.Id }, default);

        a.MergedIntoPatientId.Should().Be(b.Id);
        b.MergedIntoPatientId.Should().Be(c.Id);
        c.MergedIntoPatientId.Should().BeNull();
    }

    // --- mark-merged: an existing pointer is never overwritten ---------------

    [Fact]
    public async Task Re_pointing_an_already_merged_record_is_refused()
    {
        // Plan F-6 point 5: this feature rewrites nothing. Re-pointing would discard a decision the
        // physician recorded, with no trace that it ever existed - and undoing a merge belongs with
        // the Phase-2 tooling that can carry the audit trail for it.
        var first = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 300);
        var second = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 200);
        var duplicate = Seed("Ravi Kumar", phone: "98765 43210");

        var service = Build();

        await service.MarkMergedAsync(
            duplicate.Id, new MarkMergedRequest { MergedIntoPatientId = first.Id }, default);

        var thrown = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.MarkMergedAsync(
                duplicate.Id, new MarkMergedRequest { MergedIntoPatientId = second.Id }, default));

        thrown.RuleType.Should().Be("already-merged");
        duplicate.MergedIntoPatientId.Should().Be(first.Id);
    }

    [Fact]
    public async Task Repeating_the_same_merge_is_accepted_rather_than_reported_as_a_conflict()
    {
        // The caller asked for a state that is already true. Answering with a 409 would make a
        // retried request after a timeout look like a broken one.
        var survivor = Seed("Ravi Kumar", phone: "98765 43210", registeredDaysAgo: 200);
        var duplicate = Seed("Ravi Kumar", phone: "98765 43210");

        var service = Build();
        var request = new MarkMergedRequest { MergedIntoPatientId = survivor.Id };

        var first = await service.MarkMergedAsync(duplicate.Id, request, default);
        var second = await service.MarkMergedAsync(duplicate.Id, request, default);

        second.Id.Should().Be(first.Id);
        second.Status.Should().Be("Inactive");
        duplicate.MergedIntoPatientId.Should().Be(survivor.Id);
    }
}
