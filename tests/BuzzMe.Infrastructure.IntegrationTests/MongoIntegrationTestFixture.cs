using BuzzMe.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Options;
using Testcontainers.MongoDb;
using Xunit;

namespace BuzzMe.Infrastructure.IntegrationTests;

/// <summary>
/// Spins up a real, ephemeral MongoDB (via Testcontainers) once per test collection and
/// exposes a ready-to-use <see cref="MongoContext"/> against it — DEVELOPMENT_GUIDE.md §8's
/// "Integration tests ... against a real MongoDB." Requires Docker; CI wiring is covered by
/// the backend-ci workflow.
/// </summary>
public sealed class MongoIntegrationTestFixture : IAsyncLifetime
{
    // WithReplicaSet() is required for MongoDB multi-document transactions
    // (DEVELOPMENT_GUIDE.md §7's transactional outbox) — a plain standalone mongod
    // rejects them with "Transaction numbers are only allowed on a replica set member."
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:7.0").WithReplicaSet().Build();

    public MongoContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var options = Options.Create(new MongoOptions
        {
            ConnectionString = _container.GetConnectionString(),
            DatabaseName = "buzzme_test",
        });
        Context = new MongoContext(options);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class MongoIntegrationTestCollection : ICollectionFixture<MongoIntegrationTestFixture>
{
    public const string Name = "Mongo integration tests";
}
