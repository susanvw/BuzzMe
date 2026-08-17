using BuzzMe.Application.Auth;
using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Domain.Boards;

namespace BuzzMe.Application.Tests.Auth;

public sealed class AuthApplicationServiceTests
{
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly InMemoryBoardRepository _boardRepository = new();
    private readonly InMemoryRefreshTokenRepository _refreshTokenRepository = new();
    private readonly RecordingEmailSender _emailSender = new();
    private readonly RecordingSmsSender _smsSender = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 8, 9, 0, 0, TimeSpan.Zero));
    private readonly AuthApplicationService _sut;

    public AuthApplicationServiceTests()
    {
        _sut = new AuthApplicationService(
            _userRepository, _boardRepository, _refreshTokenRepository,
            new FakePasswordHasher(), new FakeAccessTokenIssuer(), new FakeSecureTokenGenerator(), new FakeVerificationCodeGenerator(),
            new FakeIdGenerator(), _clock, _emailSender, _smsSender);
    }

    private async Task<Guid> RegisterAsync(string? email = "alice@example.com", string? phone = null, string password = "hunter22", string displayName = "Alice")
    {
        var result = await _sut.RegisterAsync(email, phone, password, displayName, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value.UserId;
    }

    [Fact]
    public async Task RegisterAsync_CreatesAPendingVerificationUser()
    {
        var result = await _sut.RegisterAsync("alice@example.com", null, "hunter22", "Alice", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.UserId);
    }

    [Fact]
    public async Task RegisterAsync_SendsTheVerificationCodeByEmailWhenEmailIsGiven()
    {
        await _sut.RegisterAsync("alice@example.com", null, "hunter22", "Alice", CancellationToken.None);

        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Equal("alice@example.com", sent.ToAddress);
        Assert.Contains(FakeVerificationCodeGenerator.Code, sent.Body);
        Assert.Empty(_smsSender.SentMessages);
    }

    [Fact]
    public async Task RegisterAsync_SendsTheVerificationCodeBySmsWhenOnlyPhoneIsGiven()
    {
        await _sut.RegisterAsync(null, "+15551234567", "hunter22", "Alice", CancellationToken.None);

        var sent = Assert.Single(_smsSender.SentMessages);
        Assert.Equal("+15551234567", sent.ToPhoneNumber);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsConflictWhenTheEmailIsAlreadyRegistered()
    {
        await RegisterAsync(email: "alice@example.com");

        var result = await _sut.RegisterAsync("alice@example.com", null, "hunter22", "Someone Else", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public async Task VerifyAccountAsync_WithTheCorrectCode_ActivatesTheAccountAndIssuesTokens()
    {
        await RegisterAsync(email: "alice@example.com");

        var result = await _sut.VerifyAccountAsync("alice@example.com", null, FakeVerificationCodeGenerator.Code, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("active", result.Value.User.Status);
        Assert.NotEmpty(result.Value.AccessToken);
        Assert.NotEmpty(result.Value.RefreshToken);
    }

    [Fact]
    public async Task VerifyAccountAsync_CreatesThePersonalBoard()
    {
        var userId = await RegisterAsync(email: "alice@example.com");

        var result = await _sut.VerifyAccountAsync("alice@example.com", null, FakeVerificationCodeGenerator.Code, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.User.PersonalBoardId);
        var board = await _boardRepository.GetByIdAsync(new BoardId(result.Value.User.PersonalBoardId!.Value), CancellationToken.None);
        Assert.NotNull(board);
        Assert.Equal("Personal", board!.Name.Value);
        Assert.Equal(userId, board.OwnerUserId);
    }

    [Fact]
    public async Task VerifyAccountAsync_WithTheWrongCode_ReturnsUnauthorized()
    {
        await RegisterAsync(email: "alice@example.com");

        var result = await _sut.VerifyAccountAsync("alice@example.com", null, "000000", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error.Code);
    }

    [Fact]
    public async Task VerifyAccountAsync_AfterTheCodeHasExpired_ReturnsUnauthorized()
    {
        await RegisterAsync(email: "alice@example.com");
        _clock.Advance(TimeSpan.FromMinutes(16));

        var result = await _sut.VerifyAccountAsync("alice@example.com", null, FakeVerificationCodeGenerator.Code, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error.Code);
    }

    [Fact]
    public async Task VerifyAccountAsync_CalledAgainOnceAlreadyActive_IsIdempotentAndStillIssuesTokens()
    {
        await RegisterAsync(email: "alice@example.com");
        await _sut.VerifyAccountAsync("alice@example.com", null, FakeVerificationCodeGenerator.Code, CancellationToken.None);

        var second = await _sut.VerifyAccountAsync("alice@example.com", null, "000000", CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal("active", second.Value.User.Status);
    }

    [Fact]
    public async Task VerifyAccountAsync_ForAnUnknownAccount_ReturnsUnauthorized()
    {
        var result = await _sut.VerifyAccountAsync("nobody@example.com", null, "123456", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error.Code);
    }

    private async Task VerifyRegisteredUserAsync(string? email, string? phone) =>
        await _sut.VerifyAccountAsync(email, phone, FakeVerificationCodeGenerator.Code, CancellationToken.None);

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsTokensAndUser()
    {
        await RegisterAsync(email: "alice@example.com", password: "hunter22");
        await VerifyRegisteredUserAsync("alice@example.com", null);

        var result = await _sut.LoginAsync("alice@example.com", null, "hunter22", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("alice@example.com", result.Value.User.Email);
    }

    [Fact]
    public async Task LoginAsync_WithTheWrongPassword_ReturnsAGenericUnauthorized()
    {
        await RegisterAsync(email: "alice@example.com", password: "hunter22");
        await VerifyRegisteredUserAsync("alice@example.com", null);

        var result = await _sut.LoginAsync("alice@example.com", null, "wrong-password", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_ForAnUnknownAccount_ReturnsTheSameGenericUnauthorizedAsAWrongPassword()
    {
        var result = await _sut.LoginAsync("nobody@example.com", null, "hunter22", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_ForAStillPendingVerificationAccount_ReturnsUnauthorized()
    {
        await RegisterAsync(email: "alice@example.com", password: "hunter22");

        var result = await _sut.LoginAsync("alice@example.com", null, "hunter22", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error.Code);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithAValidToken_IssuesANewPairAndRevokesTheOldOne()
    {
        await RegisterAsync(email: "alice@example.com");
        var verified = await _sut.VerifyAccountAsync("alice@example.com", null, FakeVerificationCodeGenerator.Code, CancellationToken.None);
        var originalRefreshToken = verified.Value.RefreshToken;

        var result = await _sut.RefreshTokenAsync(originalRefreshToken, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(originalRefreshToken, result.Value.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_RejectsTheSameTokenUsedTwice()
    {
        await RegisterAsync(email: "alice@example.com");
        var verified = await _sut.VerifyAccountAsync("alice@example.com", null, FakeVerificationCodeGenerator.Code, CancellationToken.None);
        await _sut.RefreshTokenAsync(verified.Value.RefreshToken, CancellationToken.None);

        var result = await _sut.RefreshTokenAsync(verified.Value.RefreshToken, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error.Code);
    }

    [Fact]
    public async Task RefreshTokenAsync_RejectsAnUnknownToken()
    {
        var result = await _sut.RefreshTokenAsync("not-a-real-token", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error.Code);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ForAnExistingAccount_SendsAResetToken()
    {
        await RegisterAsync(email: "alice@example.com");
        await VerifyRegisteredUserAsync("alice@example.com", null);
        _emailSender.SentMessages.Clear();

        await _sut.ForgotPasswordAsync("alice@example.com", null, CancellationToken.None);

        Assert.Single(_emailSender.SentMessages);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ForAnUnknownAccount_CompletesWithoutSendingAnything()
    {
        await _sut.ForgotPasswordAsync("nobody@example.com", null, CancellationToken.None);

        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithAValidToken_ChangesThePasswordAndAllowsLoginWithTheNewOne()
    {
        await RegisterAsync(email: "alice@example.com", password: "hunter22");
        await VerifyRegisteredUserAsync("alice@example.com", null);
        _emailSender.SentMessages.Clear();
        await _sut.ForgotPasswordAsync("alice@example.com", null, CancellationToken.None);
        var token = _emailSender.SentMessages.Single().Body.Split(' ').Last().TrimEnd('.');

        var result = await _sut.ResetPasswordAsync(token, "new-password", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var login = await _sut.LoginAsync("alice@example.com", null, "new-password", CancellationToken.None);
        Assert.True(login.IsSuccess);
    }

    [Fact]
    public async Task ResetPasswordAsync_RejectsTheSameTokenUsedTwice()
    {
        await RegisterAsync(email: "alice@example.com");
        await VerifyRegisteredUserAsync("alice@example.com", null);
        _emailSender.SentMessages.Clear();
        await _sut.ForgotPasswordAsync("alice@example.com", null, CancellationToken.None);
        var token = _emailSender.SentMessages.Single().Body.Split(' ').Last().TrimEnd('.');
        await _sut.ResetPasswordAsync(token, "new-password", CancellationToken.None);

        var result = await _sut.ResetPasswordAsync(token, "another-password", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error.Code);
    }

    [Fact]
    public async Task ResetPasswordAsync_RejectsAnUnknownToken()
    {
        var result = await _sut.ResetPasswordAsync("not-a-real-token", "new-password", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error.Code);
    }
}
