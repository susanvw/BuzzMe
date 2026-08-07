namespace BuzzMe.Application.Abstractions;

/// <summary>
/// The single source of "now" for the whole system. Domain and Application code must never
/// call DateTime.Now/UtcNow directly (DEVELOPMENT_GUIDE.md §9) — this is what makes the
/// referenceTimezone correctness work in IMPLEMENTATION_SPEC.md §1 actually testable.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
