using FluentAssertions;
using NSubstitute;
using PMS.Application.Abstractions;
using PMS.Application.Dtos.Health;
using PMS.Application.Services;
using PMS.Application.Tests.TestDoubles;

namespace PMS.Application.Tests.Services;

public class HealthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 10, 42, 0, TimeSpan.Zero);

    private static HealthService CreateSut(IDatabaseProbe probe) =>
        new(probe, new FixedClock(Now));

    [Fact]
    public void CheckApi_is_healthy_and_stamped_from_the_clock()
    {
        var sut = CreateSut(Substitute.For<IDatabaseProbe>());

        var result = sut.CheckApi();

        result.Status.Should().Be(HealthResponse.Healthy);
        result.Component.Should().Be(HealthService.ApiComponent);
        result.CheckedUtc.Should().Be(Now);
        result.Detail.Should().BeNull();
    }

    [Fact]
    public async Task CheckDatabaseAsync_is_healthy_when_the_probe_can_connect()
    {
        var probe = Substitute.For<IDatabaseProbe>();
        probe.CheckAsync(Arg.Any<CancellationToken>()).Returns(DatabaseProbeResult.Reachable());

        var result = await CreateSut(probe).CheckDatabaseAsync();

        result.Status.Should().Be(HealthResponse.Healthy);
        result.Component.Should().Be(HealthService.DatabaseComponent);
        result.Detail.Should().BeNull();
    }

    [Fact]
    public async Task CheckDatabaseAsync_is_unhealthy_and_carries_the_reason_when_unreachable()
    {
        var probe = Substitute.For<IDatabaseProbe>();
        probe.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(DatabaseProbeResult.Unreachable("Database is not reachable."));

        var result = await CreateSut(probe).CheckDatabaseAsync();

        result.Status.Should().Be(HealthResponse.Unhealthy);
        result.Detail.Should().Be("Database is not reachable.");
    }

    [Fact]
    public async Task CheckDatabaseAsync_forwards_the_cancellation_token()
    {
        var probe = Substitute.For<IDatabaseProbe>();
        probe.CheckAsync(Arg.Any<CancellationToken>()).Returns(DatabaseProbeResult.Reachable());
        using var cts = new CancellationTokenSource();

        await CreateSut(probe).CheckDatabaseAsync(cts.Token);

        await probe.Received(1).CheckAsync(cts.Token);
    }
}
