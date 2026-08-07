using BuzzMe.Application.Abstractions;
using BuzzMe.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Migrations;

/// <summary>
/// Runs every registered <see cref="IMongoMigration"/> whose version hasn't yet been
/// recorded in `_migrations`, in ascending order. Invoked once at startup
/// (DEVELOPMENT_GUIDE.md §6) — not on every request, not on a schedule.
/// </summary>
public sealed class MongoMigrationRunner(
    MongoContext context,
    IEnumerable<IMongoMigration> migrations,
    IClock clock,
    ILogger<MongoMigrationRunner> logger)
{
    private IMongoCollection<MongoMigrationRecord> Records =>
        context.Database.GetCollection<MongoMigrationRecord>("_migrations");

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var applied = await Records.Find(Builders<MongoMigrationRecord>.Filter.Empty)
            .Project(record => record.Version)
            .ToListAsync(cancellationToken);
        var appliedVersions = applied.ToHashSet();

        foreach (var migration in migrations.OrderBy(migration => migration.Version))
        {
            if (appliedVersions.Contains(migration.Version)) continue;

            logger.LogInformation("Applying Mongo migration {Version}: {Description}", migration.Version, migration.Description);
            await migration.ApplyAsync(cancellationToken);
            await Records.InsertOneAsync(
                new MongoMigrationRecord
                {
                    Version = migration.Version,
                    Description = migration.Description,
                    AppliedAt = clock.UtcNow,
                },
                cancellationToken: cancellationToken);
        }
    }
}
