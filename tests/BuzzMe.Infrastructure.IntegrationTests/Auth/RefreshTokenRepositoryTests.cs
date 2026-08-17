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

    private static RefreshToken NewToken(string? tokenHash = null, DateTimeOffset? expiresAt = null, Guid? userId = null) => RefreshToken.Issue(
        new RefreshTokenId(Guid.CreateVersion7()), userId ?? Guid.CreateVersion7(), tokenHash ?? UniqueTokenHash(), expiresAt ?? Now + TimeSpan.FromDays(30), Now);

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

    [Fact]
    public async Task RevokeAllForUserAsync_RevokesOnlyThatUsersOutstandingTokens()
    {
        var userId = Guid.CreateVersion7();
        var otherUserId = Guid.CreateVersion7();
        var tokenHash1 = UniqueTokenHash();
        var tokenHash2 = UniqueTokenHash();
        var otherUsersTokenHash = UniqueTokenHash();
        await _repository.AddAsync(NewToken(tokenHash: tokenHash1, userId: userId), CancellationToken.None);
        await _repository.AddAsync(NewToken(tokenHash: tokenHash2, userId: userId), CancellationToken.None);
        await _repository.AddAsync(NewToken(tokenHash: otherUsersTokenHash, userId: otherUserId), CancellationToken.None);

        await _repository.RevokeAllForUserAsync(userId, Now.AddDays(1), CancellationToken.None);

        var token1 = await _repository.GetByTokenHashAsync(tokenHash1, CancellationToken.None);
        var token2 = await _repository.GetByTokenHashAsync(tokenHash2, CancellationToken.None);
        var otherToken = await _repository.GetByTokenHashAsync(otherUsersTokenHash, CancellationToken.None);
        Assert.False(token1!.IsValid(Now.AddDays(1)));
        Assert.False(token2!.IsValid(Now.AddDays(1)));
        Assert.True(otherToken!.IsValid(Now.AddDays(1)));
    }

    [Fact]
    public async Task RevokeAllForUserAsync_DoesNotReRevokeAnAlreadyRevokedTokensTimestamp()
    {
        var userId = Guid.CreateVersion7();
        var tokenHash = UniqueTokenHash();
        var token = NewToken(tokenHash: tokenHash, userId: userId);
        await _repository.AddAsync(token, CancellationToken.None);
        token.Revoke(Now);
        await _repository.UpdateAsync(token, CancellationToken.None);

        await _repository.RevokeAllForUserAsync(userId, Now.AddDays(1), CancellationToken.None);

        var reloaded = await _repository.GetByTokenHashAsync(tokenHash, CancellationToken.None);
        Assert.Equal(Now, reloaded!.RevokedAt);
    }
}
