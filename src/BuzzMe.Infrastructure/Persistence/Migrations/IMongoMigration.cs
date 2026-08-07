namespace BuzzMe.Infrastructure.Persistence.Migrations;

/// <summary>
/// One numbered, idempotent migration step (index creation, document-shape backfill).
/// DEVELOPMENT_GUIDE.md §6 — no heavyweight migration framework is prescribed; this
/// interface plus <see cref="MongoMigrationRunner"/> is the whole mechanism. Concrete
/// migrations (`_001_CreateIndexes`, etc.) are added as each collection's indexes are
/// implemented — none exist yet because no repository does.
/// </summary>
public interface IMongoMigration
{
    /// <summary>Monotonically increasing; also the migration's identity in the `_migrations` collection.</summary>
    int Version { get; }

    string Description { get; }

    Task ApplyAsync(CancellationToken cancellationToken);
}
