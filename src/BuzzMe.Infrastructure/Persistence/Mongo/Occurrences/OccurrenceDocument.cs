using MongoDB.Bson.Serialization.Attributes;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Occurrences;

/// <summary>The `occurrences` collection's document shape — one aggregate, one document (DEVELOPMENT_GUIDE.md §6).</summary>
public sealed class OccurrenceDocument
{
    [BsonId]
    public required Guid Id { get; init; }

    public required Guid ReminderId { get; init; }

    public required DateTimeOffset DueAt { get; init; }

    /// <summary>Stored as a string, not the C# enum — same reasoning as MembershipRole/Recurrence/NotifyPreset.</summary>
    public required string Status { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public Guid? ResolvedByUserId { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public required long Version { get; init; }
}
