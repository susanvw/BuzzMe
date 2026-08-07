using MongoDB.Bson.Serialization.Attributes;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Buzzes;

/// <summary>The `buzzes` collection's document shape — one aggregate, one document (DEVELOPMENT_GUIDE.md §6).</summary>
public sealed class BuzzDocument
{
    [BsonId]
    public required Guid Id { get; init; }

    public required Guid OccurrenceId { get; init; }

    public required Guid RecipientUserId { get; init; }

    public required DateTimeOffset ScheduledAt { get; init; }

    /// <summary>Stored as a string, not the C# enum — same reasoning as OccurrenceDocument.Status.</summary>
    public required string Status { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required long Version { get; init; }
}
