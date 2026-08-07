using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Invitations.Events;

/// <summary>EVENT_STORMING.md §B4 — the offer was accepted; `MembershipGranted` on Board is a separate, second step (APPLICATION_LAYER_SPEC.md §3.5/§7 — different aggregate roots, eventually consistent).</summary>
public sealed record InvitationAccepted(Guid EventId, DateTimeOffset OccurredAt, InvitationId InvitationId, BoardId BoardId, Guid AcceptedByUserId) : IDomainEvent;
