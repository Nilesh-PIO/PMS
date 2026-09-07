using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Services;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Infrastructure.Migrations;
using PMS.Infrastructure.Persistence;
using PMS.Infrastructure.Persistence.Configurations;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-6 backend integration tests (plan F-6 point 6): "409 without confirm, 201 with confirm,
/// <c>mark-merged</c> rejects a cycle". Runs the real pipeline against a throwaway LocalDB database
/// created from the committed migrations.
/// </summary>
/// <remarks>
/// The three named cases are here. The suite also covers what only a real database can prove and a
/// service-level test would pass without noticing: that the <c>PhoneMatchKey</c> column and its
/// index are genuinely in the shipped schema, that the migration's T-SQL backfill produces the same
/// key as the C# it restates, and that <c>mark-merged</c> leaves both rows on disk.
/// </remarks>
public class PatientDuplicateEndpointTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public PatientDuplicateEndpointTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        // MergedIntoPatientId is a self-referencing FK with DeleteBehavior.Restrict, so a blanket
        // DELETE fails while any pointer is still set. Clearing the pointers first is not a
        // convenience - it is the schema correctly refusing to let a delete cascade through a
        // duplicate pointer (E-26, E-33).
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Patients SET MergedIntoPatientId = NULL; DELETE FROM Patients;");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = _factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = TestWebAppFactory.TestUserName,
            password = TestWebAppFactory.TestPassword,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the tests below need a real session");
        return client;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    private static object ARegistration(
        string fullName = "Ravi Kumar",
        string? primaryPhone = null,
        string? dateOfBirth = null) => new { fullName, primaryPhone, dateOfBirth };

    /// <summary>Registers a patient, confirming past any duplicate warning, and returns the id.</summary>
    private static async Task<Guid> RegisterAsync(
        HttpClient client,
        string fullName,
        string? phone = null,
        string? dateOfBirth = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/patients?confirmDuplicate=true",
            ARegistration(fullName, phone, dateOfBirth));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<int> PatientCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        return await db.Patients.CountAsync();
    }

    // --- auth ----------------------------------------------------------------

    [Theory]
    [InlineData("/api/patients/duplicate-check")]
    [InlineData("/api/patients/8a1d0f6c-0000-4000-8000-000000000000/mark-merged")]
    public async Task Every_f6_route_returns_401_without_a_cookie(string path)
    {
        // Both routes read or write patient data and are protected by F-2's default-deny fallback
        // policy rather than by an attribute anyone had to remember to add.
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync(path, new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- 409 without confirm (acceptance criterion 1) ------------------------

    [Fact]
    public async Task Registering_a_name_and_phone_already_on_file_returns_409_before_any_row_is_written()
    {
        // Acceptance criterion 1, and the "before any row is written" half is the part worth
        // proving: a check that fired after the insert would be a duplicate report, and the split
        // history would already exist by the time anyone read the warning.
        var client = await SignedInClientAsync();

        await RegisterAsync(client, "Ravi Kumar", "98765 43210");
        (await PatientCountAsync()).Should().Be(1);

        var second = await client.PostAsJsonAsync(
            "/api/patients", ARegistration("Ravi Kumar", "98765 43210"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PatientCountAsync()).Should().Be(1, "the 409 must precede the insert");
    }

    [Fact]
    public async Task The_409_body_carries_the_candidates_and_a_machine_readable_rule_type()
    {
        // The refusal and everything needed to answer it arrive in one response. A client that had
        // to fetch the candidates separately could fail halfway through asking the question.
        var client = await SignedInClientAsync();

        var existingId = await RegisterAsync(client, "Ravi Kumar", "98765 43210", "1985-03-02");

        var second = await client.PostAsJsonAsync(
            "/api/patients", ARegistration("Ravi Kumar", "98765 43210", "1985-03-02"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await ReadJsonAsync(second);
        body.GetProperty("ruleType").GetString().Should().Be("duplicate-confirmation-required");

        var candidates = body.GetProperty("candidates").EnumerateArray().ToList();
        candidates.Should().ContainSingle();
        candidates[0].GetProperty("id").GetGuid().Should().Be(existingId);
        candidates[0].GetProperty("isLikelyDuplicate").GetBoolean().Should().BeTrue();
        candidates[0].GetProperty("matchReason").GetString().Should().Be("phone-and-date-of-birth");
    }

    [Fact]
    public async Task Every_candidate_on_the_409_carries_the_four_disambiguating_fields()
    {
        // Acceptance criterion 4 over the wire (REC-12, E-28). The physician is choosing between
        // near-identical names, so a row that showed only a name would be the wrong-patient path.
        var client = await SignedInClientAsync();

        await RegisterAsync(client, "Ravi Kumar", "98765 43210", "1985-03-02");

        var second = await client.PostAsJsonAsync(
            "/api/patients", ARegistration("Ravi Kumar", "98765 43210"));

        var candidate = (await ReadJsonAsync(second))
            .GetProperty("candidates").EnumerateArray().Single();

        candidate.GetProperty("fullName").GetString().Should().Be("Ravi Kumar");
        candidate.GetProperty("phoneTail").GetString().Should().Be("3210");
        candidate.GetProperty("ageDisplay").GetString().Should().NotBeNullOrWhiteSpace();
        candidate.GetProperty("dateOfBirth").GetString().Should().Be("1985-03-02");
        candidate.TryGetProperty("lastVisitDate", out var lastVisit).Should().BeTrue();
        lastVisit.ValueKind.Should().Be(JsonValueKind.Null, "no Visit entity exists until F-10");

        // The full number never leaves the server on a candidate row - only the tail.
        candidate.TryGetProperty("primaryPhone", out _).Should().BeFalse();
    }

    // --- 201 with confirm (acceptance criterion 2) ---------------------------

    [Fact]
    public async Task Confirming_the_warning_creates_the_patient()
    {
        // Acceptance criterion 2. The check warns and never blocks: a blocking rule turns a false
        // positive into a patient who cannot be registered, and the desk's answer to that is to
        // invent a spelling - producing a duplicate that is also unsearchable (REC-2).
        var client = await SignedInClientAsync();

        await RegisterAsync(client, "Ravi Kumar", "98765 43210");

        var refused = await client.PostAsJsonAsync(
            "/api/patients", ARegistration("Ravi Kumar", "98765 43210"));
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var confirmed = await client.PostAsJsonAsync(
            "/api/patients?confirmDuplicate=true", ARegistration("Ravi Kumar", "98765 43210"));

        confirmed.StatusCode.Should().Be(HttpStatusCode.Created);
        (await PatientCountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task The_duplicate_check_is_on_by_omission()
    {
        // A caller cannot skip the check by forgetting about it - the query parameter defaults to
        // false, so the safe behaviour is the one you get by not thinking about it.
        var client = await SignedInClientAsync();

        await RegisterAsync(client, "Ravi Kumar", "98765 43210");

        var withoutParameter = await client.PostAsJsonAsync(
            "/api/patients", ARegistration("Ravi Kumar", "98765 43210"));
        var withFalse = await client.PostAsJsonAsync(
            "/api/patients?confirmDuplicate=false", ARegistration("Ravi Kumar", "98765 43210"));

        withoutParameter.StatusCode.Should().Be(HttpStatusCode.Conflict);
        withFalse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_unrelated_patient_registers_without_any_warning()
    {
        // The check has to be quiet in the ordinary case or it stops being read.
        var client = await SignedInClientAsync();

        await RegisterAsync(client, "Ravi Kumar", "98765 43210");

        var other = await client.PostAsJsonAsync(
            "/api/patients", ARegistration("Priya Menon", "22222 22222"));

        other.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- Q-13: phone equivalence end to end ---------------------------------

    [Fact]
    public async Task A_country_code_and_a_trunk_zero_are_recognised_as_one_number_through_the_api()
    {
        // The gap verification-pms reported against F-5, closed and proven through the real
        // pipeline and a real database rather than only in the matcher's unit tests. Without this,
        // the same person typed with "+91" one week and "0" the next becomes two patient records
        // with two half-histories and no warning to anyone (E-25).
        var client = await SignedInClientAsync();

        await RegisterAsync(client, "Ravi Kumar", "+91 98765 43210");

        var second = await client.PostAsJsonAsync(
            "/api/patients", ARegistration("Ravi Kumar", "098765 43210"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PatientCountAsync()).Should().Be(1);
    }

    // --- the duplicate-check endpoint ---------------------------------------

    [Fact]
    public async Task The_check_endpoint_answers_200_with_an_empty_list_when_nothing_matches()
    {
        // "No duplicates" is the ordinary answer, so it must not look like a failure.
        var client = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/patients/duplicate-check", new
        {
            fullName = "Ravi Kumar",
            phone = "98765 43210",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(response)).EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task A_phone_shared_by_three_family_members_returns_all_three()
    {
        // Acceptance criterion 3 and E-27: a phone number is a household identifier, not a person
        // identifier. All three are returned; only the one that meets the identity rule is flagged,
        // and none is auto-selected.
        var client = await SignedInClientAsync();

        await RegisterAsync(client, "Ravi Kumar", "98765 43210");
        await RegisterAsync(client, "Sunita Kumar", "98765 43210");
        await RegisterAsync(client, "Anil Kumar", "98765 43210");

        var response = await client.PostAsJsonAsync("/api/patients/duplicate-check", new
        {
            fullName = "Ravi Kumar",
            phone = "98765 43210",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var candidates = (await ReadJsonAsync(response)).EnumerateArray().ToList();

        candidates.Should().HaveCount(3);
        candidates.Select(c => c.GetProperty("fullName").GetString())
            .Should().BeEquivalentTo(["Ravi Kumar", "Sunita Kumar", "Anil Kumar"]);
        candidates.Count(c => c.GetProperty("isLikelyDuplicate").GetBoolean()).Should().Be(1);
    }

    [Fact]
    public async Task Registering_a_family_member_on_the_household_phone_is_not_interrupted()
    {
        // The other side of the same decision. If sharing a phone were enough to raise a warning,
        // every family member would warn about every other and the dialog would be trained away
        // within a week - at which point it is worth nothing on the day it is right.
        var client = await SignedInClientAsync();

        await RegisterAsync(client, "Ravi Kumar", "98765 43210");

        var sibling = await client.PostAsJsonAsync(
            "/api/patients", ARegistration("Sunita Kumar", "98765 43210"));

        sibling.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task The_check_endpoint_writes_nothing()
    {
        var client = await SignedInClientAsync();
        await RegisterAsync(client, "Ravi Kumar", "98765 43210");

        await client.PostAsJsonAsync("/api/patients/duplicate-check", new
        {
            fullName = "Ravi Kumar",
            phone = "98765 43210",
        });

        (await PatientCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_check_endpoint_excludes_the_patient_being_edited()
    {
        var client = await SignedInClientAsync();
        var id = await RegisterAsync(client, "Ravi Kumar", "98765 43210");

        var response = await client.PostAsJsonAsync("/api/patients/duplicate-check", new
        {
            fullName = "Ravi Kumar",
            phone = "98765 43210",
            excludePatientId = id,
        });

        (await ReadJsonAsync(response)).EnumerateArray().Should().BeEmpty();
    }

    // --- mark-merged (acceptance criteria 5 and 6) ---------------------------

    [Fact]
    public async Task Mark_merged_points_one_record_at_another_and_deletes_nothing()
    {
        // Acceptance criteria 5 and 6. Both records are still on disk afterwards and both are still
        // fetchable, which is the entire reason this is a pointer rather than a merge (E-26, E-33).
        var client = await SignedInClientAsync();

        var survivorId = await RegisterAsync(client, "Ravi Kumar", "98765 43210");
        var duplicateId = await RegisterAsync(client, "Ravi Kumar", "098765 43210");

        var marked = await client.PostAsJsonAsync(
            $"/api/patients/{duplicateId}/mark-merged",
            new { mergedIntoPatientId = survivorId, note = "Same person, registered twice." });

        marked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(marked)).GetProperty("status").GetString().Should().Be("Inactive");

        (await PatientCountAsync()).Should().Be(2, "nothing is ever deleted");

        var duplicate = await ReadJsonAsync(await client.GetAsync($"/api/patients/{duplicateId}"));
        duplicate.GetProperty("mergedIntoPatientId").GetGuid().Should().Be(survivorId);
        duplicate.GetProperty("status").GetString().Should().Be("Inactive");
        duplicate.GetProperty("inactiveReason").GetString()
            .Should().Contain("Same person, registered twice.");

        // The survivor is untouched: still active, still not pointing anywhere.
        var survivor = await ReadJsonAsync(await client.GetAsync($"/api/patients/{survivorId}"));
        survivor.GetProperty("status").GetString().Should().Be("Active");
        survivor.GetProperty("mergedIntoPatientId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Both_records_stay_readable_after_a_merge_pointer_is_written()
    {
        // E-26 in the form that matters: "both histories remain readable and neither is deleted".
        // There are no visits yet (F-10 builds them), so what is provable today is that neither
        // record becomes unreachable - which is the property every future visit query inherits.
        var client = await SignedInClientAsync();

        var survivorId = await RegisterAsync(client, "Ravi Kumar", "98765 43210");
        var duplicateId = await RegisterAsync(client, "Ravi Kumar", "098765 43210");

        await client.PostAsJsonAsync(
            $"/api/patients/{duplicateId}/mark-merged", new { mergedIntoPatientId = survivorId });

        (await client.GetAsync($"/api/patients/{survivorId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/patients/{duplicateId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Mark_merged_rejects_a_cycle()
    {
        // The plan's named case. A points at B, so B must not be allowed to point back at A: in a
        // cycle neither record is identifiable as the current one and the history the pointer exists
        // to reunite becomes unreachable from either end.
        var client = await SignedInClientAsync();

        var a = await RegisterAsync(client, "Ravi Kumar", "98765 43210");
        var b = await RegisterAsync(client, "Ravi Kumar", "098765 43210");

        var first = await client.PostAsJsonAsync(
            $"/api/patients/{a}/mark-merged", new { mergedIntoPatientId = b });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var cycle = await client.PostAsJsonAsync(
            $"/api/patients/{b}/mark-merged", new { mergedIntoPatientId = a });

        cycle.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(cycle)).GetProperty("ruleType").GetString().Should().Be("merge-cycle");

        var stillClean = await ReadJsonAsync(await client.GetAsync($"/api/patients/{b}"));
        stillClean.GetProperty("mergedIntoPatientId").ValueKind.Should().Be(JsonValueKind.Null);
        stillClean.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Mark_merged_rejects_merging_a_record_into_itself()
    {
        var client = await SignedInClientAsync();
        var id = await RegisterAsync(client, "Ravi Kumar", "98765 43210");

        var response = await client.PostAsJsonAsync(
            $"/api/patients/{id}/mark-merged", new { mergedIntoPatientId = id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mark_merged_on_an_unknown_patient_is_a_404()
    {
        var client = await SignedInClientAsync();
        var survivorId = await RegisterAsync(client, "Ravi Kumar", "98765 43210");

        var response = await client.PostAsJsonAsync(
            $"/api/patients/{Guid.NewGuid()}/mark-merged",
            new { mergedIntoPatientId = survivorId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Mark_merged_rejects_an_unknown_target_with_a_field_error()
    {
        var client = await SignedInClientAsync();
        var id = await RegisterAsync(client, "Ravi Kumar", "98765 43210");

        var response = await client.PostAsJsonAsync(
            $"/api/patients/{id}/mark-merged", new { mergedIntoPatientId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("errors")
            .TryGetProperty("MergedIntoPatientId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task There_is_no_delete_route_on_the_patients_controller()
    {
        // Acceptance criterion 6, asserted rather than assumed. The record a duplicate points at is
        // the one carrying a patient's visit history; there is no button, and no route, that removes
        // one (E-33).
        var client = await SignedInClientAsync();
        var id = await RegisterAsync(client, "Ravi Kumar", "98765 43210");

        var response = await client.DeleteAsync($"/api/patients/{id}");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await PatientCountAsync()).Should().Be(1);
    }

    // --- the shipped schema --------------------------------------------------

    [Fact]
    public async Task The_phone_match_key_column_and_its_index_are_in_the_shipped_schema()
    {
        // Proven against the database the committed migrations produce, not against the EF model -
        // a model-only assertion would pass even if the migration had been forgotten.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        var columns = await db.Database
            .SqlQuery<string>($@"
                SELECT c.name AS Value
                FROM sys.columns c
                WHERE c.object_id = OBJECT_ID('Patients') AND c.name = 'PhoneMatchKey'")
            .ToListAsync();

        columns.Should().ContainSingle();

        var indexes = await db.Database
            .SqlQuery<string>($@"
                SELECT i.name AS Value
                FROM sys.indexes i
                WHERE i.object_id = OBJECT_ID('Patients')
                  AND i.name = {PatientConfiguration.PhoneMatchKeyIndexName}")
            .ToListAsync();

        indexes.Should().ContainSingle(
            "an unindexed matching column turns every registration into a table scan");
    }

    [Fact]
    public async Task A_registered_patient_gets_its_phone_match_key_written_on_save()
    {
        var client = await SignedInClientAsync();
        var id = await RegisterAsync(client, "Ravi Kumar", "+91 98765 43210");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        var stored = await db.Patients.SingleAsync(p => p.Id == id);

        stored.PhoneMatchKey.Should().Be("9876543210");
        stored.NormalizedPhone.Should().Be("919876543210", "the faithful digits are not overwritten");
        stored.PrimaryPhone.Should().Be("+91 98765 43210", "what was typed is what is displayed");
    }

    /// <summary>
    /// The migration's T-SQL backfill and the C# it restates must produce the same key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the test that makes the backfill safe to trust. Without the backfill, every patient
    /// registered under F-5 would carry a null key and be permanently invisible to the duplicate
    /// check — the feature would look like it worked while protecting only patients registered after
    /// it shipped. With a backfill written in a second language, the risk moves to the two
    /// implementations drifting apart.
    /// </para>
    /// <para>
    /// So the statement under test is the constant the migration actually ran, not a copy of it, and
    /// the expected values come from <c>PatientNormalizer.PhoneMatchKey</c> rather than from
    /// hand-written literals.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_migrations_backfill_agrees_with_the_matching_rule_in_code()
    {
        string?[] phones =
        [
            "+91 98765 43210",
            "098765 43210",
            "9876543210",
            "00 91 98765 43210",
            "007890123",
            "2345 6789",
            "204",
            null,
        ];

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        var seeded = new List<(Guid Id, string? Phone)>();

        foreach (var phone in phones)
        {
            var id = Guid.NewGuid();

            // Written the way an F-5 row looked before this migration existed: normalized digits
            // present, matching key absent.
            db.Patients.Add(new Patient
            {
                Id = id,
                FullName = "Backfill Subject",
                NormalizedName = "backfill subject",
                PrimaryPhone = phone,
                NormalizedPhone = PatientNormalizer.NormalizePhone(phone),
                PhoneMatchKey = null,
                RegisteredUtc = DateTimeOffset.UtcNow,
                Status = PatientStatus.Active,
            });

            seeded.Add((id, phone));
        }

        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(
            AddPatientDuplicateIndexes.BackfillPhoneMatchKeySql);

        foreach (var (id, phone) in seeded)
        {
            var stored = await db.Patients.AsNoTracking().SingleAsync(p => p.Id == id);

            stored.PhoneMatchKey.Should().Be(
                PatientNormalizer.PhoneMatchKey(phone),
                "the T-SQL backfill and PatientNormalizer.PhoneMatchKey must agree for \"{0}\"",
                phone ?? "(no phone)");
        }
    }
}
