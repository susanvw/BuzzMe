using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Users.Events;

/// <summary>
/// IMPLEMENTATION_SPEC.md §2 — VerifyAccount's own event, matched exactly by name. Carries
/// no PersonalBoardId: at the instant this fires, provisioning hasn't run yet (Account
/// Provisioning is the policy that reacts to this event — APPLICATION_LAYER_SPEC.md §7).
/// </summary>
public sealed record AccountVerified(Guid EventId, DateTimeOffset OccurredAt, UserId UserId) : IDomainEvent;
