using BuzzMe.Domain.Auth;

namespace BuzzMe.Domain.Tests.Auth;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);

    private static RefreshToken NewToken(DateTimeOffset? expiresAt = null) => RefreshToken.Issue(
        new RefreshTokenId(Guid.CreateVersion7()), Guid.CreateVersion7(), "token-hash", expiresAt ?? Now + TimeSpan.FromDays(30), Now);

    [Fact]
    public void Issue_StampsTheGivenFields()
    {
        var id = new RefreshTokenId(Guid.CreateVersion7());
        var userId = Guid.CreateVersion7();
        var expiresAt = Now + TimeSpan.FromDays(30);

        var token = RefreshToken.Issue(id, userId, "token-hash", expiresAt, Now);

        Assert.Equal(id, token.Id);
        Assert.Equal(userId, token.UserId);
        Assert.Equal("token-hash", token.TokenHash);
        Assert.Equal(Now, token.CreatedAt);
        Assert.Equal(expiresAt, token.ExpiresAt);
        Assert.Null(token.RevokedAt);
    }

    [Fact]
    public void IsValid_TrueBeforeExpiryAndUnrevoked()
    {
        var token = NewToken();

        Assert.True(token.IsValid(Now));
    }

    [Fact]
    public void IsValid_FalseOnceExpired()
    {
        var expiresAt = Now + TimeSpan.FromDays(30);
        var token = NewToken(expiresAt);

        Assert.False(token.IsValid(expiresAt));
    }

    [Fact]
    public void IsValid_FalseOnceRevoked()
    {
        var token = NewToken();

        token.Revoke(Now);

        Assert.False(token.IsValid(Now));
    }

    [Fact]
    public void Revoke_IsIdempotent()
    {
        var token = NewToken();
        token.Revoke(Now);
        var firstRevokedAt = token.RevokedAt;

        token.Revoke(Now + TimeSpan.FromMinutes(1));

        Assert.Equal(firstRevokedAt, token.RevokedAt);
    }
}
