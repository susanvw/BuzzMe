using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Boards.Events;

/// <summary>IMPLEMENTATION_SPEC.md §2 — LeaveBoard's own event, matched exactly by name.</summary>
public sealed record MemberLeft(Guid EventId, DateTimeOffset OccurredAt, BoardId BoardId, Guid UserId) : IDomainEvent;
