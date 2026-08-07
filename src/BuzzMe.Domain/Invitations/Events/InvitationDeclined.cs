using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Invitations.Events;

/// <summary>EVENT_STORMING.md §B4 — the recipient declined; a terminal outcome with no further Domain side effect.</summary>
public sealed record InvitationDeclined(Guid EventId, DateTimeOffset OccurredAt, InvitationId InvitationId, BoardId BoardId) : IDomainEvent;
