using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Invitations.Events;

/// <summary>
/// EVENT_STORMING.md §B4 — two distinct triggers produce this same event: the inviting
/// Member revoking their own pending offer (DOMAIN_MODEL.md: "the inviter may revoke it"),
/// or the system revoking it as a consequence of a block between the two parties
/// (IMPLEMENTATION_SPEC.md §4's "Block Revokes Pending Invitations" policy — not
/// implemented this sprint, since Block itself isn't). Sprint 5's `CancelInvitation`
/// Application use case is the inviter-initiated path.
/// </summary>
public sealed record InvitationRevoked(Guid EventId, DateTimeOffset OccurredAt, InvitationId InvitationId, BoardId BoardId) : IDomainEvent;
