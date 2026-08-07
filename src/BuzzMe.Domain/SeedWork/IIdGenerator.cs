namespace BuzzMe.Domain.SeedWork;

/// <summary>
/// Generates identifiers for new aggregates. Declared in Domain so aggregate factory
/// methods can depend on it without depending on Infrastructure; implemented in
/// Infrastructure using time-sortable IDs (DEVELOPMENT_GUIDE.md §9 — "time-sortable IDs
/// ... give cursor pagination a simple, correct cursor value for free").
/// </summary>
public interface IIdGenerator
{
    Guid NewId();
}
