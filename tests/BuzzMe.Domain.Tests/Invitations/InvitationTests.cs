using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Invitations;
using BuzzMe.Domain.Invitations.Events;

namespace BuzzMe.Domain.Tests.Invitations;

public sealed class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly BoardId SomeBoardId = new(Guid.CreateVersion7());

    private static Invitation NewPendingInvitation(DateTimeOffset? expiresAt = null, Guid? inviterUserId = null) =>
        Invitation.Send(
            new InvitationId(Guid.CreateVersion7()),
            new InvitationToken("test-token"),
            SomeBoardId,
            inviterUserId ?? Guid.CreateVersion7(),
            InvitationChannel.Link,
            targetContact: null,
            expiresAt ?? Now.AddDays(7),
            Now);

    [Fact]
    public void Send_StampsTheGivenFieldsAndStartsPending()
    {
        var invitationId = new InvitationId(Guid.CreateVersion7());
        var token = new InvitationToken("abc123");
        var inviterUserId = Guid.CreateVersion7();
        var expiresAt = Now.AddDays(7);

        var invitation = Invitation.Send(invitationId, token, SomeBoardId, inviterUserId, InvitationChannel.Email, "a@b.com", expiresAt, Now);

        Assert.Equal(invitationId, invitation.Id);
        Assert.Equal(token, invitation.Token);
        Assert.Equal(SomeBoardId, invitation.BoardId);
        Assert.Equal(inviterUserId, invitation.InviterUserId);
        Assert.Equal(InvitationChannel.Email, invitation.Channel);
        Assert.Equal("a@b.com", invitation.TargetContact);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Equal(Now, invitation.CreatedAt);
        Assert.Equal(expiresAt, invitation.ExpiresAt);
        Assert.Null(invitation.AcceptedByUserId);
        Assert.Null(invitation.ResolvedAt);
    }

    [Fact]
    public void Send_RaisesInvitationSent()
    {
        var invitationId = new InvitationId(Guid.CreateVersion7());
        var token = new InvitationToken("abc123");
        var inviterUserId = Guid.CreateVersion7();

        var invitation = Invitation.Send(invitationId, token, SomeBoardId, inviterUserId, InvitationChannel.Link, null, Now.AddDays(7), Now);

        var raised = Assert.Single(invitation.DomainEvents.OfType<InvitationSent>());
        Assert.Equal(invitationId, raised.InvitationId);
        Assert.Equal(token, raised.Token);
        Assert.Equal(SomeBoardId, raised.BoardId);
        Assert.Equal(inviterUserId, raised.InviterUserId);
    }

    [Fact]
    public void IsExpired_ReturnsFalseBeforeExpiresAt()
    {
        var invitation = NewPendingInvitation(expiresAt: Now.AddDays(1));

        Assert.False(invitation.IsExpired(Now));
    }

    [Fact]
    public void IsExpired_ReturnsTrueAtOrAfterExpiresAt()
    {
        var invitation = NewPendingInvitation(expiresAt: Now.AddDays(1));

        Assert.True(invitation.IsExpired(Now.AddDays(1)));
        Assert.True(invitation.IsExpired(Now.AddDays(2)));
    }

    [Fact]
    public void IsExpired_ReturnsFalseForANonPendingInvitationRegardlessOfExpiresAt()
    {
        var invitation = NewPendingInvitation(expiresAt: Now.AddDays(-1));
        invitation.Decline(Now);

        Assert.False(invitation.IsExpired(Now.AddDays(1)));
    }

    [Fact]
    public void Accept_TransitionsToAcceptedAndRecordsTheAcceptingUser()
    {
        var invitation = NewPendingInvitation();
        var acceptingUserId = Guid.CreateVersion7();

        invitation.Accept(acceptingUserId, Now);

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(acceptingUserId, invitation.AcceptedByUserId);
        Assert.Equal(Now, invitation.ResolvedAt);
    }

    [Fact]
    public void Accept_RaisesInvitationAccepted()
    {
        var invitation = NewPendingInvitation();
        var acceptingUserId = Guid.CreateVersion7();

        invitation.Accept(acceptingUserId, Now);

        var raised = Assert.Single(invitation.DomainEvents.OfType<InvitationAccepted>());
        Assert.Equal(invitation.Id, raised.InvitationId);
        Assert.Equal(acceptingUserId, raised.AcceptedByUserId);
    }

    [Fact]
    public void Accept_ThrowsWhenTheInvitationIsNotPending()
    {
        var invitation = NewPendingInvitation();
        invitation.Decline(Now);

        Assert.Throws<InvalidOperationException>(() => invitation.Accept(Guid.CreateVersion7(), Now));
    }

    [Fact]
    public void Decline_TransitionsToDeclined()
    {
        var invitation = NewPendingInvitation();

        invitation.Decline(Now);

        Assert.Equal(InvitationStatus.Declined, invitation.Status);
        Assert.Equal(Now, invitation.ResolvedAt);
    }

    [Fact]
    public void Decline_ThrowsWhenTheInvitationIsNotPending()
    {
        var invitation = NewPendingInvitation();
        invitation.Decline(Now);

        Assert.Throws<InvalidOperationException>(() => invitation.Decline(Now));
    }

    [Fact]
    public void Revoke_TransitionsToRevoked()
    {
        var invitation = NewPendingInvitation();

        invitation.Revoke(Now);

        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
        Assert.Equal(Now, invitation.ResolvedAt);
    }

    [Fact]
    public void Revoke_RaisesInvitationRevoked()
    {
        var invitation = NewPendingInvitation();

        invitation.Revoke(Now);

        var raised = Assert.Single(invitation.DomainEvents.OfType<InvitationRevoked>());
        Assert.Equal(invitation.Id, raised.InvitationId);
    }

    [Fact]
    public void Revoke_ThrowsWhenTheInvitationIsNotPending()
    {
        var invitation = NewPendingInvitation();
        invitation.Accept(Guid.CreateVersion7(), Now);

        Assert.Throws<InvalidOperationException>(() => invitation.Revoke(Now));
    }

    [Theory]
    [InlineData("pending", InvitationStatus.Pending)]
    [InlineData("accepted", InvitationStatus.Accepted)]
    [InlineData("declined", InvitationStatus.Declined)]
    [InlineData("expired", InvitationStatus.Expired)]
    [InlineData("revoked", InvitationStatus.Revoked)]
    public void InvitationStatusCodes_RoundTripEveryValue(string code, InvitationStatus expected)
    {
        Assert.True(InvitationStatusCodes.TryParse(code, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(code, expected.ToCode());
    }

    [Fact]
    public void InvitationStatusCodes_RejectsAnUnknownCode()
    {
        Assert.False(InvitationStatusCodes.TryParse("cancelled", out _));
    }

    [Theory]
    [InlineData("link", InvitationChannel.Link)]
    [InlineData("email", InvitationChannel.Email)]
    [InlineData("sms", InvitationChannel.Sms)]
    public void InvitationChannelCodes_RoundTripEveryValue(string code, InvitationChannel expected)
    {
        Assert.True(InvitationChannelCodes.TryParse(code, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(code, expected.ToCode());
    }

    [Fact]
    public void InvitationChannelCodes_RejectsAnUnknownCode()
    {
        Assert.False(InvitationChannelCodes.TryParse("whatsapp", out _));
    }
}
