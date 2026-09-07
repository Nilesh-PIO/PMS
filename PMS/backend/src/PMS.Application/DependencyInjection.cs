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

        // F-6. Note the direction of this dependency: PatientService depends on the duplicate
        // service, and the duplicate service depends only on the repository and the clock. It has
        // to be callable *without* going through registration, because the client asks it while the
        // form is still being filled in - a warning that only arrives on submit is a warning that
        // arrives after the physician has stopped thinking about who this patient is.
        services.AddScoped<IPatientDuplicateService, PatientDuplicateService>();

        return services;
    }
}
