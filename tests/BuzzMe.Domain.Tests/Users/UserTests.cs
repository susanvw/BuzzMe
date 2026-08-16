using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Users;
using BuzzMe.Domain.Users.Events;

namespace BuzzMe.Domain.Tests.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly BoardId SomePersonalBoardId = new(Guid.CreateVersion7());

    private static User NewActiveUser(string? email = "person@example.com", string? phone = null) =>
        User.Provision(new UserId(Guid.CreateVersion7()), email, phone, new DisplayName("Emma"), SomePersonalBoardId, Now);

    [Fact]
    public void Provision_StampsTheGivenFieldsAndStartsActive()
    {
        var userId = new UserId(Guid.CreateVersion7());

        var user = User.Provision(userId, "person@example.com", null, new DisplayName("Emma"), SomePersonalBoardId, Now);

        Assert.Equal(userId, user.Id);
        Assert.Equal("person@example.com", user.Email);
        Assert.Null(user.Phone);
        Assert.Equal("Emma", user.DisplayName.Value);
        Assert.Null(user.PhotoUrl);
        Assert.Equal(SomePersonalBoardId, user.PersonalBoardId);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void Provision_RaisesUserAccountProvisioned()
    {
        var userId = new UserId(Guid.CreateVersion7());

        var user = User.Provision(userId, "person@example.com", null, new DisplayName("Emma"), SomePersonalBoardId, Now);

        var raised = Assert.Single(user.DomainEvents.OfType<UserAccountProvisioned>());
        Assert.Equal(userId, raised.UserId);
        Assert.Equal(SomePersonalBoardId, raised.PersonalBoardId);
    }

    [Fact]
    public void Provision_AllowsPhoneOnlyWithNoEmail()
    {
        var user = User.Provision(
            new UserId(Guid.CreateVersion7()), email: null, phone: "+15551234567", new DisplayName("Emma"), SomePersonalBoardId, Now);

        Assert.Null(user.Email);
        Assert.Equal("+15551234567", user.Phone);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(null, "  ")]
    public void Provision_ThrowsWhenNeitherEmailNorPhoneIsGiven(string? email, string? phone)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Provision(new UserId(Guid.CreateVersion7()), email, phone, new DisplayName("Emma"), SomePersonalBoardId, Now));
    }

    [Fact]
    public void UpdateProfile_ChangesOnlyTheGivenFields()
    {
        var user = NewActiveUser();

        user.UpdateProfile(new DisplayName("Emma R."), photoUrl: "https://example.com/photo.png", email: null, phone: null, Now);

        Assert.Equal("Emma R.", user.DisplayName.Value);
        Assert.Equal("https://example.com/photo.png", user.PhotoUrl);
        Assert.Equal("person@example.com", user.Email);
    }

    [Fact]
    public void UpdateProfile_RaisesProfileUpdatedWhenSomethingActuallyChanged()
    {
        var user = NewActiveUser();

        user.UpdateProfile(new DisplayName("Emma R."), photoUrl: null, email: null, phone: null, Now);

        var raised = Assert.Single(user.DomainEvents.OfType<ProfileUpdated>());
        Assert.Equal(user.Id, raised.UserId);
    }

    [Fact]
    public void UpdateProfile_IsANoOpWhenTheGivenValuesAreAllIdentical()
    {
        var user = NewActiveUser();

        user.UpdateProfile(new DisplayName("Emma"), photoUrl: null, email: "person@example.com", phone: null, Now);

        Assert.Empty(user.DomainEvents.OfType<ProfileUpdated>());
    }

    [Fact]
    public void UpdateProfile_CanChangeEmail()
    {
        var user = NewActiveUser();

        user.UpdateProfile(displayName: null, photoUrl: null, email: "new@example.com", phone: null, Now);

        Assert.Equal("new@example.com", user.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DisplayName_RejectsAnEmptyValue(string invalidName)
    {
        Assert.Throws<ArgumentException>(() => new DisplayName(invalidName));
    }

    [Fact]
    public void DisplayName_TrimsSurroundingWhitespace()
    {
        var name = new DisplayName("  Emma  ");

        Assert.Equal("Emma", name.Value);
    }

    [Theory]
    [InlineData("pendingVerification", UserStatus.PendingVerification)]
    [InlineData("active", UserStatus.Active)]
    [InlineData("deactivated", UserStatus.Deactivated)]
    [InlineData("suspended", UserStatus.Suspended)]
    [InlineData("deleted", UserStatus.Deleted)]
    public void UserStatusCodes_RoundTripEveryValue(string code, UserStatus expected)
    {
        Assert.True(UserStatusCodes.TryParse(code, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(code, expected.ToCode());
    }

    [Fact]
    public void UserStatusCodes_RejectsAnUnknownCode()
    {
        Assert.False(UserStatusCodes.TryParse("banned", out _));
    }
}
