namespace BuzzMe.Infrastructure.Persistence.Migrations;

/// <summary>A row in the `_migrations` collection, tracking which <see cref="IMongoMigration"/> versions have already run.</summary>
public sealed class MongoMigrationRecord
{
    public required int Version { get; init; }

    public required string Description { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }
}
