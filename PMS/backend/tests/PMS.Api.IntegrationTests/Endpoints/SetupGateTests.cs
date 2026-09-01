using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMS.Api.Filters;
using PMS.Api.Middleware;
using PMS.Domain.Enums;
using PMS.Infrastructure.Persistence;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-3 acceptance criterion 3, end-to-end: <c>POST /api/prescriptions/...</c> returns 409 with a
/// ProblemDetails naming setup as incomplete while <c>IsSetupComplete</c> is false (E-1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a test-only controller.</b> <c>PrescriptionsController</c> is F-14's, and building it
/// here to satisfy an F-3 criterion would be shipping F-14's route with none of F-14's behaviour -
/// a stub on the live surface that later has to be found and removed. What F-3 actually owns is
/// the gate: <see cref="RequiresSetupCompleteAttribute"/> and
/// <c>ClinicProfileService.EnsureSetupCompleteAsync</c>. So the gate is exercised through the
/// real pipeline - real filter, real service, real database, real ProblemDetails middleware - on
/// a controller that exists only in this test assembly and is mounted at the route shape F-14
/// will use.
/// </para>
/// <para>
/// F-14's job is then one line: put <c>[RequiresSetupComplete]</c> on the real controller. If it
/// forgets, that is an F-14 test failure, not a hole this test papers over.
/// </para>
/// </remarks>
public class SetupGateTests : IClassFixture<SetupGateWebAppFactory>, IAsyncLifetime
{
    private readonly SetupGateWebAppFactory _factory;

    public SetupGateTests(SetupGateWebAppFactory factory)
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

    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = _factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = TestWebAppFactory.TestUserName,
            password = TestWebAppFactory.TestPassword,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    [Fact]
    public async Task Printing_before_setup_returns_409_naming_setup_as_incomplete()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsync(
            "/api/prescriptions/00000000-0000-0000-0000-000000000001/issue", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty(ProblemDetailsMiddleware.RuleTypeExtension).GetString()
            .Should().Be("setup-incomplete",
                "the client branches on the slug, not on a sentence that may be reworded");
        json.RootElement.GetProperty("detail").GetString()
            .Should().Contain("Clinic setup is incomplete");
    }

    [Fact]
    public async Task The_409_names_what_is_missing_so_the_physician_knows_what_to_do()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsync(
            "/api/prescriptions/00000000-0000-0000-0000-000000000001/issue", content: null);

        var detail = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("detail").GetString()!;

        detail.Should().ContainAll("clinic name", "doctor name", "registration number", "temperature unit");
    }

    [Fact]
    public async Task Printing_is_allowed_once_the_clinic_profile_is_saved()
    {
        var client = await SignedInClientAsync();

        await client.PutAsJsonAsync("/api/clinic-profile", new
        {
            clinicName = "Sunrise Clinic",
            addressLines = "12 Station Road",
            doctorName = "Dr A. Mehta",
            doctorRegistrationNo = "MMC-99215",
            temperatureUnit = (int)TemperatureUnit.Celsius,
        });

        var response = await client.PostAsync(
            "/api/prescriptions/00000000-0000-0000-0000-000000000001/issue", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the gate must open, not merely close");
    }

    [Fact]
    public async Task The_gate_sits_behind_authentication_not_in_front_of_it()
    {
        // A signed-out caller must get 401, not a 409 that leaks whether this clinic is configured.
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsync(
            "/api/prescriptions/00000000-0000-0000-0000-000000000001/issue", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

/// <summary>
/// <see cref="TestWebAppFactory"/> plus this assembly's controllers, so
/// <see cref="TestPrescriptionsController"/> is routable. Nothing else differs.
/// </summary>
public class SetupGateWebAppFactory : TestWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
            services.AddControllers()
                .AddApplicationPart(typeof(TestPrescriptionsController).Assembly));
    }
}

/// <summary>
/// Stands in for F-14's <c>PrescriptionsController</c> for the sole purpose of proving F-3's gate.
/// Lives in the test assembly and is only ever routed by <see cref="SetupGateWebAppFactory"/>, so
/// it cannot reach a deployed application.
/// </summary>
[ApiController]
[Route("api/prescriptions")]
[RequiresSetupComplete]
public class TestPrescriptionsController : ControllerBase
{
    [HttpPost("{visitId:guid}/issue")]
    public IActionResult Issue(Guid visitId) => Ok(new { visitId, issued = true });
}
