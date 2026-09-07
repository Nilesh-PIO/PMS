using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMS.Infrastructure.Persistence;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-5 backend integration tests (plan F-5 point 6): "create then fetch; unicode name round-trip
/// (E-57)". Runs the real pipeline against a throwaway LocalDB database created from the committed
/// migrations.
/// </summary>
/// <remarks>
/// The two named cases are here. The suite also covers the things that <em>only</em> a real
/// database can prove and which a service-level test would pass without: that the
/// <c>CK_Patient_AgeShape</c> check constraint is genuinely in the shipped schema, that the
/// filtered unique index really does stop a concurrent double-submit, and that a Devanagari name
/// survives SQL Server's collation rather than merely surviving C#.
/// </remarks>
public class PatientsEndpointTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public PatientsEndpointTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Empties the Patients table between tests, so a count assertion means what it says rather
    /// than depending on the order xUnit happened to pick.
    /// </summary>
    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Patients;");
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
        string? gender = null,
        string? dateOfBirth = null,
        int? approxAgeYears = null,
        string? altContact = null,
        Guid? submissionId = null) => new
        {
            fullName,
            primaryPhone,
            gender,
            dateOfBirth,
            approxAgeYears,
            altContact,
            submissionId,
        };

    // --- auth (plan F-5 point 3: both routes are cookie-only) ---------------

    [Theory]
    [InlineData("POST", "/api/patients")]
    [InlineData("GET", "/api/patients/8a1d0f6c-0000-4000-8000-000000000000")]
    public async Task Every_patient_route_returns_401_without_a_cookie(string method, string path)
    {
        // The first endpoints in this application to serve real patient data. They are protected by
        // F-2's default-deny fallback policy rather than by an attribute anyone had to remember.
        var client = _factory.CreateHttpsClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- create then fetch (the plan's named case) --------------------------

    [Fact]
    public async Task A_registered_patient_can_be_fetched_back_with_everything_that_was_sent()
    {
        var client = await SignedInClientAsync();

        var created = await client.PostAsJsonAsync("/api/patients", ARegistration(
            fullName: "Ravi Kumar",
            primaryPhone: "+91 98765-43210",
            gender: "Male",
            dateOfBirth: "1985-03-02",
            altContact: "Sister - 98765 00000"));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull("the client's next navigation is the profile URL");

        var body = await ReadJsonAsync(created);
        var id = body.GetProperty("id").GetGuid();

        var fetched = await client.GetAsync($"/api/patients/{id}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await ReadJsonAsync(fetched);
        profile.GetProperty("fullName").GetString().Should().Be("Ravi Kumar");
        profile.GetProperty("normalizedName").GetString().Should().Be("ravi kumar");
        profile.GetProperty("primaryPhone").GetString().Should().Be("+91 98765-43210");
        profile.GetProperty("phoneTail").GetString().Should().Be("3210");
        profile.GetProperty("gender").GetString().Should().Be("Male");
        profile.GetProperty("altContact").GetString().Should().Be("Sister - 98765 00000");
        profile.GetProperty("status").GetString().Should().Be("Active");
        profile.GetProperty("isProfileIncomplete").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task The_location_header_points_at_a_url_that_actually_works()
    {
        var client = await SignedInClientAsync();
        var created = await client.PostAsJsonAsync("/api/patients", ARegistration());

        var followed = await client.GetAsync(created.Headers.Location);

        followed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unknown_patient_id_is_a_404_problem_document_not_an_empty_200()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync($"/api/patients/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    // --- unicode round-trip through SQL Server (E-57, the plan's named case) -

    [Theory]
    [InlineData("रवि कुमार")]
    [InlineData("李小龙")]
    [InlineData("François Müller")]
    [InlineData("Ravi Kumar")]
    public async Task A_non_latin_name_survives_the_database_unchanged(string name)
    {
        // Acceptance criterion 5. Asserted through a real SQL Server column, because this is
        // precisely where an nvarchar/varchar mistake would surface and a C#-only test would not.
        var client = await SignedInClientAsync();

        var created = await client.PostAsJsonAsync("/api/patients", ARegistration(fullName: name));
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var profile = await ReadJsonAsync(await client.GetAsync($"/api/patients/{id}"));

        profile.GetProperty("fullName").GetString().Should().Be(name);
    }

    // --- the incomplete profile (acceptance criterion 1; E-8, E-13, E-20) ---

    [Fact]
    public async Task A_name_only_registration_succeeds_and_reports_exactly_what_is_missing()
    {
        var client = await SignedInClientAsync();

        var created = await client.PostAsJsonAsync("/api/patients", ARegistration(fullName: "Meera"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var id = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();
        var profile = await ReadJsonAsync(await client.GetAsync($"/api/patients/{id}"));

        profile.GetProperty("isProfileIncomplete").GetBoolean().Should().BeTrue();
        profile.GetProperty("primaryPhone").ValueKind.Should().Be(JsonValueKind.Null);
        profile.GetProperty("ageDisplay").GetString().Should().Be("Age not recorded");
        profile.GetProperty("missingFields").EnumerateArray()
            .Select(e => e.GetString())
            .Should().BeEquivalentTo(["phone", "age", "gender"]);
    }

    // --- the age rule at the API (acceptance criteria 2 and 3) --------------

    [Fact]
    public async Task An_approximate_age_is_stored_with_the_date_it_was_taken()
    {
        var client = await SignedInClientAsync();

        var created = await client.PostAsJsonAsync("/api/patients", ARegistration(approxAgeYears: 40));
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var profile = await ReadJsonAsync(await client.GetAsync($"/api/patients/{id}"));

        profile.GetProperty("approxAgeYears").GetInt32().Should().Be(40);
        profile.GetProperty("ageRecordedOn").ValueKind.Should().NotBe(JsonValueKind.Null);
        profile.GetProperty("ageDisplay").GetString().Should().StartWith("~40 (recorded ");
    }

    [Fact]
    public async Task A_future_date_of_birth_is_a_400_with_a_field_keyed_error()
    {
        var client = await SignedInClientAsync();
        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

        var response = await client.PostAsJsonAsync(
            "/api/patients", ARegistration(dateOfBirth: tomorrow));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await ReadJsonAsync(response);
        problem.GetProperty("errors").TryGetProperty("DateOfBirth", out _).Should().BeTrue();
    }

    [Fact]
    public async Task A_date_of_birth_of_today_is_accepted_and_reads_in_days()
    {
        var client = await SignedInClientAsync();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var created = await client.PostAsJsonAsync("/api/patients", ARegistration(dateOfBirth: today));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(created)).GetProperty("ageDisplay").GetString()
            .Should().EndWith("days");
    }

    /// <summary>
    /// The E-9 rule as the <em>database</em> enforces it, not as the service does.
    /// </summary>
    /// <remarks>
    /// This test bypasses <c>PatientService</c> entirely and inserts straight through the DbContext,
    /// because the point is to prove <c>CK_Patient_AgeShape</c> is really in the shipped schema. A
    /// service-level test passes whether or not the migration created the constraint, and the whole
    /// reason the rule is stated twice is that F-5 will not be the last writer of this table.
    /// </remarks>
    [Fact]
    public async Task The_database_itself_refuses_an_age_with_no_recorded_on_date()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        var act = async () => await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Patients
                (Id, FullName, NormalizedName, ApproxAgeYears, AgeRecordedOn, RegisteredUtc, Status)
            VALUES
                (NEWID(), 'Bare Age', 'bare age', 40, NULL, SYSDATETIMEOFFSET(), 1);
            """);

        // Raw SQL surfaces the provider's own exception rather than EF's DbUpdateException, which
        // is what makes the constraint name assertable - and the name is the point: it proves this
        // specific constraint refused the row, not that the insert failed for some other reason.
        (await act.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>())
            .Which.Message.Should().Contain("CK_Patient_AgeShape");
    }

    [Fact]
    public async Task The_database_itself_refuses_a_date_of_birth_and_an_approximate_age_together()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        var act = async () => await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Patients
                (Id, FullName, NormalizedName, DateOfBirth, ApproxAgeYears, AgeRecordedOn, RegisteredUtc, Status)
            VALUES
                (NEWID(), 'Both', 'both', '1985-03-02', 40, '2026-09-07', SYSDATETIMEOFFSET(), 1);
            """);

        await act.Should().ThrowAsync<Exception>();
    }

    // --- gender comes from F-4's list (C-20) --------------------------------

    [Fact]
    public async Task A_gender_that_is_not_on_the_clinics_list_is_rejected_with_400()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/patients", ARegistration(gender: "M"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("errors")
            .TryGetProperty("Gender", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("Female")]
    [InlineData("Male")]
    [InlineData("Other")]
    [InlineData("Not stated")]
    public async Task Every_seeded_gender_option_is_accepted(string gender)
    {
        // Ties F-5 to F-4's actual migration seed: if that list changes, this fails rather than
        // drifting quietly.
        var client = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/patients", ARegistration(gender: gender));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- create idempotency (acceptance criterion 6; E-43, E-46) ------------

    [Fact]
    public async Task Posting_the_same_submission_token_twice_creates_exactly_one_patient()
    {
        var client = await SignedInClientAsync();
        var token = Guid.NewGuid();
        var payload = ARegistration(submissionId: token);

        var first = await client.PostAsJsonAsync("/api/patients", payload);
        var second = await client.PostAsJsonAsync("/api/patients", payload);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstId = (await ReadJsonAsync(first)).GetProperty("id").GetGuid();
        var secondId = (await ReadJsonAsync(second)).GetProperty("id").GetGuid();
        secondId.Should().Be(firstId, "a replayed submit reports the registration that happened");

        (await CountPatientsAsync()).Should().Be(1);
    }

    /// <summary>
    /// The double-click as it actually happens: both requests in flight at once.
    /// </summary>
    /// <remarks>
    /// The sequential test above is satisfied by the service's read-before-write. This one is not -
    /// both requests can read nothing and both attempt an insert, so the only thing that can decide
    /// is the filtered unique index. That is the difference between the guarantee being real and
    /// being a narrow race, and it is why the index exists.
    /// </remarks>
    [Fact]
    public async Task Two_simultaneous_posts_of_one_submission_token_still_create_exactly_one_patient()
    {
        var client = await SignedInClientAsync();
        var token = Guid.NewGuid();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ =>
                client.PostAsJsonAsync("/api/patients", ARegistration(submissionId: token))));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);

        var ids = new List<Guid>();
        foreach (var response in responses)
        {
            ids.Add((await ReadJsonAsync(response)).GetProperty("id").GetGuid());
        }

        ids.Distinct().Should().ContainSingle("every caller must be told about the same patient");
        (await CountPatientsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Two_registrations_without_a_token_are_both_created()
    {
        // The filtered index must not treat two nulls as a collision - a plain unique index would
        // allow exactly one patient without a token in the entire table.
        var client = await SignedInClientAsync();

        await client.PostAsJsonAsync("/api/patients", ARegistration(fullName: "Ravi Kumar"));
        await client.PostAsJsonAsync("/api/patients", ARegistration(fullName: "Meera Shah"));

        (await CountPatientsAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Two_different_tokens_for_similar_details_both_create_a_patient()
    {
        // Detecting that these two look like the same person is F-6's job, as a warning and never
        // as a block. F-5 must not silently swallow the second registration.
        var client = await SignedInClientAsync();

        await client.PostAsJsonAsync("/api/patients", ARegistration(submissionId: Guid.NewGuid()));
        await client.PostAsJsonAsync("/api/patients", ARegistration(submissionId: Guid.NewGuid()));

        (await CountPatientsAsync()).Should().Be(2);
    }

    // --- normalisation reaches the database (acceptance criterion 4) --------

    [Fact]
    public async Task Whitespace_variants_persist_the_same_normalized_name()
    {
        var client = await SignedInClientAsync();

        var a = await client.PostAsJsonAsync("/api/patients", ARegistration(fullName: "  Ravi   Kumar  "));
        var b = await client.PostAsJsonAsync("/api/patients", ARegistration(fullName: "Ravi Kumar"));

        var idA = (await ReadJsonAsync(a)).GetProperty("id").GetGuid();
        var idB = (await ReadJsonAsync(b)).GetProperty("id").GetGuid();

        var profileA = await ReadJsonAsync(await client.GetAsync($"/api/patients/{idA}"));
        var profileB = await ReadJsonAsync(await client.GetAsync($"/api/patients/{idB}"));

        profileA.GetProperty("normalizedName").GetString().Should().Be("ravi kumar");
        profileB.GetProperty("normalizedName").GetString().Should().Be("ravi kumar");
        profileA.GetProperty("fullName").GetString().Should().Be("Ravi Kumar");
    }

    [Fact]
    public async Task A_client_cannot_set_a_server_owned_field_by_sending_it()
    {
        // The DTO has no property for these, so they are ignored rather than honoured. Asserted
        // because "the model binder drops unknown properties" is a behaviour worth pinning on the
        // first endpoint that accepts patient data.
        var client = await SignedInClientAsync();

        var created = await client.PostAsJsonAsync("/api/patients", new
        {
            fullName = "Ravi Kumar",
            status = "Inactive",
            normalizedName = "hijacked",
            mergedIntoPatientId = Guid.NewGuid(),
            registeredUtc = "1999-01-01T00:00:00Z",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();
        var profile = await ReadJsonAsync(await client.GetAsync($"/api/patients/{id}"));

        profile.GetProperty("status").GetString().Should().Be("Active");
        profile.GetProperty("normalizedName").GetString().Should().Be("ravi kumar");
        profile.GetProperty("mergedIntoPatientId").ValueKind.Should().Be(JsonValueKind.Null);
        profile.GetProperty("registeredUtc").GetDateTimeOffset().Year.Should().BeGreaterThan(2020);
    }

    private async Task<int> CountPatientsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        return await db.Patients.CountAsync();
    }
}

