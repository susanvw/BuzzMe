using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;

namespace BuzzMe.Api.IntegrationTests;

/// <summary>
/// Boots the real BuzzMe.Api host in-process, against a real, ephemeral MongoDB
/// (DEVELOPMENT_GUIDE.md §8 — "run against the real host, asserting exact status codes,
/// envelope shape, and error codes"; Sprint 1 — "do not mock MongoDB repositories").
/// Overrides only what a test run must not depend on an external environment for
/// (the signing key, the Mongo connection); everything else is the real composition root.
/// </summary>
public sealed class BuzzMeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "integration-test-signing-key-not-for-production-use-0123456789";
    public const string Issuer = "buzzme-api";
    public const string Audience = "buzzme-clients";

    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder("mongo:7.0").Build();

    public Task InitializeAsync() => _mongoContainer.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    public string CreateAccessTokenFor(Guid userId) =>
        TestJwtTokenFactory.CreateAccessToken(userId, Issuer, Audience, SigningKey);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Mongo:ConnectionString"] = _mongoContainer.GetConnectionString(),
                ["Mongo:DatabaseName"] = "buzzme_api_integration_tests",
            });
        });
    }
}
