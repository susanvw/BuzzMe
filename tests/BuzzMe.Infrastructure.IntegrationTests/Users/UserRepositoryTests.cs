using BuzzMe.Domain.Users;
using BuzzMe.Infrastructure.Persistence.Migrations.Steps;
using BuzzMe.Infrastructure.Persistence.Mongo.Users;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.IntegrationTests.Users;

/// <summary>
/// Against a real, ephemeral MongoDB (MongoIntegrationTestFixture) — Sprint 1's explicit
/// "do not mock MongoDB repositories."
/// </summary>
[Collection(MongoIntegrationTestCollection.Name)]
public sealed class UserRepositoryTests(MongoIntegrationTestFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private UserRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _repository = new UserRepository(fixture.Context);
        await new CreateUserIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
        await new CreateUserPasswordResetIndex(fixture.Context).ApplyAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // The Mongo container is shared across every test in this collection (no per-test
    // database reset), so email/phone — both globally unique — must never repeat a literal
    // value across test methods; each call here mints a fresh one, same reasoning as
    // InvitationRepositoryTests generating a fresh token per test.
    private static string UniqueEmail() => $"{Guid.CreateVersion7()}@example.com";

    private static string UniquePhone() => $"+1555{Random.Shared.Next(1000000, 9999999)}";

    private static User NewUser(Guid? id = null, string? email = null, string? phone = null, string displayName = "Alice") =>
        User.Register(
            new UserId(id ?? Guid.CreateVersion7()), email ?? (phone is null ? UniqueEmail() : null), phone,
            "hashed-password", new DisplayName(displayName), "123456", Now + TimeSpan.FromMinutes(15), Now);

    [Fact]
    public async Task AddAsync_PersistsTheUserAtVersionZero()
    {
        var email = UniqueEmail();
        var user = NewUser(email: email);

        await _repository.AddAsync(user, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(user.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.Version);
        Assert.Equal(email, reloaded.Email);
        Assert.Equal("Alice", reloaded.DisplayName.Value);
        Assert.Equal(UserStatus.PendingVerification, reloaded.Status);
        Assert.Null(reloaded.PersonalBoardId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForAnUnknownId()
    {
        var result = await _repository.GetByIdAsync(new UserId(Guid.CreateVersion7()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_RejectsADuplicateEmailAtTheDatabaseLevel()
    {
        // The sparse unique index is the real enforcement of the email-uniqueness invariant
        // (IMPLEMENTATION_SPEC.md §1) — verified here at the database level, same pattern as
        // InvitationRepositoryTests' own duplicate-key test.
        var email = UniqueEmail();
        await _repository.AddAsync(NewUser(email: email), CancellationToken.None);

        var duplicate = NewUser(email: email);
        await Assert.ThrowsAsync<MongoWriteException>(() => _repository.AddAsync(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_RejectsADuplicatePhoneAtTheDatabaseLevel()
    {
        var phone = UniquePhone();
        await _repository.AddAsync(NewUser(email: null, phone: phone), CancellationToken.None);

        var duplicate = NewUser(email: null, phone: phone);
        await Assert.ThrowsAsync<MongoWriteException>(() => _repository.AddAsync(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_AllowsTwoUsersWithNoEmailAtAll()
    {
        // The sparse index must not treat "email absent" as a colliding null across Users.
        await _repository.AddAsync(NewUser(email: null, phone: UniquePhone()), CancellationToken.None);

        await _repository.AddAsync(NewUser(email: null, phone: UniquePhone()), CancellationToken.None);
    }

    [Fact]
    public async Task ExistsWithEmailOrPhoneAsync_ReturnsTrueWhenTheEmailIsTaken()
    {
        var email = UniqueEmail();
        await _repository.AddAsync(NewUser(email: email), CancellationToken.None);

        var result = await _repository.ExistsWithEmailOrPhoneAsync(
            email, null, excludingUserId: null, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsWithEmailOrPhoneAsync_ReturnsFalseWhenNeitherIsTaken()
    {
        var result = await _repository.ExistsWithEmailOrPhoneAsync(
            UniqueEmail(), UniquePhone(), excludingUserId: null, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ExistsWithEmailOrPhoneAsync_ExcludesTheGivenUserId()
    {
        var email = UniqueEmail();
        var user = NewUser(email: email);
        await _repository.AddAsync(user, CancellationToken.None);

        var result = await _repository.ExistsWithEmailOrPhoneAsync(
            email, null, excludingUserId: user.Id, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task GetByEmailOrPhoneAsync_FindsTheUserByEmail()
    {
        var email = UniqueEmail();
        var user = NewUser(email: email);
        await _repository.AddAsync(user, CancellationToken.None);

        var result = await _repository.GetByEmailOrPhoneAsync(email, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task GetByEmailOrPhoneAsync_FindsTheUserByPhone()
    {
        var phone = UniquePhone();
        var user = NewUser(email: null, phone: phone);
        await _repository.AddAsync(user, CancellationToken.None);

        var result = await _repository.GetByEmailOrPhoneAsync(null, phone, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task GetByEmailOrPhoneAsync_ReturnsNullWhenNeitherMatches()
    {
        var result = await _repository.GetByEmailOrPhoneAsync(UniqueEmail(), UniquePhone(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByPasswordResetTokenHashAsync_FindsTheMatchingUser()
    {
        var user = NewUser();
        user.Verify(Now);
        user.RequestPasswordReset("a-token-hash", Now + TimeSpan.FromHours(1), Now);
        await _repository.AddAsync(user, CancellationToken.None);

        var result = await _repository.GetByPasswordResetTokenHashAsync("a-token-hash", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task GetByPasswordResetTokenHashAsync_ReturnsNullWhenNoUserHasThatToken()
    {
        var result = await _repository.GetByPasswordResetTokenHashAsync("no-such-token-hash", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_AllowsTwoUsersWithNoOutstandingPasswordResetToken()
    {
        // The sparse index must not treat "no reset token" as a colliding null across Users — same lesson as the Email/Phone sparse indexes.
        await _repository.AddAsync(NewUser(email: UniqueEmail()), CancellationToken.None);

        await _repository.AddAsync(NewUser(email: UniqueEmail()), CancellationToken.None);
    }

    [Fact]
    public async Task UpdateAsync_PersistsProfileChanges()
    {
        var user = NewUser();
        await _repository.AddAsync(user, CancellationToken.None);

        user.UpdateProfile(new DisplayName("Alicia"), photoUrl: "https://example.com/photo.jpg", email: null, phone: null, Now);
        await _repository.UpdateAsync(user, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(user.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal("Alicia", reloaded.DisplayName.Value);
        Assert.Equal("https://example.com/photo.jpg", reloaded.PhotoUrl);
    }

    [Fact]
    public async Task UpdateAsync_PersistsVerificationAndProvisioning()
    {
        var user = NewUser();
        await _repository.AddAsync(user, CancellationToken.None);

        user.Verify(Now);
        await _repository.UpdateAsync(user, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(user.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(UserStatus.Active, reloaded.Status);
        Assert.Null(reloaded.VerificationCode);
    }
}
