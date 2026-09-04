using PMS.Application.Abstractions;
using PMS.Application.Dtos.Clinic;
using PMS.Application.Exceptions;
using PMS.Application.Services;
using PMS.Domain.Enums;

namespace PMS.Application.Tests.TestDoubles;

/// <summary>
/// A minimal <see cref="IClinicProfileService"/> whose only interesting behaviour is the answer
/// it gives to "is setup complete?". Lets the F-2 AuthService tests state the clinic's setup
/// state in one word without dragging the whole F-3 service and its repository into them.
/// </summary>
public sealed class StubClinicProfileService : IClinicProfileService
{
    private readonly bool _setupComplete;
    private readonly TemperatureUnit? _temperatureUnit;

    /// <param name="setupComplete">What "is setup complete?" answers.</param>
    /// <param name="temperatureUnit">
    /// F-4. The clinic's temperature unit, which <c>ClinicSettingsService</c> reads through this
    /// interface to label a temperature threshold (E-24). <c>null</c> means no profile has been
    /// saved at all, which is a state F-4 has to survive: the settings screens are reachable
    /// before anything downstream needs a unit.
    /// </param>
    public StubClinicProfileService(
        bool setupComplete = false,
        TemperatureUnit? temperatureUnit = null)
    {
        _setupComplete = setupComplete;
        _temperatureUnit = temperatureUnit;
    }

    /// <summary>How many times the session path asked. Pins that it is a read, not a cached claim.</summary>
    public int IsSetupCompleteCallCount { get; private set; }

    public Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken)
    {
        IsSetupCompleteCallCount++;
        return Task.FromResult(_setupComplete);
    }

    public Task EnsureSetupCompleteAsync(CancellationToken cancellationToken) =>
        _setupComplete
            ? Task.CompletedTask
            : throw new DomainRuleException(ClinicProfileService.SetupIncompleteRuleType, "Setup is incomplete.");

    public Task<ClinicProfileResponse?> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult<ClinicProfileResponse?>(_temperatureUnit is null
            ? null
            : new ClinicProfileResponse(
                "Sunrise Clinic",
                "12 Station Road",
                "Dr A. Mehta",
                "MMC-99215",
                null,
                _temperatureUnit.Value,
                null,
                _setupComplete,
                new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero)));

    public Task<ClinicProfileResponse> UpsertAsync(
        UpsertClinicProfileRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by the auth tests.");

    public Task<ClinicProfileResponse> SetSignatureAsync(byte[] content, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by the auth tests.");

    public Task<ClinicProfileResponse> ClearSignatureAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by the auth tests.");
}
