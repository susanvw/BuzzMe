using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Boards.Events;

/// <summary>
/// IMPLEMENTATION_SPEC.md §5: the future Offer/Accept/Decline ownership-transfer sequence
/// is collapsed to this single system-triggered event for V1 — there is no counterpart
/// offer/accept step to model yet. Raised only as part of a LeaveBoard (or, in a future
/// sprint, DeleteAccount) transaction where the departing actor was the sole Owner.
/// </summary>
public sealed record BoardOwnershipReassigned(
    Guid EventId, DateTimeOffset OccurredAt, BoardId BoardId, Guid PreviousOwnerUserId, Guid NewOwnerUserId) : IDomainEvent;
