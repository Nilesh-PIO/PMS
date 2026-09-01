using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Services;
using PMS.Domain.Enums;
using PMS.Infrastructure.Persistence;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-3 backend integration tests (plan F-3 point 6): "PUT then GET round-trip; signature upload
/// persists bytes". Runs the real pipeline against a throwaway LocalDB database created from the
/// committed migrations.
/// </summary>
/// <remarks>
/// The ClinicProfile table is truncated before each test. The row is a singleton, so without a
/// reset these tests would silently depend on the order xUnit happened to run them in - and a
/// suite that passes for that reason is worse than no suite.
/// </remarks>
public class ClinicProfileEndpointTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public ClinicProfileEndpointTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        await db.ClinicProfile.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static object AValidProfile => new
    {
        clinicName = "Sunrise Clinic",
        addressLines = "12 Station Road\nPune 411001",
        doctorName = "Dr A. Mehta",
        doctorRegistrationNo = "MMC-99215",
        prescriptionFooter = "Please bring this prescription to your next visit.",
        temperatureUnit = (int)TemperatureUnit.Celsius,
    };

    /// <summary>A real 1x1 PNG, so the magic-byte check sees a genuine file.</summary>
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

    private static MultipartFormDataContent AFileUpload(byte[] bytes, string fileName = "signature.png")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { content, "file", fileName } };
    }

    // --- auth: every route needs the cookie (plan F-3 point 3) --------------

    [Theory]
    [InlineData("GET", "/api/clinic-profile")]
    [InlineData("PUT", "/api/clinic-profile")]
    [InlineData("POST", "/api/clinic-profile/signature")]
    [InlineData("DELETE", "/api/clinic-profile/signature")]
    public async Task Every_clinic_profile_route_returns_401_without_a_cookie(string method, string path)
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), path));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_signature_upload_route_returns_401_for_a_real_upload_without_a_cookie()
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsync("/api/clinic-profile/signature", AFileUpload(APng()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- PUT then GET round-trip (plan F-3 point 6) -------------------------

    [Fact]
    public async Task Get_before_any_setup_returns_404_as_ProblemDetails()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/clinic-profile");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Put_then_Get_round_trips_every_field()
    {
        var client = await SignedInClientAsync();

        var put = await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync("/api/clinic-profile");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("clinicName").GetString().Should().Be("Sunrise Clinic");
        root.GetProperty("addressLines").GetString().Should().Be("12 Station Road\nPune 411001");
        root.GetProperty("doctorName").GetString().Should().Be("Dr A. Mehta");
        root.GetProperty("doctorRegistrationNo").GetString().Should().Be("MMC-99215");
        root.GetProperty("prescriptionFooter").GetString().Should()
            .Be("Please bring this prescription to your next visit.");
        root.GetProperty("temperatureUnit").GetInt32().Should().Be((int)TemperatureUnit.Celsius);
        root.GetProperty("isSetupComplete").GetBoolean().Should().BeTrue();
        root.GetProperty("signatureImageDataUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_second_put_updates_the_singleton_and_the_table_still_holds_one_row()
    {
        var client = await SignedInClientAsync();

        await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);
        await client.PutAsJsonAsync("/api/clinic-profile", new
        {
            clinicName = "Sunrise Family Clinic",
            addressLines = "12 Station Road",
            doctorName = "Dr A. Mehta",
            doctorRegistrationNo = "MMC-99215",
            temperatureUnit = (int)TemperatureUnit.Fahrenheit,
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        var rows = await db.ClinicProfile.AsNoTracking().ToListAsync();

        rows.Should().ContainSingle("the clinic has exactly one identity");
        rows[0].Id.Should().Be(1);
        rows[0].ClinicName.Should().Be("Sunrise Family Clinic");
        rows[0].TemperatureUnit.Should().Be(TemperatureUnit.Fahrenheit);
    }

    [Fact]
    public async Task The_database_refuses_a_second_clinic_profile_row()
    {
        // The service only ever writes Id = 1, so this asserts the guard underneath it: a direct
        // INSERT - the SSMS path - must fail on the check constraint, not create a second clinic.
        var client = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        var act = () => db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ClinicProfile
                (Id, ClinicName, AddressLines, DoctorName, DoctorRegistrationNo,
                 PrescriptionFooter, SignatureImage, TemperatureUnit, IsSetupComplete, UpdatedUtc)
            VALUES (2, 'Rogue Clinic', '', 'Dr Nobody', 'X', NULL, NULL, 1, 1, SYSDATETIMEOFFSET())
            """);

        (await act.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>())
            .Which.Message.Should().Contain("CK_ClinicProfile_SingletonRow",
                "the database itself must refuse the second row, not just the service");
    }

    [Fact]
    public async Task Put_with_missing_required_fields_returns_400_with_field_errors()
    {
        var client = await SignedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/clinic-profile", new
        {
            clinicName = "",
            doctorName = "",
            doctorRegistrationNo = "",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        errors.TryGetProperty("ClinicName", out _).Should().BeTrue();
        errors.TryGetProperty("DoctorName", out _).Should().BeTrue();
        errors.TryGetProperty("DoctorRegistrationNo", out _).Should().BeTrue();
        errors.TryGetProperty("TemperatureUnit", out _).Should().BeTrue();
    }

    [Fact]
    public async Task A_rejected_put_writes_nothing()
    {
        var client = await SignedInClientAsync();

        await client.PutAsJsonAsync("/api/clinic-profile", new { clinicName = "Half A Clinic" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        (await db.ClinicProfile.AnyAsync()).Should().BeFalse(
            "a 400 must not leave a half-written clinic identity behind");
    }

    // --- setupComplete flows through the session (E-1) ----------------------

    [Fact]
    public async Task The_session_reports_setup_incomplete_until_the_profile_is_saved()
    {
        var client = await SignedInClientAsync();

        var before = await client.GetFromJsonAsync<JsonElement>("/api/auth/session");
        before.GetProperty("setupComplete").GetBoolean().Should().BeFalse();

        await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);

        var after = await client.GetFromJsonAsync<JsonElement>("/api/auth/session");
        after.GetProperty("setupComplete").GetBoolean().Should().BeTrue(
            "the physician completes setup during a session, so the flag cannot be cached in the cookie");
    }

    [Fact]
    public async Task Login_after_setup_reports_setup_complete()
    {
        var setupClient = await SignedInClientAsync();
        await setupClient.PutAsJsonAsync("/api/clinic-profile", AValidProfile);

        var freshClient = _factory.CreateHttpsClient();
        var login = await freshClient.PostAsJsonAsync("/api/auth/login", new
        {
            userName = TestWebAppFactory.TestUserName,
            password = TestWebAppFactory.TestPassword,
        });

        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("setupComplete").GetBoolean().Should().BeTrue();
    }

    // --- signature upload persists bytes (plan F-3 point 6) -----------------

    [Fact]
    public async Task A_signature_upload_persists_the_exact_bytes()
    {
        var client = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);
        var png = APng();

        var response = await client.PostAsync("/api/clinic-profile/signature", AFileUpload(png));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        var stored = await db.ClinicProfile.AsNoTracking().SingleAsync();

        stored.SignatureImage.Should().Equal(png, "the stored image must be byte-identical to the upload");
    }

    [Fact]
    public async Task An_uploaded_signature_comes_back_on_the_profile_as_a_data_url()
    {
        var client = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);
        await client.PostAsync("/api/clinic-profile/signature", AFileUpload(APng()));

        var get = await client.GetFromJsonAsync<JsonElement>("/api/clinic-profile");

        get.GetProperty("signatureImageDataUrl").GetString()
            .Should().Be($"data:image/png;base64,{Convert.ToBase64String(APng())}");
    }

    [Fact]
    public async Task An_oversize_signature_returns_413_and_stores_nothing()
    {
        var client = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);

        var oversize = new byte[ClinicProfileService.MaxSignatureBytes + 1024];
        APng().CopyTo(oversize, 0);

        var response = await client.PostAsync("/api/clinic-profile/signature", AFileUpload(oversize));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        (await db.ClinicProfile.AsNoTracking().SingleAsync()).SignatureImage.Should().BeNull();
    }

    [Fact]
    public async Task A_signature_past_the_transport_limit_is_still_a_413_and_not_a_400()
    {
        // The regression this pins: with an IFormFile action parameter, MVC's form value provider
        // caught the transport limit, recorded it in ModelState, and [ApiController] answered
        // *400* - telling the physician their form was malformed when their file was too big.
        // The upload above (business-rule sized) never reached that code path, so this case needs
        // its own test.
        var client = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);

        var farOversize = new byte[ClinicProfileService.MaxSignatureBytes * 3];
        APng().CopyTo(farOversize, 0);

        var response = await client.PostAsync("/api/clinic-profile/signature", AFileUpload(farOversize));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task A_signature_post_with_no_file_is_a_400_not_a_500()
    {
        var client = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);

        var response = await client.PostAsync(
            "/api/clinic-profile/signature", new MultipartFormDataContent());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_non_png_signature_returns_400()
    {
        var client = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);

        // A JPEG deliberately named .png with an image/png content type: only the bytes tell
        // the truth, and only checking them keeps this failure off the print path.
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

        var response = await client.PostAsync("/api/clinic-profile/signature", AFileUpload(jpeg));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Uploading_a_signature_before_setup_returns_404()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsync("/api/clinic-profile/signature", AFileUpload(APng()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- signature removal --------------------------------------------------

    [Fact]
    public async Task Deleting_the_signature_clears_it_and_leaves_setup_complete()
    {
        var client = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/clinic-profile", AValidProfile);
        await client.PostAsync("/api/clinic-profile/signature", AFileUpload(APng()));

        var response = await client.DeleteAsync("/api/clinic-profile/signature");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("signatureImageDataUrl").ValueKind.Should().Be(JsonValueKind.Null);
        json.RootElement.GetProperty("isSetupComplete").GetBoolean().Should().BeTrue(
            "a physician who signs by hand has a complete setup, not a broken one");
    }

    [Fact]
    public async Task Deleting_a_signature_before_setup_returns_404()
    {
        var client = await SignedInClientAsync();

        var response = await client.DeleteAsync("/api/clinic-profile/signature");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
