using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Abstractions;
using PMS.Application.Services;

namespace PMS.Application.Tests;

/// <summary>
/// The composition root is scaffolding, so it gets scaffolding-level tests: if these break,
/// every later feature fails at runtime rather than at build time.
/// </summary>
public class ApplicationRegistrationTests
{
    [Fact]
    public void AddApplication_registers_the_clock_as_a_singleton()
    {
        var provider = new ServiceCollection().AddApplication().BuildServiceProvider();

        var first = provider.GetRequiredService<IClock>();
        var second = provider.GetRequiredService<IClock>();

        first.Should().BeOfType<SystemClock>();
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AddApplication_registers_the_health_service()
    {
        var services = new ServiceCollection().AddApplication();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IHealthService)
            && d.ImplementationType == typeof(HealthService)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddApplication_registers_the_clinic_profile_service()
    {
        var services = new ServiceCollection().AddApplication();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IClinicProfileService)
            && d.ImplementationType == typeof(ClinicProfileService)
            && d.Lifetime == ServiceLifetime.Scoped);
    }
}
