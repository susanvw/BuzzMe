using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Users.Events;

/// <summary>APPLICATION_LAYER_SPEC.md §3.10 — UpdateProfile's own event, matched exactly by name.</summary>
public sealed record ProfileUpdated(Guid EventId, DateTimeOffset OccurredAt, UserId UserId) : IDomainEvent;
