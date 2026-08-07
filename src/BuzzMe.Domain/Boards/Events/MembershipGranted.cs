using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Boards.Events;

/// <summary>EVENT_STORMING.md §B4/§N — a User now belongs to a Board, with the given role.</summary>
public sealed record MembershipGranted(Guid EventId, DateTimeOffset OccurredAt, BoardId BoardId, Guid UserId, MembershipRole Role) : IDomainEvent;
