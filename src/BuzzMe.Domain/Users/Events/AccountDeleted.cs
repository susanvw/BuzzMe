using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Users.Events;

/// <summary>IMPLEMENTATION_SPEC.md §2 — ConfirmAccountDeletion's own event, matched exactly by name.</summary>
public sealed record AccountDeleted(Guid EventId, DateTimeOffset OccurredAt, UserId UserId) : IDomainEvent;
