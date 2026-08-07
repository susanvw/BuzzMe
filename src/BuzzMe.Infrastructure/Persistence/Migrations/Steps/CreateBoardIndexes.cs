using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Mongo.Boards;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Migrations.Steps;

/// <summary>DEVELOPMENT_GUIDE.md §6 — the `boards` collection's index table, exactly.</summary>
public sealed class CreateBoardIndexes(MongoContext context) : IMongoMigration
{
    public int Version => 1;

    public string Description => "Create boards.memberships.userId index";

    public Task ApplyAsync(CancellationToken cancellationToken)
    {
        var collection = context.Database.GetCollection<BoardDocument>("boards");

        var membersIndex = new CreateIndexModel<BoardDocument>(
            Builders<BoardDocument>.IndexKeys.Ascending("memberships.userId"),
            new CreateIndexOptions { Name = "ix_boards_memberships_userId" });

        return collection.Indexes.CreateOneAsync(membersIndex, cancellationToken: cancellationToken);
    }
}
