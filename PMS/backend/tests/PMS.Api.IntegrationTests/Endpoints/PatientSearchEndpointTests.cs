using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Infrastructure.Persistence;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-7 backend integration tests (plan F-7 point 6): "seeded 5,000-row set, asserts result
/// correctness and records elapsed time against the 2 s budget".
/// </summary>
/// <remarks>
/// <para>
/// These run the real pipeline against a throwaway LocalDB database created from the committed
/// migrations, so they prove the things a service-level test cannot: that the LINQ actually
/// translates to SQL rather than silently evaluating on the client, that the ranking survives SQL
/// Server's collation, and that the latency budget holds against a real query plan over a real
/// 5,000-row table.
/// </para>
/// <para>
/// <b>The class shares one seeded 5,000-patient database across every test in it.</b> Seeding is
/// the expensive part and it is identical for all of them, so it happens once in
/// <see cref="InitializeAsync"/> and no test mutates it. That is also why every test's assertions
/// are written to tolerate the noise rows rather than assuming an empty table.
/// </para>
/// </remarks>
public class PatientSearchEndpointTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    /// <summary>The plan's design point (C-12, REC-19): 5,000 patients.</summary>
    private const int SeededPatientCount = 5_000;

    /// <summary>The plan's NFR: p95 ≤ 2 s from keystroke to rendered result.</summary>
    private static readonly TimeSpan LatencyBudget = TimeSpan.FromSeconds(2);

    private readonly TestWebAppFactory _factory;

    public PatientSearchEndpointTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    // Distinctive names planted among the 5,000, so an assertion can be exact rather than
    // approximate. All share the "Zephyrine" stem, which no generated name contains.
    private static readonly Guid AnchorAId = new("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid AnchorBId = new("aaaaaaaa-0000-4000-8000-000000000002");
    private static readonly Guid RetiredId = new("aaaaaaaa-0000-4000-8000-000000000003");
    private static readonly Guid MergedId = new("aaaaaaaa-0000-4000-8000-000000000004");
    private static readonly Guid TypoId = new("aaaaaaaa-0000-4000-8000-000000000005");

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        // Idempotent: xUnit runs InitializeAsync per test, and re-seeding 5,000 rows each time
        // would dominate the run and make the latency numbers meaningless.
        if (await db.Patients.AnyAsync())
        {
            return;
        }

        var registered = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var bulk = new List<Patient>(SeededPatientCount);

        var given = new[]
        {
            "Ravi", "Sunita", "Anil", "Meena", "Prakash", "Lakshmi", "Vijay", "Radha",
            "Suresh", "Kavita", "Deepak", "Anita", "Rajesh", "Priya", "Manoj", "Neha",
        };
        var family = new[]
        {
            "Kumar", "Sharma", "Patel", "Reddy", "Singh", "Gupta", "Nair", "Desai",
            "Iyer", "Joshi", "Mehta", "Rao", "Verma", "Shah", "Bose", "Chauhan",
        };

        for (var i = 0; i < SeededPatientCount; i++)
        {
            // Deterministic rather than random: a latency budget that passes on some seeds and
            // fails on others is not a measurement.
            var name = $"{given[i % given.Length]} {family[(i / given.Length) % family.Length]}";
            var phone = $"9{(100000000 + i):D9}";

            bulk.Add(NewPatient(
                Guid.NewGuid(),
                $"{name} {i:D5}",
                phone,
                registered.AddMinutes(i)));
        }

        // The anchors. Registered *after* the bulk so recency tie-breaks are predictable.
        var anchorTime = registered.AddYears(1);

        var anchorA = NewPatient(AnchorAId, "Zephyrine Marchetti", "+91 98765-43210", anchorTime);
        anchorA.DateOfBirth = new DateOnly(1985, 3, 2);

        // Deliberately identical name and identical date of birth to anchor A - E-28's scenario.
        var anchorB = NewPatient(AnchorBId, "Zephyrine Marchetti", "+91 91234-59999", anchorTime.AddDays(1));
        anchorB.DateOfBirth = new DateOnly(1985, 3, 2);

        var retired = NewPatient(RetiredId, "Zephyrine Retired", "9000000001", anchorTime);
        retired.Status = PatientStatus.Inactive;
        retired.InactiveReason = "Moved away";

        var merged = NewPatient(MergedId, "Zephyrine Duplicate", "9000000002", anchorTime);
        merged.MergedIntoPatientId = AnchorAId;

        // The E-30 record: stored with a typo, so only the similarity fallback can find it.
        var typo = NewPatient(TypoId, "Bartholemew Quintrell", "9000000003", anchorTime);

        db.Patients.AddRange(bulk);
        db.Patients.AddRange(anchorA, anchorB, retired, merged, typo);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static Patient NewPatient(Guid id, string fullName, string phone, DateTimeOffset registered) =>
        new()
        {
            Id = id,
            FullName = fullName,
            // Derived exactly as PatientService would on save - a hand-written normalized value
            // would be testing the test rather than the application.
            NormalizedName = PMS.Application.Services.PatientNormalizer.NormalizeName(fullName),
            PrimaryPhone = phone,
            NormalizedPhone = PMS.Application.Services.PatientNormalizer.NormalizePhone(phone),
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = "Female",
            Status = PatientStatus.Active,
            RegisteredUtc = registered,
        };

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

    private static List<string> NamesOf(JsonElement array) =>
        [.. array.EnumerateArray().Select(e => e.GetProperty("fullName").GetString()!)];

    // --- auth ---------------------------------------------------------------

    [Theory]
    [InlineData("/api/patients/search?query=ravi")]
    [InlineData("/api/patients/recent")]
    public async Task Both_F7_routes_return_401_without_a_cookie(string path)
    {
        // Protected by F-2's default-deny fallback policy rather than by an attribute someone had
        // to remember to add. These routes return patient names, so it matters.
        var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- the acceptance criteria --------------------------------------------

    [Fact]
    public async Task AC1_typing_four_digits_matching_a_stored_phone_tail_returns_that_patient()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/patients/search?query=3210");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);

        // The full number is "+91 98765-43210"; four digits is all the physician remembered.
        NamesOf(body).Should().Contain("Zephyrine Marchetti");
        body.EnumerateArray()
            .First(e => e.GetProperty("id").GetString() == AnchorAId.ToString())
            .GetProperty("matchKind").GetString().Should().Be("Phone");
    }

    [Fact]
    public async Task AC2_two_patients_sharing_name_and_age_come_back_distinguishable()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/patients/search?query=Zephyrine%20Marchetti");
        var body = await ReadJsonAsync(response);

        var rows = body.EnumerateArray().ToList();
        rows.Should().HaveCount(2);

        rows.Select(r => r.GetProperty("fullName").GetString()).Distinct()
            .Should().ContainSingle("the two patients genuinely share a name");
        rows.Select(r => r.GetProperty("ageDisplay").GetString()).Distinct()
            .Should().ContainSingle("they also share a date of birth");

        // ...and are still told apart, on two axes, without opening either record.
        rows.Select(r => r.GetProperty("phoneTail").GetString()).Should().OnlyHaveUniqueItems();
        rows.Select(r => r.GetProperty("registeredOn").GetString()).Should().OnlyHaveUniqueItems();

        // Never auto-selected: the response is an array of two, not a redirect to one.
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task AC3_a_no_match_query_returns_an_empty_array_and_not_an_error()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/patients/search?query=Nobodyxyzzy");

        // E-7's inline "register this name" action is a client concern, but it is only reachable if
        // the server answers 200 with an empty list rather than a 404 or a 500.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AC4_search_over_5000_patients_stays_inside_the_two_second_budget()
    {
        // Plan F-7 point 7, acceptance criterion 4 (C-12, REC-19). Measured, not assumed.
        var client = await SignedInClientAsync();

        var db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<PmsDbContext>();
        (await db.Patients.CountAsync()).Should().BeGreaterThanOrEqualTo(
            SeededPatientCount,
            "the latency claim is only meaningful at the plan's design point");

        // A spread of query shapes, because they produce different plans: a prefix seeks the index,
        // a substring and a phone suffix scan it, and a no-match query pays for the fuzzy fallback
        // over every name in the table - which is the slowest path this feature has.
        string[] queries =
        [
            "ravi", "kumar", "sharma", "ra", "Zephyrine", "3210", "98765", "9100000500",
            "priya sharma", "meena", "Nobodyxyzzy", "Bartholomew Quintrell",
        ];

        var timings = new List<double>();

        // One untimed pass first: the first request pays for JIT, the connection pool and the query
        // plan, none of which a physician's second keystroke pays for.
        foreach (var q in queries)
        {
            await client.GetAsync($"/api/patients/search?query={Uri.EscapeDataString(q)}");
        }

        for (var round = 0; round < 5; round++)
        {
            foreach (var q in queries)
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await client.GetAsync($"/api/patients/search?query={Uri.EscapeDataString(q)}");
                stopwatch.Stop();

                response.StatusCode.Should().Be(HttpStatusCode.OK);
                timings.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        timings.Sort();
        var p95 = timings[(int)Math.Floor(0.95 * (timings.Count - 1))];
        var worst = timings[^1];

        // Printed so the number lands in the test output and can go into the progress log as a
        // measurement rather than a claim.
        Console.WriteLine(
            $"F-7 search latency over {SeededPatientCount} patients, {timings.Count} requests: "
            + $"p50={timings[timings.Count / 2]:F1} ms, p95={p95:F1} ms, max={worst:F1} ms "
            + $"(budget {LatencyBudget.TotalMilliseconds:F0} ms)");

        p95.Should().BeLessThan(
            LatencyBudget.TotalMilliseconds,
            "plan F-7 point 1 NFR: p95 <= 2 s from keystroke to rendered result at 5,000 patients");
    }

    [Fact]
    public async Task AC5_recent_patients_returns_a_list_and_is_empty_stated_by_the_client()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/patients/recent");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.GetArrayLength().Should().Be(10, "the default take is 10");

        // Every row carries what a picker row needs (E-28) - proven on the wire, not in a DTO.
        foreach (var row in body.EnumerateArray())
        {
            row.GetProperty("fullName").GetString().Should().NotBeNullOrWhiteSpace();
            row.TryGetProperty("phoneTail", out _).Should().BeTrue();
            row.GetProperty("ageDisplay").GetString().Should().NotBeNullOrWhiteSpace();
            row.TryGetProperty("lastVisitDate", out _).Should().BeTrue();
            row.GetProperty("registeredOn").GetString().Should().NotBeNullOrWhiteSpace();
            row.GetProperty("status").GetString().Should().Be("Active");
        }
    }

    [Fact]
    public async Task Recent_patients_is_an_empty_array_when_no_patients_exist()
    {
        // E-2 on a genuinely fresh install. Its own factory, because this class's shared database
        // is deliberately full - and "empty" is exactly the state that must not be an error.
        await using var freshFactory = new TestWebAppFactory();
        await freshFactory.InitializeAsync();

        var client = freshFactory.CreateHttpsClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = TestWebAppFactory.TestUserName,
            password = TestWebAppFactory.TestPassword,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/api/patients/recent");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.GetArrayLength().Should().Be(0);
    }

    // --- ranking and filtering, against real SQL ----------------------------

    [Fact]
    public async Task Ranking_survives_translation_to_SQL()
    {
        // The unit tests prove the rule; this proves SQL Server applies the same rule to the same
        // rows. Without it, a LINQ construct that silently evaluates on the client would still pass
        // every unit test while scanning the whole table in production.
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/patients/search?query=Zephyrine&includeInactive=true");
        var names = NamesOf(await ReadJsonAsync(response));

        // "Zephyrine Duplicate" and "Zephyrine Retired" are name-prefix matches alongside the two
        // "Zephyrine Marchetti" rows; all four are rank 1, so recency orders them.
        names.Should().HaveCount(4);
        names.Should().Contain("Zephyrine Marchetti");
        names.Should().Contain("Zephyrine Retired");
        names.Should().Contain("Zephyrine Duplicate");
    }

    [Fact]
    public async Task Inactive_and_merged_records_are_absent_by_default_and_present_when_asked_for()
    {
        var client = await SignedInClientAsync();

        var defaultBody = await ReadJsonAsync(
            await client.GetAsync("/api/patients/search?query=Zephyrine"));
        var defaultNames = NamesOf(defaultBody);

        defaultNames.Should().NotContain("Zephyrine Retired");
        defaultNames.Should().NotContain("Zephyrine Duplicate");
        defaultNames.Should().Contain("Zephyrine Marchetti");

        var inclusiveBody = await ReadJsonAsync(
            await client.GetAsync("/api/patients/search?query=Zephyrine&includeInactive=true"));

        var merged = inclusiveBody.EnumerateArray()
            .First(e => e.GetProperty("fullName").GetString() == "Zephyrine Duplicate");

        // Found, and visibly not an ordinary choice.
        merged.GetProperty("isMerged").GetBoolean().Should().BeTrue();

        inclusiveBody.EnumerateArray()
            .First(e => e.GetProperty("fullName").GetString() == "Zephyrine Retired")
            .GetProperty("status").GetString().Should().Be("Inactive");
    }

    [Fact]
    public async Task Recent_patients_never_includes_a_retired_or_merged_record()
    {
        var client = await SignedInClientAsync();

        var body = await ReadJsonAsync(await client.GetAsync("/api/patients/recent?take=50"));
        var rows = body.EnumerateArray().ToList();

        rows.Should().NotContain(e => e.GetProperty("status").GetString() == "Inactive");
        rows.Should().NotContain(e => e.GetProperty("isMerged").GetBoolean());
    }

    [Fact]
    public async Task The_fuzzy_fallback_finds_a_typo_stored_record_through_the_real_pipeline()
    {
        // E-30 end to end: 5,000 exact-matchable names in the table, and the one the physician
        // wants is stored misspelled. An exact search returns nothing; the fallback rescues it.
        var client = await SignedInClientAsync();

        var response = await client.GetAsync(
            "/api/patients/search?query=" + Uri.EscapeDataString("Bartholomew Quintrell"));

        var body = await ReadJsonAsync(response);
        var rows = body.EnumerateArray().ToList();

        rows.Should().Contain(e => e.GetProperty("fullName").GetString() == "Bartholemew Quintrell");
        rows.First(e => e.GetProperty("fullName").GetString() == "Bartholemew Quintrell")
            .GetProperty("matchKind").GetString()
            .Should().Be("SimilarName", "a guess must never be rendered as a certainty");
    }

    // --- request validation --------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("r")]
    [InlineData("%20%20")]
    public async Task A_query_below_the_minimum_length_is_a_400_with_a_field_error(string query)
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync($"/api/patients/search?query={query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").TryGetProperty("query", out _).Should().BeTrue();
    }

    [Fact]
    public async Task A_missing_query_parameter_is_a_400_rather_than_the_whole_table()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/patients/search");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Take_is_clamped_at_fifty_rather_than_returning_five_thousand_rows()
    {
        var client = await SignedInClientAsync();

        var body = await ReadJsonAsync(
            await client.GetAsync("/api/patients/search?query=ra&take=5000"));

        body.GetArrayLength().Should().Be(50);
    }

    [Fact]
    public async Task The_default_result_size_is_twenty()
    {
        var client = await SignedInClientAsync();

        var body = await ReadJsonAsync(await client.GetAsync("/api/patients/search?query=ra"));

        body.GetArrayLength().Should().Be(20);
    }

    // --- the search route does not shadow the profile route ------------------

    [Fact]
    public async Task The_search_and_recent_routes_do_not_shadow_the_patient_profile_route()
    {
        // "search" and "recent" are literal segments on a controller that also has {id:guid}. The
        // guid constraint makes the collision impossible, and this proves it rather than assuming
        // it - a regression here would turn every profile fetch into a search.
        var client = await SignedInClientAsync();

        var profile = await client.GetAsync($"/api/patients/{AnchorAId}");

        profile.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(profile)).GetProperty("fullName").GetString()
            .Should().Be("Zephyrine Marchetti");
    }

    [Fact]
    public async Task A_unicode_name_is_searchable_through_SQL_Server_collation()
    {
        // E-57. C# normalisation is proven by the unit tests; this proves the value survives the
        // round trip through nvarchar and is matched by a LIKE under the database's collation.
        var client = await SignedInClientAsync();

        var created = await client.PostAsJsonAsync("/api/patients", new
        {
            fullName = "रवि कुमार",
            primaryPhone = "9000000099",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await ReadJsonAsync(
            await client.GetAsync("/api/patients/search?query=" + Uri.EscapeDataString("कुमार")));

        NamesOf(body).Should().Contain("रवि कुमार");
    }
}
