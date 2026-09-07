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

        // F-4. Scoped: reaches the database through IClinicSettingsRepository, and reads the
        // clinic's temperature unit through IClinicProfileService rather than re-deriving it.
        services.AddScoped<IClinicSettingsService, ClinicSettingsService>();

        // F-5. Scoped: reaches the database through IPatientRepository, and asks
        // IClinicSettingsService whether a submitted gender is one the clinic offers rather than
        // keeping a second copy of that list (C-20).
        services.AddScoped<IPatientService, PatientService>();

        // F-7. Scoped: reaches the database through the same IPatientRepository. Kept separate from
        // IPatientService because finding a patient and registering one are different jobs with
        // different collaborators - F-9's appointment picker will want this one and not the other.
        services.AddScoped<IPatientSearchService, PatientSearchService>();

        return services;
    }
}
