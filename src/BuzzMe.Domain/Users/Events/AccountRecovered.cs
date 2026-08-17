using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Users.Events;

/// <summary>IMPLEMENTATION_SPEC.md §2 — ConfirmAccountRecovery's own event, matched exactly by name (APPLICATION_LAYER_SPEC.md §3.10 names the use case ResetPassword; the event itself keeps the Implementation Spec's original name).</summary>
public sealed record AccountRecovered(Guid EventId, DateTimeOffset OccurredAt, UserId UserId) : IDomainEvent;
