using PMS.Application.Abstractions;
using PMS.Application.Dtos.Clinic;
using PMS.Application.Exceptions;
using PMS.Application.Services;

namespace PMS.Application.Tests.TestDoubles;

/// <summary>
/// A minimal <see cref="IClinicProfileService"/> whose only interesting behaviour is the answer
/// it gives to "is setup complete?". Lets the F-2 AuthService tests state the clinic's setup
/// state in one word without dragging the whole F-3 service and its repository into them.
/// </summary>
public sealed class StubClinicProfileService : IClinicProfileService
{
    private readonly bool _setupComplete;

    public StubClinicProfileService(bool setupComplete = false) => _setupComplete = setupComplete;

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
        throw new NotSupportedException("Not exercised by the auth tests.");

    public Task<ClinicProfileResponse> UpsertAsync(
        UpsertClinicProfileRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by the auth tests.");

    public Task<ClinicProfileResponse> SetSignatureAsync(byte[] content, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by the auth tests.");

    public Task<ClinicProfileResponse> ClearSignatureAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not exercised by the auth tests.");
}
