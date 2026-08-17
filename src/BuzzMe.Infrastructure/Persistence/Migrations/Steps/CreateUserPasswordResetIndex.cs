using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Mongo.Users;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Migrations.Steps;

/// <summary>
/// A separate migration step, not folded into CreateUserIndexes (Version 6) — a shipped
/// migration's own body is never edited after the fact, only superseded by a new one, same
/// discipline as every other migration in this codebase. ResetPasswordAsync resolves a User
/// by the hash of its bearer token; sparse + unique for the same reasons as the Email/Phone
/// indexes (most Users have no outstanding reset token at any given moment).
/// </summary>
public sealed class CreateUserPasswordResetIndex(MongoContext context) : IMongoMigration
{
    public int Version => 7;

    public string Description => "Create users.passwordResetTokenHash (unique, sparse) index";

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var collection = context.Database.GetCollection<UserDocument>("users");

        var index = new CreateIndexModel<UserDocument>(
            Builders<UserDocument>.IndexKeys.Ascending(d => d.PasswordResetTokenHash),
            new CreateIndexOptions { Name = "ux_users_passwordResetTokenHash", Unique = true, Sparse = true });

        await collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }
}
