using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Application.Users;
using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Tests.Users;

public sealed class UserApplicationServiceTests
{
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly UserApplicationService _sut;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));

    public UserApplicationServiceTests()
    {
        _sut = new UserApplicationService(_userRepository, _clock);
    }

    /// <summary>Registration/verification are AuthApplicationService's job (AuthApplicationServiceTests) — Users tests seed an already-Active User directly through the aggregate, same as this codebase's other bounded-context tests seed their own prerequisite state.</summary>
    private async Task<Guid> SeedActiveUserAsync(string? email = "alice@example.com", string? phone = null, string displayName = "Alice")
    {
        var user = User.Register(
            new UserId(Guid.CreateVersion7()), email, phone, "hashed-password", new DisplayName(displayName),
            "123456", _clock.UtcNow.AddMinutes(15), _clock.UtcNow);
        user.Verify(_clock.UtcNow);
        await _userRepository.AddAsync(user, CancellationToken.None);
        return user.Id.Value;
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsTheUserWhenOneExists()
    {
        var userId = await SeedActiveUserAsync();

        var result = await _sut.GetCurrentUserAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.Id);
        Assert.Equal("Alice", result.Value.DisplayName);
        Assert.Equal("alice@example.com", result.Value.Email);
        Assert.Equal("active", result.Value.Status);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsNotFoundWhenNoSuchUserExists()
    {
        var result = await _sut.GetCurrentUserAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesOnlyTheFieldsProvided()
    {
        var userId = await SeedActiveUserAsync();

        var result = await _sut.UpdateProfileAsync(
            userId, displayName: "Alicia", photoUrl: null, email: null, phone: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Alicia", result.Value.DisplayName);
        Assert.Equal("alice@example.com", result.Value.Email);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsNotFoundForAUserThatDoesNotExist()
    {
        var result = await _sut.UpdateProfileAsync(
            Guid.CreateVersion7(), displayName: "Alicia", photoUrl: null, email: null, phone: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsConflictWhenChangingToAnEmailAlreadyInUse()
    {
        await SeedActiveUserAsync(email: "bob@example.com", displayName: "Bob");
        var userId = await SeedActiveUserAsync();

        var result = await _sut.UpdateProfileAsync(
            userId, displayName: null, photoUrl: null, email: "bob@example.com", phone: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public async Task UpdateProfileAsync_AllowsReAssertingTheSameEmailTheUserAlreadyHas()
    {
        var userId = await SeedActiveUserAsync();

        var result = await _sut.UpdateProfileAsync(
            userId, displayName: null, photoUrl: null, email: "alice@example.com", phone: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
