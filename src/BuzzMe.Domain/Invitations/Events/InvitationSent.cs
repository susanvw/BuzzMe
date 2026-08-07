using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Invitations.Events;

/// <summary>EVENT_STORMING.md §B4 — an offer of Membership now exists.</summary>
public sealed record InvitationSent(Guid EventId, DateTimeOffset OccurredAt, InvitationId InvitationId, InvitationToken Token, BoardId BoardId, Guid InviterUserId) : IDomainEvent;
