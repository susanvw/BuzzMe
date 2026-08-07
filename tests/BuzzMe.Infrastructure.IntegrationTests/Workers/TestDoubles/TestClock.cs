using BuzzMe.Application.Abstractions;

namespace BuzzMe.Infrastructure.IntegrationTests.Workers.TestDoubles;

/// <summary>A settable IClock — same reasoning as Application.Tests' FakeClock, needed here since this test project has no clock double of its own yet.</summary>
public sealed class TestClock(DateTimeOffset initialUtcNow) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = initialUtcNow;

    public void Advance(TimeSpan by) => UtcNow += by;
}
