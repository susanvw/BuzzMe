using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Mongo.Users;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Migrations.Steps;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1's uniqueness invariant ("email/phone unique across all
/// Users") enforced as a real database constraint. Both indexes are sparse — a User may
/// legitimately have only one of Email/Phone set (User's own constructor already
/// requires at least one), and a sparse index simply omits documents missing the indexed
/// field from the uniqueness check, rather than treating every "field absent" User as a
/// colliding null.
/// </summary>
public sealed class CreateUserIndexes(MongoContext context) : IMongoMigration
{
    public int Version => 6;

    public string Description => "Create users.email (unique, sparse) and users.phone (unique, sparse) indexes";

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var collection = context.Database.GetCollection<UserDocument>("users");

        var uniqueEmailIndex = new CreateIndexModel<UserDocument>(
            Builders<UserDocument>.IndexKeys.Ascending(d => d.Email),
            new CreateIndexOptions { Name = "ux_users_email", Unique = true, Sparse = true });

        var uniquePhoneIndex = new CreateIndexModel<UserDocument>(
            Builders<UserDocument>.IndexKeys.Ascending(d => d.Phone),
            new CreateIndexOptions { Name = "ux_users_phone", Unique = true, Sparse = true });

        await collection.Indexes.CreateManyAsync([uniqueEmailIndex, uniquePhoneIndex], cancellationToken);
    }
}
