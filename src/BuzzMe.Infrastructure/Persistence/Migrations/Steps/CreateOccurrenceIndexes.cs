using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Mongo.Occurrences;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Migrations.Steps;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1's stated idempotency key for Occurrence generation is
/// (reminderId, resolved due-date) — enforced here as a real, database-level unique
/// constraint, not just application logic (DEVELOPMENT_GUIDE.md §6's established pattern).
/// A second index backs ListByReminderAsync's cursor.
/// </summary>
public sealed class CreateOccurrenceIndexes(MongoContext context) : IMongoMigration
{
    public int Version => 3;

    public string Description => "Create occurrences.reminderId+dueAt (unique) and reminderId+_id indexes";

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var collection = context.Database.GetCollection<OccurrenceDocument>("occurrences");

        var uniqueDueAtIndex = new CreateIndexModel<OccurrenceDocument>(
            Builders<OccurrenceDocument>.IndexKeys.Ascending(d => d.ReminderId).Ascending(d => d.DueAt),
            new CreateIndexOptions { Name = "ux_occurrences_reminderId_dueAt", Unique = true });

        var listIndex = new CreateIndexModel<OccurrenceDocument>(
            Builders<OccurrenceDocument>.IndexKeys.Ascending(d => d.ReminderId).Ascending(d => d.Id),
            new CreateIndexOptions { Name = "ix_occurrences_reminderId_id" });

        await collection.Indexes.CreateManyAsync([uniqueDueAtIndex, listIndex], cancellationToken);
    }
}
