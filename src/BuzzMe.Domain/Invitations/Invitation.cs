using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Invitations.Events;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Domain.Invitations;

/// <summary>
/// An offer of Membership on a specific Board — DOMAIN_MODEL.md's Invitation. Its own
/// Aggregate Root, deliberately separate from Board (DOMAIN_MODEL.md §6: "neither of those
/// aggregates should need to be locked to expire or revoke an Invitation").
/// </summary>
public sealed class Invitation : AggregateRoot<InvitationId>
{
    public InvitationToken Token { get; private init; }

    public BoardId BoardId { get; private init; }

    public Guid InviterUserId { get; private init; }

    public InvitationChannel Channel { get; private init; }

    /// <summary>Email or phone contact, only for <see cref="InvitationChannel.Email"/>/<see cref="InvitationChannel.Sms"/>; null for <see cref="InvitationChannel.Link"/>, whose invitee is resolved only at acceptance time (DOMAIN_MODEL.md).</summary>
    public string? TargetContact { get; private init; }

    public InvitationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    public Guid? AcceptedByUserId { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    private Invitation(InvitationToken token, BoardId boardId, Guid inviterUserId, InvitationChannel channel, string? targetContact, DateTimeOffset expiresAt)
    {
        Token = token;
        BoardId = boardId;
        InviterUserId = inviterUserId;
        Channel = channel;
        TargetContact = targetContact;
        ExpiresAt = expiresAt;
    }

    /// <summary>The only way a new Invitation comes into existence — always starts `Pending` (IMPLEMENTATION_SPEC.md §1's `SendInvitation` command).</summary>
    public static Invitation Send(
        InvitationId id, InvitationToken token, BoardId boardId, Guid inviterUserId,
        InvitationChannel channel, string? targetContact, DateTimeOffset expiresAt, DateTimeOffset sentAt)
    {
        var invitation = new Invitation(token, boardId, inviterUserId, channel, targetContact, expiresAt)
        {
            Id = id,
            Status = InvitationStatus.Pending,
            CreatedAt = sentAt,
        };

        invitation.Raise(new InvitationSent(Guid.CreateVersion7(), sentAt, id, token, boardId, inviterUserId));

        return invitation;
    }

    /// <summary>
    /// Lazy expiration (Sprint 5 brief: "No background cleanup worker yet. Expired
    /// invitations may simply be rejected when used.") — a Pending Invitation whose
    /// ExpiresAt has passed is treated as expired by every caller, without ever requiring
    /// Status to be physically transitioned to `Expired` in storage.
    /// </summary>
    public bool IsExpired(DateTimeOffset now) => Status == InvitationStatus.Pending && now >= ExpiresAt;

    /// <summary>IMPLEMENTATION_SPEC.md §1 — acceptance is who becomes the new Member; Board's own `MembershipGranted` is a separate, second step (APPLICATION_LAYER_SPEC.md §3.5/§7).</summary>
    public void Accept(Guid acceptingUserId, DateTimeOffset acceptedAt)
    {
        EnsurePending();

        Status = InvitationStatus.Accepted;
        AcceptedByUserId = acceptingUserId;
        ResolvedAt = acceptedAt;

        Raise(new InvitationAccepted(Guid.CreateVersion7(), acceptedAt, Id, BoardId, acceptingUserId));
    }

    public void Decline(DateTimeOffset declinedAt)
    {
        EnsurePending();

        Status = InvitationStatus.Declined;
        ResolvedAt = declinedAt;

        Raise(new InvitationDeclined(Guid.CreateVersion7(), declinedAt, Id, BoardId));
    }

    /// <summary>The inviter withdrawing their own still-pending offer (DOMAIN_MODEL.md: "the inviter may revoke it") — Sprint 5's `CancelInvitation`.</summary>
    public void Revoke(DateTimeOffset revokedAt)
    {
        EnsurePending();

        Status = InvitationStatus.Revoked;
        ResolvedAt = revokedAt;

        Raise(new InvitationRevoked(Guid.CreateVersion7(), revokedAt, Id, BoardId));
    }

    /// <summary>Terminal-state protection is the aggregate's own invariant, not just an Application-layer precondition — a resolved Invitation can never be re-resolved to a different outcome.</summary>
    private void EnsurePending()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Invitation {Id} is not Pending (current status: {Status}).");
    }

    internal static Invitation Rehydrate(
        InvitationId id, InvitationToken token, BoardId boardId, Guid inviterUserId, InvitationChannel channel,
        string? targetContact, InvitationStatus status, DateTimeOffset createdAt, DateTimeOffset expiresAt,
        Guid? acceptedByUserId, DateTimeOffset? resolvedAt, long version)
    {
        var invitation = new Invitation(token, boardId, inviterUserId, channel, targetContact, expiresAt)
        {
            Id = id,
            Status = status,
            CreatedAt = createdAt,
            AcceptedByUserId = acceptedByUserId,
            ResolvedAt = resolvedAt,
        };
        invitation.Version = version;
        return invitation;
    }
}
