using PMS.Application.Abstractions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IClinicProfileRepository"/> for the F-3 service tests.
/// </summary>
/// <remarks>
/// Models the singleton the same way the real repository does - at most one row - so a test that
/// saves twice exercises the update path rather than accidentally accumulating rows and passing
/// for the wrong reason.
/// </remarks>
public sealed class FakeClinicProfileRepository : IClinicProfileRepository
{
    private ClinicProfile? _profile;

    /// <summary>How many times <see cref="SaveChangesAsync"/> was called.</summary>
    public int SaveCount { get; private set; }

    /// <summary>How many times a row was inserted. A second insert would be a singleton bug.</summary>
    public int AddCount { get; private set; }

    public FakeClinicProfileRepository(ClinicProfile? seed = null) => _profile = seed;

    /// <summary>The stored row, for assertions about what was actually persisted.</summary>
    public ClinicProfile? Stored => _profile;

    public Task<ClinicProfile?> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_profile);

    public Task AddAsync(ClinicProfile profile, CancellationToken cancellationToken)
    {
        AddCount++;
        _profile = profile;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    /// <summary>A complete profile, i.e. one that satisfies the E-1 setup gate.</summary>
    public static ClinicProfile ACompleteProfile() => new()
    {
        Id = ClinicProfile.SingletonId,
        ClinicName = "Sunrise Clinic",
        AddressLines = "12 Station Road\nPune 411001",
        DoctorName = "Dr A. Mehta",
        DoctorRegistrationNo = "MMC-99215",
        TemperatureUnit = TemperatureUnit.Celsius,
        IsSetupComplete = true,
        UpdatedUtc = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero),
    };
}
