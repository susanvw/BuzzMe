using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Users;
using BuzzMe.Domain.Users.Events;

namespace BuzzMe.Domain.Tests.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    private const string Code = "123456";

    private static User NewPendingUser(string? email = "person@example.com", string? phone = null, string code = Code) =>
        User.Register(new UserId(Guid.CreateVersion7()), email, phone, "hashed-password", new DisplayName("Emma"), code, Now + CodeLifetime, Now);

    private static User NewActiveUser(string? email = "person@example.com", string? phone = null)
    {
        var user = NewPendingUser(email, phone);
        user.Verify(Now);
        return user;
    }

    [Fact]
    public void Register_StampsTheGivenFieldsAndStartsPendingVerification()
    {
        var userId = new UserId(Guid.CreateVersion7());

        var user = User.Register(userId, "person@example.com", null, "hashed-password", new DisplayName("Emma"), Code, Now + CodeLifetime, Now);

        Assert.Equal(userId, user.Id);
        Assert.Equal("person@example.com", user.Email);
        Assert.Null(user.Phone);
        Assert.Equal("Emma", user.DisplayName.Value);
        Assert.Null(user.PhotoUrl);
        Assert.Equal("hashed-password", user.PasswordHash);
        Assert.Null(user.PersonalBoardId);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void Register_RaisesAccountRegistered()
    {
        var userId = new UserId(Guid.CreateVersion7());

        var user = User.Register(userId, "person@example.com", null, "hashed-password", new DisplayName("Emma"), Code, Now + CodeLifetime, Now);

        var raised = Assert.Single(user.DomainEvents.OfType<AccountRegistered>());
        Assert.Equal(userId, raised.UserId);
    }

    [Fact]
    public void Register_AllowsPhoneOnlyWithNoEmail()
    {
        var user = NewPendingUser(email: null, phone: "+15551234567");

        Assert.Null(user.Email);
        Assert.Equal("+15551234567", user.Phone);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(null, "  ")]
    public void Register_ThrowsWhenNeitherEmailNorPhoneIsGiven(string? email, string? phone)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Register(new UserId(Guid.CreateVersion7()), email, phone, "hashed-password", new DisplayName("Emma"), Code, Now + CodeLifetime, Now));
    }

    [Fact]
    public void HasValidVerificationCode_TrueForTheMatchingUnexpiredCode()
    {
        var user = NewPendingUser();

        Assert.True(user.HasValidVerificationCode(Code, Now));
    }

    [Fact]
    public void HasValidVerificationCode_FalseForTheWrongCode()
    {
        var user = NewPendingUser();

        Assert.False(user.HasValidVerificationCode("000000", Now));
    }

    [Fact]
    public void HasValidVerificationCode_FalseOnceExpired()
    {
        var user = NewPendingUser();

        Assert.False(user.HasValidVerificationCode(Code, Now + CodeLifetime + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void HasValidVerificationCode_FalseOnceAlreadyActive()
    {
        var user = NewActiveUser();

        Assert.False(user.HasValidVerificationCode(Code, Now));
    }

    [Fact]
    public void Verify_TransitionsToActiveAndClearsTheCode()
    {
        var user = NewPendingUser();

        user.Verify(Now);

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.VerificationCode);
        Assert.Null(user.VerificationCodeExpiresAt);
    }

    [Fact]
    public void Verify_RaisesAccountVerified()
    {
        var user = NewPendingUser();

        user.Verify(Now);

        var raised = Assert.Single(user.DomainEvents.OfType<AccountVerified>());
        Assert.Equal(user.Id, raised.UserId);
    }

    [Fact]
    public void Verify_ThrowsWhenNotPendingVerification()
    {
        var user = NewActiveUser();

        Assert.Throws<InvalidOperationException>(() => user.Verify(Now));
    }

    [Fact]
    public void CompleteProvisioning_SetsThePersonalBoardId()
    {
        var user = NewActiveUser();
        var personalBoardId = new BoardId(Guid.CreateVersion7());

        user.CompleteProvisioning(personalBoardId);

        Assert.Equal(personalBoardId, user.PersonalBoardId);
    }

    [Fact]
    public void CompleteProvisioning_IsIdempotentOnceAlreadySet()
    {
        var user = NewActiveUser();
        var firstBoardId = new BoardId(Guid.CreateVersion7());
        user.CompleteProvisioning(firstBoardId);

        user.CompleteProvisioning(new BoardId(Guid.CreateVersion7()));

        Assert.Equal(firstBoardId, user.PersonalBoardId);
    }

    [Fact]
    public void RequestPasswordReset_SetsTheTokenHashAndExpiry()
    {
        var user = NewActiveUser();
        var expiresAt = Now + TimeSpan.FromHours(1);

        user.RequestPasswordReset("token-hash", expiresAt, Now);

        Assert.True(user.HasValidPasswordResetToken("token-hash", Now));
        Assert.Equal(expiresAt, user.PasswordResetTokenExpiresAt);
    }

    [Fact]
    public void RequestPasswordReset_RaisesAccountRecoveryRequested()
    {
        var user = NewActiveUser();

        user.RequestPasswordReset("token-hash", Now + TimeSpan.FromHours(1), Now);

        var raised = Assert.Single(user.DomainEvents.OfType<AccountRecoveryRequested>());
        Assert.Equal(user.Id, raised.UserId);
    }

    [Fact]
    public void RequestPasswordReset_OverwritesAnyPriorOutstandingToken()
    {
        var user = NewActiveUser();
        user.RequestPasswordReset("first-hash", Now + TimeSpan.FromHours(1), Now);

        user.RequestPasswordReset("second-hash", Now + TimeSpan.FromHours(1), Now);

        Assert.False(user.HasValidPasswordResetToken("first-hash", Now));
        Assert.True(user.HasValidPasswordResetToken("second-hash", Now));
    }

    [Fact]
    public void HasValidPasswordResetToken_FalseOnceExpired()
    {
        var user = NewActiveUser();
        var expiresAt = Now + TimeSpan.FromHours(1);
        user.RequestPasswordReset("token-hash", expiresAt, Now);

        Assert.False(user.HasValidPasswordResetToken("token-hash", expiresAt + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void ResetPassword_ChangesThePasswordHashAndConsumesTheToken()
    {
        var user = NewActiveUser();
        user.RequestPasswordReset("token-hash", Now + TimeSpan.FromHours(1), Now);

        user.ResetPassword("new-hashed-password", Now);

        Assert.Equal("new-hashed-password", user.PasswordHash);
        Assert.Null(user.PasswordResetTokenHash);
        Assert.Null(user.PasswordResetTokenExpiresAt);
    }

    [Fact]
    public void ResetPassword_RaisesAccountRecovered()
    {
        var user = NewActiveUser();
        user.RequestPasswordReset("token-hash", Now + TimeSpan.FromHours(1), Now);

        user.ResetPassword("new-hashed-password", Now);

        var raised = Assert.Single(user.DomainEvents.OfType<AccountRecovered>());
        Assert.Equal(user.Id, raised.UserId);
    }

    [Fact]
    public void ResetPassword_ThrowsWhenThereIsNoOutstandingToken()
    {
        var user = NewActiveUser();

        Assert.Throws<InvalidOperationException>(() => user.ResetPassword("new-hashed-password", Now));
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
        user.ClearDomainEvents();

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

    [Fact]
    public void Delete_TransitionsToDeletedAndRaisesAccountDeleted()
    {
        var user = NewActiveUser();

        user.Delete(Now);

        Assert.Equal(UserStatus.Deleted, user.Status);
        var raised = Assert.Single(user.DomainEvents.OfType<AccountDeleted>());
        Assert.Equal(user.Id, raised.UserId);
    }

    [Fact]
    public void Delete_IsIdempotent()
    {
        var user = NewActiveUser();
        user.Delete(Now);

        user.Delete(Now.AddDays(1));

        Assert.Single(user.DomainEvents.OfType<AccountDeleted>());
    }
}
