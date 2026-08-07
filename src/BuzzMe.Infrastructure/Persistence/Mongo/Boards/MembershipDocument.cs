namespace BuzzMe.Infrastructure.Persistence.Mongo.Boards;

/// <summary>Embedded within <see cref="BoardDocument"/>. Role stored as a plain string, not the C# enum, so BSON serialization never depends on enum-serialization conventions — DEVELOPMENT_GUIDE.md §9.</summary>
public sealed class MembershipDocument
{
    public required Guid UserId { get; init; }

    public required string Role { get; init; }
}
