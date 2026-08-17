using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Mongo.Auth;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Migrations.Steps;

/// <summary>RefreshTokenAsync's own lookup path — every refresh request resolves its bearer token by this index.</summary>
public sealed class CreateRefreshTokenIndexes(MongoContext context) : IMongoMigration
{
    public int Version => 8;

    public string Description => "Create refreshtokens.tokenHash (unique) index";

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var collection = context.Database.GetCollection<RefreshTokenDocument>("refreshtokens");

        var index = new CreateIndexModel<RefreshTokenDocument>(
            Builders<RefreshTokenDocument>.IndexKeys.Ascending(d => d.TokenHash),
            new CreateIndexOptions { Name = "ux_refreshtokens_tokenhash", Unique = true });

        await collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }
}
