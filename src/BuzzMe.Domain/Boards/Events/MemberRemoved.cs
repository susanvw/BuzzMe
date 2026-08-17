using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Boards.Events;

/// <summary>IMPLEMENTATION_SPEC.md §2 — RemoveMember's own event, matched exactly by name.</summary>
public sealed record MemberRemoved(Guid EventId, DateTimeOffset OccurredAt, BoardId BoardId, Guid UserId, Guid RemovedByUserId) : IDomainEvent;
