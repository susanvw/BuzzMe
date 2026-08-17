using BuzzMe.Domain.Auth;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>An in-memory IRefreshTokenRepository — same rationale as InMemoryUserRepository.</summary>
public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _tokens = [];

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        _tokens.Add(refreshToken);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_tokens.FirstOrDefault(token => token.TokenHash == tokenHash));

    public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) =>
        // No-op: the fake holds RefreshToken by reference — the caller already mutated it via token.Revoke(...) before calling this.
        Task.CompletedTask;

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        // Unlike UpdateAsync, there's no prior domain-level Revoke() call for the caller to
        // have already made — this fake mutates directly, mirroring the real repository's own
        // "no in-memory aggregate involved" reasoning (IRefreshTokenRepository's own doc comment).
        foreach (var token in _tokens.Where(t => t.UserId == userId && t.RevokedAt is null))
            token.Revoke(revokedAt);

        return Task.CompletedTask;
    }
}
