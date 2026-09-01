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

        // F-2. Both are scoped because they reach the database through IAppUserRepository.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IInitialUserSeeder, InitialUserSeeder>();

        // F-3. Scoped: reaches the database through IClinicProfileRepository.
        services.AddScoped<IClinicProfileService, ClinicProfileService>();

        return services;
    }
}
