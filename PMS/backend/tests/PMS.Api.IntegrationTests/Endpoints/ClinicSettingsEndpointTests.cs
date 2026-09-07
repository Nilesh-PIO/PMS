using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMS.Domain.Enums;
using PMS.Infrastructure.Persistence;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-4 backend integration tests (plan F-4 point 6): "seed present after migration; PUT reorders
/// and deactivates". Runs the real pipeline against a throwaway LocalDB database created from the
/// committed migrations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the seed assertions live in a separate class</b> (<see cref="ClinicSettingsSeedTests"/>).
/// The tests here mutate the option lists, so they have to reset the tables between runs - and a
/// reset written by hand in the test project is not evidence that the <em>migration</em> seeded
/// anything. The seed is therefore asserted in its own class, which gets its own
/// <see cref="TestWebAppFactory"/> instance and therefore its own pristine database, and never
/// writes to these tables at all.
/// </para>
/// </remarks>
public class ClinicSettingsEndpointTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public ClinicSettingsEndpointTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Restores both tables to the state the migration leaves them in.
    /// </summary>
    /// <remarks>
    /// Raw SQL with IDENTITY_INSERT rather than EF, because the seeded rows carry the fixed ids the
    /// migration's <c>HasData</c> assigns and EF will not insert an explicit identity value.
    /// Without this reset these tests would silently depend on the order xUnit happened to run them
    /// in, and a suite that passes for that reason is worse than no suite.
    /// </remarks>
    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM VitalRangeSetting;
            DELETE FROM SettingOption;
            SET IDENTITY_INSERT SettingOption ON;
            INSERT INTO SettingOption (Id, Category, Value, DisplayOrder, IsActive) VALUES
                (1, 1, 'Female', 1, 1),
                (2, 1, 'Male', 2, 1),
                (3, 1, 'Other', 3, 1),
                (4, 1, 'Not stated', 4, 1),
                (5, 2, 'Equipment unavailable', 1, 1),
                (6, 2, 'Patient declined', 2, 1),
                (7, 2, 'Not clinically indicated', 3, 1),
                (8, 2, 'Other', 4, 1);
            SET IDENTITY_INSERT SettingOption OFF;
            """);
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

    private static object AnOptionList(params (string Value, bool IsActive)[] items) => new
    {
        items = items.Select(i => new { value = i.Value, isActive = i.IsActive }).ToArray(),
    };

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    // --- auth: every route needs the cookie (plan F-4 point 3) --------------

    [Theory]
    [InlineData("GET", "/api/clinic-settings/options?category=Gender")]
    [InlineData("PUT", "/api/clinic-settings/options/Gender")]
    [InlineData("GET", "/api/clinic-settings/vital-ranges")]
    [InlineData("PUT", "/api/clinic-settings/vital-ranges")]
    public async Task Every_clinic_settings_route_returns_401_without_a_cookie(
        string method,
        string path)
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), path));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- reading the lists --------------------------------------------------

    [Fact]
    public async Task Get_options_returns_the_category_in_display_order()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/clinic-settings/options?category=Gender");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.EnumerateArray().Select(o => o.GetProperty("value").GetString())
            .Should().Equal("Female", "Male", "Other", "Not stated");
        body[0].GetProperty("category").GetString().Should().Be("Gender",
            "categories travel as names, matching the query string that asked for them");
    }

    [Fact]
    public async Task Get_options_without_a_category_is_a_400_naming_the_ones_that_exist()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/clinic-settings/options");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("Gender");
    }

    [Fact]
    public async Task Get_options_for_an_unknown_category_is_a_400_not_an_empty_list()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/clinic-settings/options?category=Bloodtype");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- PUT reorders and deactivates (plan F-4 point 6) --------------------

    [Fact]
    public async Task Put_reorders_the_list_and_the_new_order_survives_a_reread()
    {
        var client = await SignedInClientAsync();

        var put = await client.PutAsJsonAsync(
            "/api/clinic-settings/options/Gender",
            AnOptionList(("Not stated", true), ("Female", true), ("Male", true), ("Other", true)));

        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await ReadJsonAsync(
            await client.GetAsync("/api/clinic-settings/options?category=Gender"));

        reread.EnumerateArray().Select(o => o.GetProperty("value").GetString())
            .Should().Equal("Not stated", "Female", "Male", "Other");
        reread.EnumerateArray().Select(o => o.GetProperty("displayOrder").GetInt32())
            .Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task Put_deactivates_an_omitted_option_and_the_row_is_still_in_the_table()
    {
        var client = await SignedInClientAsync();

        await client.PutAsJsonAsync(
            "/api/clinic-settings/options/Gender",
            AnOptionList(("Female", true), ("Male", true), ("Not stated", true)));

        // Gone from a dropdown...
        var forADropdown = await ReadJsonAsync(
            await client.GetAsync("/api/clinic-settings/options?category=Gender"));
        forADropdown.EnumerateArray().Select(o => o.GetProperty("value").GetString())
            .Should().NotContain("Other");

        // ...but still visible to the editor, and - the point of the rule - still in the database,
        // so a patient row recorded as "Other" still means something (plan F-4 point 5).
        var forTheEditor = await ReadJsonAsync(await client.GetAsync(
            "/api/clinic-settings/options?category=Gender&includeInactive=true"));
        forTheEditor.EnumerateArray().Select(o => o.GetProperty("value").GetString())
            .Should().Contain("Other");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        var stored = await db.SettingOptions
            .SingleAsync(o => o.Category == SettingCategory.Gender && o.Value == "Other");
        stored.IsActive.Should().BeFalse("retired, not deleted");
    }

    [Fact]
    public async Task Put_adds_a_new_option_at_the_position_it_was_submitted_in()
    {
        var client = await SignedInClientAsync();

        var put = await client.PutAsJsonAsync(
            "/api/clinic-settings/options/Gender",
            AnOptionList(
                ("Female", true), ("Male", true), ("Non-binary", true), ("Other", true),
                ("Not stated", true)));

        var body = await ReadJsonAsync(put);
        body.EnumerateArray().Select(o => o.GetProperty("value").GetString())
            .Should().Equal("Female", "Male", "Non-binary", "Other", "Not stated");
    }

    [Fact]
    public async Task Put_that_drops_Not_stated_is_a_400_and_changes_nothing()
    {
        var client = await SignedInClientAsync();

        var put = await client.PutAsJsonAsync(
            "/api/clinic-settings/options/Gender",
            AnOptionList(("Female", true), ("Male", true), ("Other", true)));

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        put.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await put.Content.ReadAsStringAsync()).Should().Contain("Not stated");

        var reread = await ReadJsonAsync(
            await client.GetAsync("/api/clinic-settings/options?category=Gender"));
        // The `because` argument goes through the IEnumerable overload deliberately: the params
        // overload would swallow it as a fifth expected value, which is how this assertion failed
        // on its first run while the endpoint was behaving correctly.
        reread.EnumerateArray().Select(o => o.GetProperty("value").GetString())
            .Should().Equal(
                new[] { "Female", "Male", "Other", "Not stated" },
                "a rejected edit must leave the list exactly as it was");
    }

    [Fact]
    public async Task Put_with_a_case_insensitive_duplicate_is_a_400_with_a_field_error()
    {
        var client = await SignedInClientAsync();

        var put = await client.PutAsJsonAsync(
            "/api/clinic-settings/options/Gender",
            AnOptionList(("Male", true), ("male", true), ("Not stated", true)));

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(put);
        body.GetProperty("errors").TryGetProperty("Items[1].Value", out _).Should().BeTrue(
            "the error is keyed to the row the physician has to fix");
    }

    [Fact]
    public async Task The_database_refuses_a_duplicate_value_even_when_the_service_is_bypassed()
    {
        // C-20 at the level that actually holds: SSMS is a stated tool of this stack, so the rule
        // cannot live only in the service. This inserts straight through the DbContext.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        db.SettingOptions.Add(new Domain.Entities.SettingOption
        {
            Category = SettingCategory.Gender,
            Value = "male",
            DisplayOrder = 99,
            IsActive = true,
        });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "the unique index is case-insensitive under the default collation");
    }

    // --- vital ranges -------------------------------------------------------

    [Fact]
    public async Task Get_vital_ranges_returns_every_metric_unconfigured()
    {
        var client = await SignedInClientAsync();

        var body = await ReadJsonAsync(await client.GetAsync("/api/clinic-settings/vital-ranges"));

        body.EnumerateArray().Select(r => r.GetProperty("metric").GetString())
            .Should().Equal("Temperature", "BloodPressureSystolic", "BloodPressureDiastolic", "PulseBpm");
        body.EnumerateArray().Should().OnlyContain(r =>
            r.GetProperty("warnLow").ValueKind == JsonValueKind.Null
            && r.GetProperty("warnHigh").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Put_then_Get_round_trips_a_threshold()
    {
        var client = await SignedInClientAsync();

        var put = await client.PutAsJsonAsync("/api/clinic-settings/vital-ranges", new
        {
            items = new[]
            {
                new { metric = "Temperature", warnLow = (decimal?)35.5m, warnHigh = (decimal?)42m },
                new { metric = "PulseBpm", warnLow = (decimal?)null, warnHigh = (decimal?)120m },
            },
        });

        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(
            await client.GetAsync("/api/clinic-settings/vital-ranges"));

        var temperature = body.EnumerateArray()
            .Single(r => r.GetProperty("metric").GetString() == "Temperature");
        temperature.GetProperty("warnLow").GetDecimal().Should().Be(35.5m);
        temperature.GetProperty("warnHigh").GetDecimal().Should().Be(42m);
        temperature.GetProperty("updatedUtc").ValueKind.Should().NotBe(JsonValueKind.Null);

        var pulse = body.EnumerateArray()
            .Single(r => r.GetProperty("metric").GetString() == "PulseBpm");
        pulse.GetProperty("warnLow").ValueKind.Should().Be(JsonValueKind.Null,
            "a blank bound must come back blank, not as zero");
    }

    [Fact]
    public async Task A_blank_bound_is_stored_as_NULL_in_SQL_and_never_as_zero()
    {
        // Asserted against the column itself rather than the API response, because the failure
        // being guarded against is a coercion somewhere between the two - and a 0 in this column
        // would warn on every reading the clinic ever enters.
        var client = await SignedInClientAsync();

        await client.PutAsJsonAsync("/api/clinic-settings/vital-ranges", new
        {
            items = new[] { new { metric = "PulseBpm", warnLow = (decimal?)null, warnHigh = (decimal?)120m } },
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        var stored = await db.VitalRangeSettings.SingleAsync(r => r.Metric == VitalMetric.PulseBpm);

        stored.WarnLow.Should().BeNull();
        stored.WarnHigh.Should().Be(120m);
    }

    [Fact]
    public async Task Put_with_an_inverted_range_is_a_400()
    {
        var client = await SignedInClientAsync();

        var put = await client.PutAsJsonAsync("/api/clinic-settings/vital-ranges", new
        {
            items = new[] { new { metric = "PulseBpm", warnLow = 120m, warnHigh = 40m } },
        });

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(put);
        body.GetProperty("errors").TryGetProperty("Items[0].WarnLow", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Put_with_an_unknown_metric_is_a_400_rather_than_a_silent_no_op()
    {
        var client = await SignedInClientAsync();

        var put = await client.PutAsJsonAsync("/api/clinic-settings/vital-ranges", new
        {
            items = new[] { new { metric = "Weight", warnHigh = 100m } },
        });

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_database_refuses_an_inverted_range_even_when_the_service_is_bypassed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        db.VitalRangeSettings.Add(new Domain.Entities.VitalRangeSetting
        {
            Metric = VitalMetric.PulseBpm,
            WarnLow = 200m,
            WarnHigh = 40m,
            UpdatedUtc = DateTimeOffset.UtcNow,
        });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "CK_VitalRangeSetting_LowNotAboveHigh holds regardless of who is writing");
    }
}

/// <summary>
/// F-4: "seed present after migration" (plan F-4 point 6), asserted on a database this class never
/// writes to.
/// </summary>
/// <remarks>
/// Its own <see cref="TestWebAppFactory"/> instance, so its own throwaway database, created purely
/// by running the committed migrations. Nothing here inserts an option - if these assertions pass,
/// it is because <c>AddClinicSettings</c> seeded the rows.
/// </remarks>
public class ClinicSettingsSeedTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ClinicSettingsSeedTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task The_migration_seeds_the_gender_list_from_the_plans_Q9_assumption()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        var genders = await db.SettingOptions
            .Where(o => o.Category == SettingCategory.Gender)
            .OrderBy(o => o.DisplayOrder)
            .ToListAsync();

        genders.Select(o => o.Value).Should().Equal("Female", "Male", "Other", "Not stated");
        genders.Should().OnlyContain(o => o.IsActive);
    }

    [Fact]
    public async Task The_migration_seeds_the_vitals_reason_list_from_the_plans_Q2_assumption()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        var reasons = await db.SettingOptions
            .Where(o => o.Category == SettingCategory.VitalsNotRecordedReason)
            .OrderBy(o => o.DisplayOrder)
            .ToListAsync();

        reasons.Select(o => o.Value).Should().Equal(
            "Equipment unavailable",
            "Patient declined",
            "Not clinically indicated",
            "Other");
    }

    [Fact]
    public async Task The_migration_seeds_no_vital_range_at_all()
    {
        // F-4 acceptance criterion 3 at its root: the reason no value produces a warning on a fresh
        // clinic is that there is nothing to compare it against. If this table were ever seeded
        // with a "sensible default", this codebase would be authoring clinical ranges - which plan
        // section 7 forbids outright.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        (await db.VitalRangeSettings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_fresh_clinic_warns_about_nothing_through_the_real_endpoint()
    {
        var client = _factory.CreateHttpsClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = TestWebAppFactory.TestUserName,
            password = TestWebAppFactory.TestPassword,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/api/clinic-settings/vital-ranges");
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        body.EnumerateArray().Should().OnlyContain(r =>
            r.GetProperty("warnLow").ValueKind == JsonValueKind.Null
            && r.GetProperty("warnHigh").ValueKind == JsonValueKind.Null
            && r.GetProperty("updatedUtc").ValueKind == JsonValueKind.Null);
    }
}
