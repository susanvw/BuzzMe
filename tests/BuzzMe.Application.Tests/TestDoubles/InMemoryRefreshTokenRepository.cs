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
}
