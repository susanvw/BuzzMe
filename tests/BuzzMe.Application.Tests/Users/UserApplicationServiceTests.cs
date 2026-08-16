using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Application.Users;
using BuzzMe.Domain.Boards;

namespace BuzzMe.Application.Tests.Users;

public sealed class UserApplicationServiceTests
{
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly InMemoryBoardRepository _boardRepository = new();
    private readonly UserApplicationService _sut;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));

    public UserApplicationServiceTests()
    {
        _sut = new UserApplicationService(_userRepository, _boardRepository, new FakeIdGenerator(), _clock);
    }

    [Fact]
    public async Task ProvisionAccountAsync_CreatesTheUserWithAPersonalBoard()
    {
        var userId = Guid.CreateVersion7();

        var result = await _sut.ProvisionAccountAsync(userId, "alice@example.com", null, "Alice", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.Id);
        Assert.Equal("Alice", result.Value.DisplayName);
        Assert.Equal("alice@example.com", result.Value.Email);
        Assert.Equal("active", result.Value.Status);

        var board = await _boardRepository.GetByIdAsync(new BoardId(result.Value.PersonalBoardId), CancellationToken.None);
        Assert.NotNull(board);
        Assert.Equal("Personal", board!.Name.Value);
        Assert.Equal(userId, board.OwnerUserId);
    }

    [Fact]
    public async Task ProvisionAccountAsync_CalledAgainForTheSameUser_IsIdempotent()
    {
        var userId = Guid.CreateVersion7();
        var first = await _sut.ProvisionAccountAsync(userId, "alice@example.com", null, "Alice", CancellationToken.None);

        var second = await _sut.ProvisionAccountAsync(userId, "alice@example.com", null, "Alice", CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Equal(first.Value.PersonalBoardId, second.Value.PersonalBoardId);
    }

    [Fact]
    public async Task ProvisionAccountAsync_ReturnsConflictWhenTheEmailIsAlreadyRegistered()
    {
        await _sut.ProvisionAccountAsync(Guid.CreateVersion7(), "alice@example.com", null, "Alice", CancellationToken.None);

        var result = await _sut.ProvisionAccountAsync(
            Guid.CreateVersion7(), "alice@example.com", null, "Someone Else", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public async Task ProvisionAccountAsync_ReturnsConflictWhenThePhoneIsAlreadyRegistered()
    {
        await _sut.ProvisionAccountAsync(Guid.CreateVersion7(), null, "+15551234567", "Alice", CancellationToken.None);

        var result = await _sut.ProvisionAccountAsync(
            Guid.CreateVersion7(), null, "+15551234567", "Someone Else", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsTheUserWhenProvisioned()
    {
        var userId = Guid.CreateVersion7();
        await _sut.ProvisionAccountAsync(userId, "alice@example.com", null, "Alice", CancellationToken.None);

        var result = await _sut.GetCurrentUserAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.Id);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsNotFoundWhenNeverProvisioned()
    {
        var result = await _sut.GetCurrentUserAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesOnlyTheFieldsProvided()
    {
        var userId = Guid.CreateVersion7();
        await _sut.ProvisionAccountAsync(userId, "alice@example.com", null, "Alice", CancellationToken.None);

        var result = await _sut.UpdateProfileAsync(
            userId, displayName: "Alicia", photoUrl: null, email: null, phone: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Alicia", result.Value.DisplayName);
        Assert.Equal("alice@example.com", result.Value.Email);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsNotFoundForAUserThatWasNeverProvisioned()
    {
        var result = await _sut.UpdateProfileAsync(
            Guid.CreateVersion7(), displayName: "Alicia", photoUrl: null, email: null, phone: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsConflictWhenChangingToAnEmailAlreadyInUse()
    {
        await _sut.ProvisionAccountAsync(Guid.CreateVersion7(), "bob@example.com", null, "Bob", CancellationToken.None);
        var userId = Guid.CreateVersion7();
        await _sut.ProvisionAccountAsync(userId, "alice@example.com", null, "Alice", CancellationToken.None);

        var result = await _sut.UpdateProfileAsync(
            userId, displayName: null, photoUrl: null, email: "bob@example.com", phone: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public async Task UpdateProfileAsync_AllowsReAssertingTheSameEmailTheUserAlreadyHas()
    {
        var userId = Guid.CreateVersion7();
        await _sut.ProvisionAccountAsync(userId, "alice@example.com", null, "Alice", CancellationToken.None);

        var result = await _sut.UpdateProfileAsync(
            userId, displayName: null, photoUrl: null, email: "alice@example.com", phone: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
