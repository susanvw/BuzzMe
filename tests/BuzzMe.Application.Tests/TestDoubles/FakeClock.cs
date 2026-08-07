using BuzzMe.Application.Abstractions;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>
/// A settable IClock for deterministic tests — every Application Service test that touches
/// timing (recurrence generation, grace windows, expiry) depends on this rather than the
/// real clock (DEVELOPMENT_GUIDE.md §9's "all time is read through IClock" rule is exactly
/// what makes this possible).
/// </summary>
public sealed class FakeClock(DateTimeOffset initialUtcNow) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = initialUtcNow;

    public void Advance(TimeSpan by) => UtcNow += by;

    public void Set(DateTimeOffset to) => UtcNow = to;
}
