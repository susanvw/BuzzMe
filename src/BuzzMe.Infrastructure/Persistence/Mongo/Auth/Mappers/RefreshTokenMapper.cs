using BuzzMe.Domain.Auth;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Auth.Mappers;

/// <summary>The one place RefreshToken (Domain) and RefreshTokenDocument (Mongo) translate into each other — DEVELOPMENT_GUIDE.md §4.</summary>
internal static class RefreshTokenMapper
{
    public static RefreshTokenDocument ToDocument(RefreshToken refreshToken) => new()
    {
        Id = refreshToken.Id.Value,
        UserId = refreshToken.UserId,
        TokenHash = refreshToken.TokenHash,
        CreatedAt = refreshToken.CreatedAt,
        ExpiresAt = refreshToken.ExpiresAt,
        RevokedAt = refreshToken.RevokedAt,
        Version = refreshToken.Version,
    };

    public static RefreshToken ToDomain(RefreshTokenDocument document) => RefreshToken.Rehydrate(
        new RefreshTokenId(document.Id),
        document.UserId,
        document.TokenHash,
        document.CreatedAt,
        document.ExpiresAt,
        document.RevokedAt,
        document.Version);
}
