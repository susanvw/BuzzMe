using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Boards.Models;
using BuzzMe.Application.Invitations.Models;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Invitations;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Application.Invitations;

/// <summary>
/// One Application Service for the Invitations bounded-context area — Sprint 5's use cases
/// (APPLICATION_LAYER_SPEC.md §3.5: InviteMember/AcceptInvitation/DeclineInvitation, plus
/// ValidateInvitation which API_CONTRACT.md §5 specifies an endpoint for even though §3.5
/// doesn't name it separately, and the Sprint 5 brief's own CancelInvitation). Depends on
/// IBoardRepository for the same reason ReminderApplicationService does: Board-membership
/// is the authorization gate, and here also the place a new Membership is actually
/// granted. `ListPendingInvitationsAsync` was removed in Sprint 6 after review found it
/// had no specification basis anywhere — see SPRINT_6_REPORT.md.
/// </summary>
public sealed class InvitationApplicationService(
    IInvitationRepository invitationRepository, IBoardRepository boardRepository,
    IInvitationTokenGenerator tokenGenerator, IIdGenerator idGenerator, IClock clock)
{
    /// <summary>
    /// No document anywhere fixes an Invitation token TTL — an open operational parameter,
    /// same category as IMPLEMENTATION_SPEC.md §6's explicitly-acknowledged Buzz retry
    /// cadence. Seven days is a conventional default (matches common invitation-link
    /// lifetimes elsewhere), not a value derived from any BuzzMe specification.
    /// </summary>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.5 — Authorization: Board Member (any Active Member, no
    /// configurable policy — Implementation Spec §5's resolution). "Cannot invite existing
    /// Members" and "Cannot invite blocked users" (Sprint 5 brief's own validation examples)
    /// are NOT enforced here — see SPRINT_5_REPORT.md's specification gap: no User/Profile
    /// domain exists anywhere in this codebase to resolve <paramref name="targetContact"/>
    /// (an email/phone string) to a UserId, so there is nothing to check either rule
    /// against, for any channel. Documented, not invented, mirroring Sprint 4's identical
    /// treatment of the missing Block feature.
    /// </summary>
    public async Task<Result<InvitationResult>> InviteMemberAsync(
        Guid requestingUserId, Guid boardId, string channelCode, string? targetContact, CancellationToken cancellationToken)
    {
        var board = await boardRepository.GetByIdAsync(new BoardId(boardId), cancellationToken);
        if (board is null || !board.HasMember(requestingUserId))
            return Result.Failure<InvitationResult>(Error.NotFound("Board not found."));

        if (!InvitationChannelCodes.TryParse(channelCode, out var channel))
            return Result.Failure<InvitationResult>(Error.Validation("Channel must be one of the supported values."));

        var token = tokenGenerator.NewToken();
        var sentAt = clock.UtcNow;
        var expiresAt = sentAt + TokenLifetime;

        var invitation = Invitation.Send(
            new InvitationId(idGenerator.NewId()), token, board.Id, requestingUserId, channel, targetContact, expiresAt, sentAt);

        await invitationRepository.AddAsync(invitation, cancellationToken);

        return Result.Success(InvitationResult.FromDomain(invitation, board.Name.Value, sentAt));
    }

    /// <summary>API_CONTRACT.md §5 — Authorization: Public. The one unauthenticated read in this service; must work for someone with no account yet.</summary>
    public async Task<Result<InvitationResult>> ValidateInvitationAsync(string token, CancellationToken cancellationToken)
    {
        var invitation = await invitationRepository.GetByTokenAsync(new InvitationToken(token), cancellationToken);
        if (invitation is null)
            return Result.Failure<InvitationResult>(Error.NotFound("Invitation not found."));

        var board = await boardRepository.GetByIdAsync(invitation.BoardId, cancellationToken);
        if (board is null)
            return Result.Failure<InvitationResult>(Error.NotFound("Invitation not found."));

        return Result.Success(InvitationResult.FromDomain(invitation, board.Name.Value, clock.UtcNow));
    }

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.5 — Authorization: Authenticated User (the invitation's
    /// resolved invitee "where one was specified" per API_CONTRACT.md §5 — NOT enforced for
    /// email/sms invitations here, same specification gap as InviteMember: no mechanism
    /// exists to resolve TargetContact to a UserId to match against). Two-step,
    /// eventually-consistent by specification (§3.5/§7): Invitation → Accepted commits
    /// first, then Board's own MembershipGranted follows.
    /// </summary>
    public async Task<Result<MembershipResult>> AcceptInvitationAsync(Guid requestingUserId, string token, CancellationToken cancellationToken)
    {
        var invitation = await invitationRepository.GetByTokenAsync(new InvitationToken(token), cancellationToken);
        if (invitation is null)
            return Result.Failure<MembershipResult>(Error.NotFound("Invitation not found."));

        var board = await boardRepository.GetByIdAsync(invitation.BoardId, cancellationToken);
        if (board is null)
            return Result.Failure<MembershipResult>(Error.NotFound("Invitation not found."));

        // Natural idempotency: re-accepting your own already-accepted Invitation returns
        // the existing Membership, not an error (IMPLEMENTATION_SPEC.md §2's AcceptInvitation
        // row; API_CONTRACT.md §5: "already Accepted by the same User → 200 not 409").
        if (invitation.Status == InvitationStatus.Accepted && invitation.AcceptedByUserId == requestingUserId)
        {
            var existingMembership = board.FindActiveMembership(requestingUserId);
            if (existingMembership is not null)
                return Result.Success(MembershipResult.FromDomain(board.Id, existingMembership));
        }

        if (invitation.Status != InvitationStatus.Pending)
            return Result.Failure<MembershipResult>(Error.Conflict("Invitation has already been resolved."));

        if (invitation.IsExpired(clock.UtcNow))
            return Result.Failure<MembershipResult>(Error.Conflict("Invitation has expired."));

        invitation.Accept(requestingUserId, clock.UtcNow);
        await invitationRepository.UpdateAsync(invitation, cancellationToken);

        // DOMAIN_MODEL.md's Invitation invariant: acceptance never creates a duplicate
        // Membership if the invitee is already an Active Member via some other path (e.g.
        // a second, independent Invitation to the same Board, accepted after the first).
        if (!board.HasMember(requestingUserId))
        {
            var grantedAt = clock.UtcNow;
            board.GrantMembership(requestingUserId, grantedAt);
            await boardRepository.AddMemberAsync(board.Id, requestingUserId, grantedAt, cancellationToken);
        }

        var membership = board.FindActiveMembership(requestingUserId)!; // just granted above if it wasn't already present
        return Result.Success(MembershipResult.FromDomain(board.Id, membership));
    }

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.5 — Authorization: Authenticated User. `requestingUserId`
    /// is accepted (and enforced at the Api layer's [Authorize]) but not otherwise used in
    /// this method's own logic — same identity-resolution gap as Accept: there is no
    /// invitee-matching check to apply it to.
    /// </summary>
    public async Task<Result> DeclineInvitationAsync(Guid requestingUserId, string token, CancellationToken cancellationToken)
    {
        var invitation = await invitationRepository.GetByTokenAsync(new InvitationToken(token), cancellationToken);
        if (invitation is null)
            return Result.Failure(Error.NotFound("Invitation not found."));

        // Natural idempotency — API_CONTRACT.md §5's Decline row, same convention as
        // RenameBoard's "re-applying same name is a no-op" and LeaveBoard's "already Left".
        if (invitation.Status == InvitationStatus.Declined)
            return Result.Success();

        if (invitation.Status != InvitationStatus.Pending)
            return Result.Failure(Error.Conflict("Invitation has already been resolved."));

        if (invitation.IsExpired(clock.UtcNow))
            return Result.Failure(Error.Conflict("Invitation has expired."));

        invitation.Decline(clock.UtcNow);
        await invitationRepository.UpdateAsync(invitation, cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// DOMAIN_MODEL.md: "the inviter may revoke it" — authorization is specifically the
    /// original inviter, not any Board Member (a narrower rule than InviteMember's own).
    /// No API endpoint exists for this in API_CONTRACT.md (SPRINT_5_REPORT.md's
    /// specification gap) — an internal Application capability only, same posture as
    /// Occurrence's and Buzz's read methods in Sprints 3–4.
    /// </summary>
    public async Task<Result> CancelInvitationAsync(Guid requestingUserId, Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await invitationRepository.GetByIdAsync(new InvitationId(invitationId), cancellationToken);
        if (invitation is null || invitation.InviterUserId != requestingUserId)
            return Result.Failure(Error.NotFound("Invitation not found."));

        // Natural idempotency, mirroring Buzz-cancellation's "cancelling an already-cancelled
        // thing is a no-op" precedent (IMPLEMENTATION_SPEC.md §4's Delete-Cascade policy row).
        if (invitation.Status == InvitationStatus.Revoked)
            return Result.Success();

        if (invitation.Status != InvitationStatus.Pending)
            return Result.Failure(Error.Conflict("Invitation has already been resolved."));

        invitation.Revoke(clock.UtcNow);
        await invitationRepository.UpdateAsync(invitation, cancellationToken);

        return Result.Success();
    }
}
