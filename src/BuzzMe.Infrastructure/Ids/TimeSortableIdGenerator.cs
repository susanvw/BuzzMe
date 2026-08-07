using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Infrastructure.Ids;

/// <summary>
/// Guid Version 7 (RFC 9562) — time-ordered, per DEVELOPMENT_GUIDE.md §9's ID generation
/// standard. Natural MongoDB insertion order is meaningful and a correct, simple cursor
/// value for API_CONTRACT.md §7's pagination falls out for free.
/// </summary>
public sealed class TimeSortableIdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}
