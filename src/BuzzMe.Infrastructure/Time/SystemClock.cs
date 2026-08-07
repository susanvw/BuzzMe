using BuzzMe.Application.Abstractions;

namespace BuzzMe.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
