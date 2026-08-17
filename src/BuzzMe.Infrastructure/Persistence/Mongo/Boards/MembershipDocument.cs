namespace BuzzMe.Infrastructure.Persistence.Mongo.Boards;

/// <summary>Embedded within <see cref="BoardDocument"/>. Role/Status stored as plain strings, not the C# enums, so BSON serialization never depends on enum-serialization conventions — DEVELOPMENT_GUIDE.md §9.</summary>
public sealed class MembershipDocument
{
    public required Guid UserId { get; init; }

    public required string Role { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset JoinedAt { get; init; }

    public required bool Muted { get; init; }
}
