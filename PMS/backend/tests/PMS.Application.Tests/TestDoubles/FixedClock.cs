using PMS.Application.Abstractions;

namespace PMS.Application.Tests.TestDoubles;

/// <summary>
/// Deterministic <see cref="IClock"/> for tests. Every later service test (visit draft
/// timestamps, session expiry, VisitDate fixed at draft creation) depends on time being
/// controllable rather than ambient, which is why F-1 introduces this.
/// </summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public DateOnly UtcToday => DateOnly.FromDateTime(UtcNow.UtcDateTime);

    /// <summary>Moves the clock forward, so a test can assert on elapsed-time behaviour.</summary>
    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
