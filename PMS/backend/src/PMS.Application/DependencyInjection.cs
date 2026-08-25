using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Abstractions;
using PMS.Application.Services;

namespace PMS.Application;

/// <summary>
/// Registers the application layer. Called from the PMS.Api composition root so that
/// Program.cs never news up a service by hand.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IHealthService, HealthService>();

        return services;
    }
}
