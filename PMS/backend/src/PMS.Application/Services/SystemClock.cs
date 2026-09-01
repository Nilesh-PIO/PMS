using PMS.Application.Abstractions;

namespace PMS.Application.Services;

/// <summary>
/// Production <see cref="IClock"/>. Registered as a singleton in the composition root.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly UtcToday => DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
}
