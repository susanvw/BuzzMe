using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Outbox;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Migrations.Steps;

/// <summary>DEVELOPMENT_GUIDE.md §6's named index — backs the outbox dispatcher's own claim query (unprocessed rows, oldest-available first).</summary>
public sealed class CreateOutboxIndexes(MongoContext context) : IMongoMigration
{
    public int Version => 9;

    public string Description => "Create outbox.processedAt+availableAt index";

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var collection = context.Database.GetCollection<OutboxMessage>("outbox");

        var dispatchIndex = new CreateIndexModel<OutboxMessage>(
            Builders<OutboxMessage>.IndexKeys.Ascending(d => d.ProcessedAt).Ascending(d => d.AvailableAt),
            new CreateIndexOptions { Name = "ix_outbox_processedAt_availableAt" });

        await collection.Indexes.CreateOneAsync(dispatchIndex, cancellationToken: cancellationToken);
    }
}
