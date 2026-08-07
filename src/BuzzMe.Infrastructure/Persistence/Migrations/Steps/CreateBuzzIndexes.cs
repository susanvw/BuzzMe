using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Mongo.Buzzes;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Migrations.Steps;

/// <summary>
/// Sprint 4's stated idempotency key for Buzz generation is (occurrenceId,
/// recipientUserId) — enforced here as a real, database-level unique constraint, not just
/// application logic (DEVELOPMENT_GUIDE.md §6's established pattern, same as
/// CreateOccurrenceIndexes). A second index backs ListPendingByRecipientAsync's
/// recipient+status+cursor query.
/// </summary>
public sealed class CreateBuzzIndexes(MongoContext context) : IMongoMigration
{
    public int Version => 4;

    public string Description => "Create buzzes.occurrenceId+recipientUserId (unique) and recipientUserId+status+_id indexes";

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var collection = context.Database.GetCollection<BuzzDocument>("buzzes");

        var uniqueRecipientIndex = new CreateIndexModel<BuzzDocument>(
            Builders<BuzzDocument>.IndexKeys.Ascending(d => d.OccurrenceId).Ascending(d => d.RecipientUserId),
            new CreateIndexOptions { Name = "ux_buzzes_occurrenceId_recipientUserId", Unique = true });

        var pendingListIndex = new CreateIndexModel<BuzzDocument>(
            Builders<BuzzDocument>.IndexKeys.Ascending(d => d.RecipientUserId).Ascending(d => d.Status).Ascending(d => d.Id),
            new CreateIndexOptions { Name = "ix_buzzes_recipientUserId_status_id" });

        await collection.Indexes.CreateManyAsync([uniqueRecipientIndex, pendingListIndex], cancellationToken);
    }
}
