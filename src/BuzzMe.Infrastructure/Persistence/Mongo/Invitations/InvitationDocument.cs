using MongoDB.Bson.Serialization.Attributes;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Invitations;

/// <summary>The `invitations` collection's document shape — one aggregate, one document (DEVELOPMENT_GUIDE.md §6).</summary>
public sealed class InvitationDocument
{
    [BsonId]
    public required Guid Id { get; init; }

    public required string Token { get; init; }

    public required Guid BoardId { get; init; }

    public required Guid InviterUserId { get; init; }

    /// <summary>Stored as a string, not the C# enum — same reasoning as every other Status/Channel field in this codebase.</summary>
    public required string Channel { get; init; }

    public string? TargetContact { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public Guid? AcceptedByUserId { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public required long Version { get; init; }
}
