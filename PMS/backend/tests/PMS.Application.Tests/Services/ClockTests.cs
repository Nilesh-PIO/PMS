using FluentAssertions;
using PMS.Application.Abstractions;
using PMS.Application.Services;
using PMS.Application.Tests.TestDoubles;

namespace PMS.Application.Tests.Services;

/// <summary>
/// F-1 test strategy: "deterministic IClock, used by every later service test".
/// These assertions pin the contract the rest of the suite will lean on.
/// </summary>
public class ClockTests
{
    [Fact]
    public void SystemClock_returns_utc()
    {
        IClock clock = new SystemClock();

        clock.UtcNow.Offset.Should().Be(TimeSpan.Zero,
            "every stored instant is UTC; a local-time offset would make visit timestamps ambiguous across DST");
    }

    [Fact]
    public void SystemClock_UtcToday_matches_UtcNow_date()
    {
        IClock clock = new SystemClock();

        var now = clock.UtcNow;
        var today = clock.UtcToday;

        today.Should().BeOneOf(
            DateOnly.FromDateTime(now.UtcDateTime),
            DateOnly.FromDateTime(now.UtcDateTime.AddSeconds(1)));
    }

    [Fact]
    public void SystemClock_advances()
    {
        IClock clock = new SystemClock();

        var first = clock.UtcNow;
        var second = clock.UtcNow;

        second.Should().BeOnOrAfter(first);
    }

    [Fact]
    public void FixedClock_is_stable_across_reads()
    {
        var instant = new DateTimeOffset(2026, 8, 25, 10, 42, 0, TimeSpan.Zero);
        IClock clock = new FixedClock(instant);

        clock.UtcNow.Should().Be(instant);
        clock.UtcNow.Should().Be(instant, "a test double must not drift between two reads");
    }

    [Fact]
    public void FixedClock_UtcToday_is_derived_from_UtcNow()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 25, 23, 59, 59, TimeSpan.Zero));

        clock.UtcToday.Should().Be(new DateOnly(2026, 8, 25));
    }

    [Fact]
    public void FixedClock_Advance_moves_time_forward_only_when_told()
    {
        var start = new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(start);

        clock.Advance(TimeSpan.FromMinutes(5));

        clock.UtcNow.Should().Be(start.AddMinutes(5));
        clock.UtcToday.Should().Be(new DateOnly(2026, 8, 25));
    }

    [Fact]
    public void FixedClock_Advance_across_midnight_rolls_UtcToday()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 25, 23, 30, 0, TimeSpan.Zero));

        clock.Advance(TimeSpan.FromHours(1));

        clock.UtcToday.Should().Be(new DateOnly(2026, 8, 26),
            "F-10 fixes Visit.VisitDate at draft creation, so a date that rolls silently would be a real defect");
    }
}
