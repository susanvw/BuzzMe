using BuzzMe.Application.Invitations;
using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Domain.Boards;

namespace BuzzMe.Application.Tests.Invitations;

public sealed class InvitationApplicationServiceTests
{
    private readonly InMemoryBoardRepository _boardRepository = new();
    private readonly InMemoryInvitationRepository _invitationRepository = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly InvitationApplicationService _sut;
    private readonly Guid _ownerUserId = Guid.CreateVersion7();
    private readonly Board _board;

    public InvitationApplicationServiceTests()
    {
        _sut = new InvitationApplicationService(
            _invitationRepository, _boardRepository, new FakeInvitationTokenGenerator(), new FakeIdGenerator(), _clock);

        _board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), _ownerUserId, _clock.UtcNow);
        _boardRepository.AddAsync(_board, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task<string> InviteAsync(Guid? requestingUserId = null) =>
        (await _sut.InviteMemberAsync(requestingUserId ?? _ownerUserId, _board.Id.Value, "link", null, CancellationToken.None)).Value.Token;

    [Fact]
    public async Task InviteMemberAsync_CreatesAPendingInvitation()
    {
        var result = await _sut.InviteMemberAsync(_ownerUserId, _board.Id.Value, "link", null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
        Assert.Equal(_board.Id.Value, result.Value.BoardId);
        Assert.Equal("Family", result.Value.BoardName);
    }

    [Fact]
    public async Task InviteMemberAsync_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var result = await _sut.InviteMemberAsync(Guid.CreateVersion7(), _board.Id.Value, "link", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task InviteMemberAsync_ReturnsValidationErrorForAnUnknownChannel()
    {
        var result = await _sut.InviteMemberAsync(_ownerUserId, _board.Id.Value, "carrier-pigeon", null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task ValidateInvitationAsync_ReturnsTheInvitationForAKnownToken()
    {
        var token = await InviteAsync();

        var result = await _sut.ValidateInvitationAsync(token, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
    }

    [Fact]
    public async Task ValidateInvitationAsync_ReturnsNotFoundForAnUnknownToken()
    {
        var result = await _sut.ValidateInvitationAsync("does-not-exist", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task ValidateInvitationAsync_ReportsExpiredStatusPastExpiresAtWithoutAnyoneHavingActedOnIt()
    {
        var token = await InviteAsync();
        _clock.Advance(TimeSpan.FromDays(8));

        var result = await _sut.ValidateInvitationAsync(token, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("expired", result.Value.Status);
    }

    [Fact]
    public async Task AcceptInvitationAsync_GrantsMembershipOnTheBoard()
    {
        var token = await InviteAsync();
        var inviteeUserId = Guid.CreateVersion7();

        var result = await _sut.AcceptInvitationAsync(inviteeUserId, token, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(inviteeUserId, result.Value.UserId);
        Assert.Equal("Member", result.Value.Role);
        Assert.True(_board.HasMember(inviteeUserId));
    }

    [Fact]
    public async Task AcceptInvitationAsync_CalledAgainBySameUser_IsIdempotent()
    {
        var token = await InviteAsync();
        var inviteeUserId = Guid.CreateVersion7();
        await _sut.AcceptInvitationAsync(inviteeUserId, token, CancellationToken.None);

        var second = await _sut.AcceptInvitationAsync(inviteeUserId, token, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Single(_board.Memberships, m => m.UserId == inviteeUserId);
    }

    [Fact]
    public async Task AcceptInvitationAsync_ReturnsConflictWhenAlreadyAcceptedBySomeoneElse()
    {
        var token = await InviteAsync();
        await _sut.AcceptInvitationAsync(Guid.CreateVersion7(), token, CancellationToken.None);

        var result = await _sut.AcceptInvitationAsync(Guid.CreateVersion7(), token, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public async Task AcceptInvitationAsync_ReturnsConflictForAnExpiredInvitation()
    {
        var token = await InviteAsync();
        _clock.Advance(TimeSpan.FromDays(8));

        var result = await _sut.AcceptInvitationAsync(Guid.CreateVersion7(), token, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public async Task AcceptInvitationAsync_ReturnsNotFoundForAnUnknownToken()
    {
        var result = await _sut.AcceptInvitationAsync(Guid.CreateVersion7(), "does-not-exist", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task DeclineInvitationAsync_Succeeds()
    {
        var token = await InviteAsync();

        var result = await _sut.DeclineInvitationAsync(Guid.CreateVersion7(), token, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeclineInvitationAsync_CalledAgain_IsIdempotent()
    {
        var token = await InviteAsync();
        await _sut.DeclineInvitationAsync(Guid.CreateVersion7(), token, CancellationToken.None);

        var second = await _sut.DeclineInvitationAsync(Guid.CreateVersion7(), token, CancellationToken.None);

        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task DeclineInvitationAsync_ReturnsConflictWhenAlreadyAccepted()
    {
        var token = await InviteAsync();
        await _sut.AcceptInvitationAsync(Guid.CreateVersion7(), token, CancellationToken.None);

        var result = await _sut.DeclineInvitationAsync(Guid.CreateVersion7(), token, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public async Task CancelInvitationAsync_SucceedsForTheOriginalInviter()
    {
        var invitationResult = await _sut.InviteMemberAsync(_ownerUserId, _board.Id.Value, "link", null, CancellationToken.None);

        var result = await _sut.CancelInvitationAsync(_ownerUserId, invitationResult.Value.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CancelInvitationAsync_ReturnsNotFoundForSomeoneWhoIsNotTheInviter()
    {
        var invitationResult = await _sut.InviteMemberAsync(_ownerUserId, _board.Id.Value, "link", null, CancellationToken.None);

        var result = await _sut.CancelInvitationAsync(Guid.CreateVersion7(), invitationResult.Value.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task CancelInvitationAsync_CalledAgain_IsIdempotent()
    {
        var invitationResult = await _sut.InviteMemberAsync(_ownerUserId, _board.Id.Value, "link", null, CancellationToken.None);
        await _sut.CancelInvitationAsync(_ownerUserId, invitationResult.Value.Id, CancellationToken.None);

        var second = await _sut.CancelInvitationAsync(_ownerUserId, invitationResult.Value.Id, CancellationToken.None);

        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task CancelInvitationAsync_ReturnsConflictWhenAlreadyAccepted()
    {
        var invitationResult = await _sut.InviteMemberAsync(_ownerUserId, _board.Id.Value, "link", null, CancellationToken.None);
        await _sut.AcceptInvitationAsync(Guid.CreateVersion7(), invitationResult.Value.Token, CancellationToken.None);

        var result = await _sut.CancelInvitationAsync(_ownerUserId, invitationResult.Value.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }
}
