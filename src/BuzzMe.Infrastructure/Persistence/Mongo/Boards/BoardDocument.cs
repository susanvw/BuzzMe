using MongoDB.Bson.Serialization.Attributes;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Boards;

/// <summary>
/// The `boards` collection's document shape — DEVELOPMENT_GUIDE.md §6: "one aggregate,
/// one document," Membership embedded because it lives inside the Board aggregate.
/// Deliberately a plain data shape, never the same class as the Domain aggregate
/// (DEVELOPMENT_GUIDE.md §4) — <see cref="Mappers.BoardMapper"/> is the only place that
/// translates between the two.
/// </summary>
public sealed class BoardDocument
{
    [BsonId]
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required List<MembershipDocument> Memberships { get; init; }

    /// <summary>Null = active. Soft delete — IMPLEMENTATION_SPEC.md §1, same pattern as ReminderDocument.DeletedAt (Sprint 3.1).</summary>
    public DateTimeOffset? DeletedAt { get; init; }

    public required long Version { get; init; }
}
