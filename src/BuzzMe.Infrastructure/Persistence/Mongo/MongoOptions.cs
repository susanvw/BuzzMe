namespace BuzzMe.Infrastructure.Persistence.Mongo;

/// <summary>Bound from the "Mongo" configuration section (DEVELOPMENT_GUIDE.md §9 — strongly-typed Options, bound once).</summary>
public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public required string ConnectionString { get; init; }

    public required string DatabaseName { get; init; }
}
