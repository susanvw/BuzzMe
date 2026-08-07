using MongoDB.Bson.Serialization.Attributes;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Reminders;

/// <summary>
/// The `reminders` collection's document shape — one aggregate, one document
/// (DEVELOPMENT_GUIDE.md §6). Recurrence/NotifyPreset stored as plain strings (the same
/// codes the wire uses), not the C# enum, for the same reason MembershipRole was — BSON
/// serialization never depends on enum-serialization conventions.
/// </summary>
public sealed class ReminderDocument
{
    [BsonId]
    public required Guid Id { get; init; }

    public required Guid BoardId { get; init; }

    public required string Title { get; init; }

    public required string Recurrence { get; init; }

    public required DateTime StartDate { get; init; }

    public required string ReferenceTimezone { get; init; }

    public required string NotifyPreset { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Null = active. Soft delete — IMPLEMENTATION_SPEC.md §1, aligned in Sprint 3.1.</summary>
    public DateTimeOffset? DeletedAt { get; init; }

    public required long Version { get; init; }
}
