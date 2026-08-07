using BuzzMe.Domain.Boards;
using BuzzMe.Infrastructure.Persistence.Migrations.Steps;
using BuzzMe.Infrastructure.Persistence.Mongo.Boards;

namespace BuzzMe.Infrastructure.IntegrationTests.Boards;

/// <summary>
/// Against a real, ephemeral MongoDB (MongoIntegrationTestFixture) — Sprint 1's explicit
/// "do not mock MongoDB repositories."
/// </summary>
[Collection(MongoIntegrationTestCollection.Name)]
public sealed class BoardRepositoryTests(MongoIntegrationTestFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private BoardRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _repository = new BoardRepository(fixture.Context);
        await new CreateBoardIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddAsync_PersistsTheBoardAtVersionZero()
    {
        var creatorUserId = Guid.CreateVersion7();
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), creatorUserId, Now);

        await _repository.AddAsync(board, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(board.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.Version);
        Assert.Equal("Family", reloaded.Name.Value);
        Assert.Equal(creatorUserId, reloaded.OwnerUserId);
        Assert.True(reloaded.HasMember(creatorUserId));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForAnUnknownId()
    {
        var result = await _repository.GetByIdAsync(new BoardId(Guid.CreateVersion7()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListByMemberAsync_OnlyReturnsBoardsTheGivenUserBelongsTo()
    {
        var userId = Guid.CreateVersion7();
        var strangerUserId = Guid.CreateVersion7();

        var ownBoard = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), userId, Now);
        var strangersBoard = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Someone Else's"), strangerUserId, Now);
        await _repository.AddAsync(ownBoard, CancellationToken.None);
        await _repository.AddAsync(strangersBoard, CancellationToken.None);

        var results = await _repository.ListByMemberAsync(userId, afterId: null, limit: 20, CancellationToken.None);

        var found = Assert.Single(results);
        Assert.Equal(ownBoard.Id, found.Id);
    }

    [Fact]
    public async Task ListByMemberAsync_RespectsTheCursorAndLimit()
    {
        var userId = Guid.CreateVersion7();
        var boardIds = new List<BoardId>();
        for (var i = 0; i < 3; i++)
        {
            var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName($"Board {i}"), userId, Now);
            boardIds.Add(board.Id);
            await _repository.AddAsync(board, CancellationToken.None);
        }

        var firstPage = await _repository.ListByMemberAsync(userId, afterId: null, limit: 2, CancellationToken.None);
        Assert.Equal(2, firstPage.Count);

        var secondPage = await _repository.ListByMemberAsync(userId, afterId: firstPage[^1].Id.Value, limit: 2, CancellationToken.None);
        Assert.Single(secondPage);
        Assert.DoesNotContain(secondPage, board => firstPage.Select(item => item.Id).Contains(board.Id));
    }

    [Fact]
    public async Task AddMemberAsync_AddsANewMemberWithTheMemberRole()
    {
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), Guid.CreateVersion7(), Now);
        await _repository.AddAsync(board, CancellationToken.None);
        var newMemberUserId = Guid.CreateVersion7();

        await _repository.AddMemberAsync(board.Id, newMemberUserId, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(board.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.Memberships.Count);
        Assert.True(reloaded.HasMember(newMemberUserId));
        Assert.Equal(MembershipRole.Member, reloaded.Memberships.Single(m => m.UserId == newMemberUserId).Role);
    }
}
