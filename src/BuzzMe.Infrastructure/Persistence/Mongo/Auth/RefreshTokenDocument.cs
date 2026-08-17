using MongoDB.Bson.Serialization.Attributes;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Auth;

/// <summary>The `refreshtokens` collection's document shape — one aggregate, one document (DEVELOPMENT_GUIDE.md §6).</summary>
public sealed class RefreshTokenDocument
{
    [BsonId]
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public required string TokenHash { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public required long Version { get; init; }
}
