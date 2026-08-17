namespace BuzzMe.Domain.Auth;

/// <summary>Declared in Domain, implemented in Infrastructure — only what RefreshTokenAsync/rotation needs.</summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    /// <summary>RefreshTokenAsync resolves the presented plaintext bearer token by the hash of it — never a lookup by <see cref="RefreshTokenId"/>, which the client never sees.</summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken);
}
