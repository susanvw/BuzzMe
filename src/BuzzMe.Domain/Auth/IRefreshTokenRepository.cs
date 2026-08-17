namespace BuzzMe.Domain.Auth;

/// <summary>Declared in Domain, implemented in Infrastructure — only what RefreshTokenAsync/rotation needs.</summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    /// <summary>RefreshTokenAsync resolves the presented plaintext bearer token by the hash of it — never a lookup by <see cref="RefreshTokenId"/>, which the client never sees.</summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's ConfirmAccountDeletion — "revokes sessions." A direct,
    /// bulk operation (not a load-every-RefreshToken-then-Revoke-then-UpdateAsync loop):
    /// unlike every other RefreshToken write in this codebase, there is no in-memory
    /// aggregate whose own `Revoke()` needs to run first — Infrastructure sets RevokedAt
    /// directly for every still-valid token belonging to this User.
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
