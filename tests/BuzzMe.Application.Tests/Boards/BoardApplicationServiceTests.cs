using BuzzMe.Application.Boards;
using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Tests.Boards;

public sealed class BoardApplicationServiceTests
{
    private readonly InMemoryBoardRepository _repository = new();
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly BoardApplicationService _sut;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));

    public BoardApplicationServiceTests()
    {
        _sut = new BoardApplicationService(_repository, _userRepository, new FakeIdGenerator(), _clock);
    }

    /// <summary>Seeds a User whose PersonalBoardId is <paramref name="personalBoardId"/> — LeaveBoardAsync's "cannot leave your Personal Board" check needs this, mirroring UserApplicationServiceTests' own seeding helper.</summary>
    private async Task SeedUserWithPersonalBoardAsync(Guid userId, Guid personalBoardId)
    {
        var user = User.Register(
            new UserId(userId), $"{Guid.CreateVersion7()}@example.com", null, "hashed-password", new DisplayName("Alice"),
            "123456", _clock.UtcNow.AddMinutes(15), _clock.UtcNow);
        user.Verify(_clock.UtcNow);
        user.CompleteProvisioning(new BoardId(personalBoardId));
        await _userRepository.AddAsync(user, CancellationToken.None);
    }

    [Fact]
    public async Task CreateBoardAsync_PersistsTheBoardWithTheCreatorAsOwner()
    {
        var creatorUserId = Guid.CreateVersion7();

        var result = await _sut.CreateBoardAsync(creatorUserId, "Family", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Family", result.Value.Name);
        Assert.Equal(creatorUserId, result.Value.OwnerUserId);
        Assert.Equal(_clock.UtcNow, result.Value.CreatedAt);
    }

    [Fact]
    public async Task GetBoardAsync_ReturnsTheBoardForAMember()
    {
        var creatorUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(creatorUserId, "Family", CancellationToken.None);

        var result = await _sut.GetBoardAsync(creatorUserId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(created.Value.Id, result.Value.Id);
    }

    [Fact]
    public async Task GetBoardAsync_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var creatorUserId = Guid.CreateVersion7();
        var strangerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(creatorUserId, "Family", CancellationToken.None);

        var result = await _sut.GetBoardAsync(strangerUserId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task GetBoardAsync_ReturnsNotFoundForABoardThatDoesNotExist()
    {
        var result = await _sut.GetBoardAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task ListBoardsAsync_OnlyReturnsBoardsTheRequesterBelongsTo()
    {
        var userId = Guid.CreateVersion7();
        var strangerUserId = Guid.CreateVersion7();
        await _sut.CreateBoardAsync(userId, "Family", CancellationToken.None);
        await _sut.CreateBoardAsync(userId, "CrossFit", CancellationToken.None);
        await _sut.CreateBoardAsync(strangerUserId, "Someone Else's Board", CancellationToken.None);

        var result = await _sut.ListBoardsAsync(userId, cursor: null, limit: 20, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.All(result.Value.Items, board => Assert.Equal(userId, board.OwnerUserId));
    }

    [Fact]
    public async Task ListBoardsAsync_ReturnsNoNextCursorWhenFewerThanTheLimitCameBack()
    {
        var userId = Guid.CreateVersion7();
        await _sut.CreateBoardAsync(userId, "Family", CancellationToken.None);

        var result = await _sut.ListBoardsAsync(userId, cursor: null, limit: 20, CancellationToken.None);

        Assert.Null(result.Value.NextCursor);
    }

    [Fact]
    public async Task MuteBoardAsync_MutesTheRequestersOwnMembership()
    {
        var userId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(userId, "Family", CancellationToken.None);

        var result = await _sut.MuteBoardAsync(userId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var board = await _repository.GetByIdAsync(new BoardId(created.Value.Id), CancellationToken.None);
        Assert.True(board!.Memberships.Single(m => m.UserId == userId).Muted);
    }

    [Fact]
    public async Task MuteBoardAsync_CalledAgain_IsIdempotent()
    {
        var userId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(userId, "Family", CancellationToken.None);
        await _sut.MuteBoardAsync(userId, created.Value.Id, CancellationToken.None);

        var second = await _sut.MuteBoardAsync(userId, created.Value.Id, CancellationToken.None);

        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task MuteBoardAsync_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var userId = Guid.CreateVersion7();
        var strangerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(userId, "Family", CancellationToken.None);

        var result = await _sut.MuteBoardAsync(strangerUserId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task MuteBoardAsync_ReturnsNotFoundForABoardThatDoesNotExist()
    {
        var result = await _sut.MuteBoardAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task UnmuteBoardAsync_UnmutesTheRequestersOwnMembership()
    {
        var userId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(userId, "Family", CancellationToken.None);
        await _sut.MuteBoardAsync(userId, created.Value.Id, CancellationToken.None);

        var result = await _sut.UnmuteBoardAsync(userId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var board = await _repository.GetByIdAsync(new BoardId(created.Value.Id), CancellationToken.None);
        Assert.False(board!.Memberships.Single(m => m.UserId == userId).Muted);
    }

    [Fact]
    public async Task UnmuteBoardAsync_CalledAgain_IsIdempotent()
    {
        var userId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(userId, "Family", CancellationToken.None);

        var result = await _sut.UnmuteBoardAsync(userId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UnmuteBoardAsync_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var userId = Guid.CreateVersion7();
        var strangerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(userId, "Family", CancellationToken.None);

        var result = await _sut.UnmuteBoardAsync(strangerUserId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task LeaveBoardAsync_NonOwnerMember_RemovesTheirMembershipWithNoReassignment()
    {
        var ownerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);
        var leavingUserId = Guid.CreateVersion7();
        var board = await _repository.GetByIdAsync(new BoardId(created.Value.Id), CancellationToken.None);
        board!.GrantMembership(leavingUserId, _clock.UtcNow);

        var result = await _sut.LeaveBoardAsync(leavingUserId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ReassignedOwnerUserId);
        Assert.False(board.HasMember(leavingUserId));
    }

    [Fact]
    public async Task LeaveBoardAsync_SoleOwnerWithOtherMembers_ReturnsTheReassignedOwner()
    {
        var ownerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);
        var otherMemberUserId = Guid.CreateVersion7();
        var board = await _repository.GetByIdAsync(new BoardId(created.Value.Id), CancellationToken.None);
        board!.GrantMembership(otherMemberUserId, _clock.UtcNow);

        var result = await _sut.LeaveBoardAsync(ownerUserId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(otherMemberUserId, result.Value.ReassignedOwnerUserId);
        Assert.Equal(otherMemberUserId, board.OwnerUserId);
    }

    [Fact]
    public async Task LeaveBoardAsync_SoleMemberEntirely_ReturnsForbidden()
    {
        var ownerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);

        var result = await _sut.LeaveBoardAsync(ownerUserId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error.Code);
    }

    [Fact]
    public async Task LeaveBoardAsync_ReturnsForbiddenForThePersonalBoard()
    {
        var ownerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Personal", CancellationToken.None);
        var otherMemberUserId = Guid.CreateVersion7();
        var board = await _repository.GetByIdAsync(new BoardId(created.Value.Id), CancellationToken.None);
        board!.GrantMembership(otherMemberUserId, _clock.UtcNow);
        await SeedUserWithPersonalBoardAsync(ownerUserId, created.Value.Id);

        var result = await _sut.LeaveBoardAsync(ownerUserId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error.Code);
    }

    [Fact]
    public async Task LeaveBoardAsync_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var ownerUserId = Guid.CreateVersion7();
        var strangerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);

        var result = await _sut.LeaveBoardAsync(strangerUserId, created.Value.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task LeaveBoardAsync_ReturnsNotFoundForABoardThatDoesNotExist()
    {
        var result = await _sut.LeaveBoardAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task LeaveBoardAsync_CalledAgain_IsIdempotent()
    {
        var ownerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);
        var leavingUserId = Guid.CreateVersion7();
        var board = await _repository.GetByIdAsync(new BoardId(created.Value.Id), CancellationToken.None);
        board!.GrantMembership(leavingUserId, _clock.UtcNow);
        await _sut.LeaveBoardAsync(leavingUserId, created.Value.Id, CancellationToken.None);

        var second = await _sut.LeaveBoardAsync(leavingUserId, created.Value.Id, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Null(second.Value.ReassignedOwnerUserId);
    }

    [Fact]
    public async Task RemoveMemberAsync_OwnerRemovesAMember_Succeeds()
    {
        var ownerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);
        var targetUserId = Guid.CreateVersion7();
        var board = await _repository.GetByIdAsync(new BoardId(created.Value.Id), CancellationToken.None);
        board!.GrantMembership(targetUserId, _clock.UtcNow);

        var result = await _sut.RemoveMemberAsync(ownerUserId, created.Value.Id, targetUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(board.HasMember(targetUserId));
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsForbiddenForANonOwner()
    {
        var ownerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);
        var nonOwnerUserId = Guid.CreateVersion7();
        var targetUserId = Guid.CreateVersion7();
        var board = await _repository.GetByIdAsync(new BoardId(created.Value.Id), CancellationToken.None);
        board!.GrantMembership(nonOwnerUserId, _clock.UtcNow);
        board.GrantMembership(targetUserId, _clock.UtcNow);

        var result = await _sut.RemoveMemberAsync(nonOwnerUserId, created.Value.Id, targetUserId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsConflictWhenTargetingSelf()
    {
        var ownerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);

        var result = await _sut.RemoveMemberAsync(ownerUserId, created.Value.Id, ownerUserId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsNotFoundWhenTheTargetWasNeverAMember()
    {
        var ownerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);

        var result = await _sut.RemoveMemberAsync(ownerUserId, created.Value.Id, Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_CalledAgain_IsIdempotent()
    {
        var ownerUserId = Guid.CreateVersion7();
        var created = await _sut.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);
        var targetUserId = Guid.CreateVersion7();
        var board = await _repository.GetByIdAsync(new BoardId(created.Value.Id), CancellationToken.None);
        board!.GrantMembership(targetUserId, _clock.UtcNow);
        await _sut.RemoveMemberAsync(ownerUserId, created.Value.Id, targetUserId, CancellationToken.None);

        var second = await _sut.RemoveMemberAsync(ownerUserId, created.Value.Id, targetUserId, CancellationToken.None);

        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsNotFoundForABoardThatDoesNotExist()
    {
        var result = await _sut.RemoveMemberAsync(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }
}
