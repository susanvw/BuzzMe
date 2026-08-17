using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Auth;

/// <summary>
/// A session credential exchanged for a new access/refresh pair (API_CONTRACT.md §5's
/// `POST /auth/refresh-token`). Its own aggregate root, deliberately separate from User —
/// APPLICATION_LAYER_SPEC.md §3.10 lists Refresh Token's own transaction as "Session
/// reissuance only," never touching the User aggregate, and a User may hold many
/// concurrently (one per signed-in device). Only <see cref="TokenHash"/> is ever stored —
/// the plaintext bearer value is generated once (ISecureTokenGenerator), returned to the
/// caller, and never persisted, same at-rest discipline as a password.
/// </summary>
public sealed class RefreshToken : AggregateRoot<RefreshTokenId>
{
    public Guid UserId { get; private init; }

    public string TokenHash { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    public DateTimeOffset? RevokedAt { get; private set; }

    private RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    /// <summary>The only way a new RefreshToken comes into existence — issued on Login, VerifyAccount, or a Refresh Token rotation.</summary>
    public static RefreshToken Issue(RefreshTokenId id, Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset issuedAt)
    {
        return new RefreshToken(userId, tokenHash, expiresAt)
        {
            Id = id,
            CreatedAt = issuedAt,
        };
    }

    /// <summary>Lazy expiration, same pattern as Invitation.IsExpired — no background sweep is specified for refresh tokens, so validity is always checked at use time.</summary>
    public bool IsValid(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>Rotation-on-use: RefreshTokenAsync revokes the token it was just given before issuing its replacement, so the same bearer value can never be exchanged twice — idempotent (revoking an already-revoked token is a no-op).</summary>
    public void Revoke(DateTimeOffset revokedAt)
    {
        RevokedAt ??= revokedAt;
    }

    internal static RefreshToken Rehydrate(
        RefreshTokenId id, Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt, DateTimeOffset? revokedAt, long version)
    {
        var refreshToken = new RefreshToken(userId, tokenHash, expiresAt)
        {
            Id = id,
            CreatedAt = createdAt,
            RevokedAt = revokedAt,
        };
        refreshToken.Version = version;
        return refreshToken;
    }
}
