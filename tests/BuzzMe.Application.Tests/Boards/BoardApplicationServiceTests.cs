using BuzzMe.Application.Boards;
using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Domain.Boards;

namespace BuzzMe.Application.Tests.Boards;

public sealed class BoardApplicationServiceTests
{
    private readonly InMemoryBoardRepository _repository = new();
    private readonly BoardApplicationService _sut;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));

    public BoardApplicationServiceTests()
    {
        _sut = new BoardApplicationService(_repository, new FakeIdGenerator(), _clock);
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
}
