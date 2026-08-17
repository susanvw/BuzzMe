using BuzzMe.Domain.Auth;
using BuzzMe.Infrastructure.Persistence.Migrations.Steps;
using BuzzMe.Infrastructure.Persistence.Mongo.Auth;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.IntegrationTests.Auth;

/// <summary>Against a real, ephemeral MongoDB (MongoIntegrationTestFixture) — Sprint 1's explicit "do not mock MongoDB repositories."</summary>
[Collection(MongoIntegrationTestCollection.Name)]
public sealed class RefreshTokenRepositoryTests(MongoIntegrationTestFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private RefreshTokenRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _repository = new RefreshTokenRepository(fixture.Context);
        await new CreateRefreshTokenIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // The Mongo container is shared across every test in this collection — TokenHash is
    // uniquely indexed, so each test mints a fresh one, same reasoning as UserRepositoryTests'
    // UniqueEmail/UniquePhone.
    private static string UniqueTokenHash() => Guid.CreateVersion7().ToString();

    private static RefreshToken NewToken(string? tokenHash = null, DateTimeOffset? expiresAt = null) => RefreshToken.Issue(
        new RefreshTokenId(Guid.CreateVersion7()), Guid.CreateVersion7(), tokenHash ?? UniqueTokenHash(), expiresAt ?? Now + TimeSpan.FromDays(30), Now);

    [Fact]
    public async Task AddAsync_PersistsTheTokenAtVersionZero()
    {
        var tokenHash = UniqueTokenHash();
        var token = NewToken(tokenHash: tokenHash);

        await _repository.AddAsync(token, CancellationToken.None);
        var reloaded = await _repository.GetByTokenHashAsync(tokenHash, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.Version);
        Assert.Equal(token.UserId, reloaded.UserId);
        Assert.Null(reloaded.RevokedAt);
    }

    [Fact]
    public async Task GetByTokenHashAsync_ReturnsNullForAnUnknownHash()
    {
        var result = await _repository.GetByTokenHashAsync(UniqueTokenHash(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_RejectsADuplicateTokenHashAtTheDatabaseLevel()
    {
        var tokenHash = UniqueTokenHash();
        await _repository.AddAsync(NewToken(tokenHash: tokenHash), CancellationToken.None);

        var duplicate = NewToken(tokenHash: tokenHash);
        await Assert.ThrowsAsync<MongoWriteException>(() => _repository.AddAsync(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_PersistsRevocation()
    {
        var tokenHash = UniqueTokenHash();
        var token = NewToken(tokenHash: tokenHash);
        await _repository.AddAsync(token, CancellationToken.None);

        token.Revoke(Now);
        await _repository.UpdateAsync(token, CancellationToken.None);
        var reloaded = await _repository.GetByTokenHashAsync(tokenHash, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded.RevokedAt);
        Assert.False(reloaded.IsValid(Now));
    }
}
