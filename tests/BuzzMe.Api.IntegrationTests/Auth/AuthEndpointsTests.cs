using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuzzMe.Api.IntegrationTests.TestDoubles;
using BuzzMe.Application.Abstractions;
using BuzzMe.Contracts.V1.Auth;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Domain.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BuzzMe.Api.IntegrationTests.Auth;

/// <summary>
/// The six unauthenticated Auth endpoints (API_CONTRACT.md §5), against the real host and
/// real MongoDB, end to end: Register really persists a PendingVerification User with a
/// hashed password; the verification code is read back from the real, persisted User
/// record (it's stored in plaintext there by design — see User.VerificationCode) rather
/// than invented, so Verify is exercised with the actual code Register generated. The one
/// exception is the password-reset token, which is never persisted in plaintext anywhere
/// (only its hash) — those tests substitute a RecordingEmailSender for the real (Null,
/// logging-only) one via a derived factory, to read what was actually "sent."
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<BuzzMeApiFactory>
{
    private readonly BuzzMeApiFactory _factory;
    private readonly WebApplicationFactory<Program> _recordingFactory;
    private readonly RecordingEmailSender _emailSender = new();

    public AuthEndpointsTests(BuzzMeApiFactory factory)
    {
        _factory = factory;
        _recordingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IEmailSender>(_emailSender)));
    }

    private static string UniqueEmail() => $"{Guid.CreateVersion7()}@example.com";

    /// <summary>
    /// Reads the real, persisted verification code back out of MongoDB via the shared
    /// factory's IUserRepository — the recording factory is derived from the same factory
    /// and points at the same MongoDB connection, so this finds Users registered through
    /// either client.
    /// </summary>
    private async Task<string> ReadVerificationCodeAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByIdAsync(new UserId(userId), CancellationToken.None);
        Assert.NotNull(user);
        Assert.NotNull(user.VerificationCode);
        return user.VerificationCode!;
    }

    private async Task<(Guid UserId, string Email)> RegisterAsync(HttpClient client, string password = "hunter22")
    {
        var email = UniqueEmail();
        var response = await client.PostAsJsonAsync("/v1/auth/register", new RegisterRequest("Alice", email, null, password));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        return (body!.Data!.UserId, email);
    }

    [Fact]
    public async Task Register_PersistsAPendingVerificationUser()
    {
        var client = _factory.CreateClient();

        var (userId, _) = await RegisterAsync(client);

        Assert.NotEqual(Guid.Empty, userId);
    }

    [Fact]
    public async Task Register_WithADuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var (_, email) = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/v1/auth/register", new RegisterRequest("Someone Else", email, null, "hunter22"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        Assert.Equal(ErrorCode.Conflict, body?.Error?.Code);
    }

    [Fact]
    public async Task Register_WithATooShortPassword_ReturnsValidationError()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/register", new RegisterRequest("Alice", UniqueEmail(), null, "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        Assert.Equal(ErrorCode.ValidationError, body?.Error?.Code);
    }

    [Fact]
    public async Task Register_WithNeitherEmailNorPhone_ReturnsValidationError()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/register", new RegisterRequest("Alice", null, null, "hunter22"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyAccount_WithTheRealCode_ActivatesTheAccount()
    {
        var client = _factory.CreateClient();
        var (userId, email) = await RegisterAsync(client);
        var code = await ReadVerificationCodeAsync(userId);

        var response = await client.PostAsJsonAsync("/v1/auth/verify", new VerifyAccountRequest(email, null, code));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.Equal("active", body?.Data?.User.Status);
        Assert.NotNull(body?.Data?.User.PersonalBoardId);
        Assert.NotEmpty(body?.Data?.AccessToken ?? "");
        Assert.NotEmpty(body?.Data?.RefreshToken ?? "");
    }

    [Fact]
    public async Task VerifyAccount_WithTheWrongCode_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var (_, email) = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/v1/auth/verify", new VerifyAccountRequest(email, null, "000000"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.Equal(ErrorCode.Unauthorized, body?.Error?.Code);
    }

    private async Task<string> RegisterAndVerifyAsync(HttpClient client, string password = "hunter22")
    {
        var (userId, email) = await RegisterAsync(client, password);
        var code = await ReadVerificationCodeAsync(userId);
        var response = await client.PostAsJsonAsync("/v1/auth/verify", new VerifyAccountRequest(email, null, code));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return email;
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        var client = _factory.CreateClient();
        var email = await RegisterAndVerifyAsync(client, password: "hunter22");

        var response = await client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, null, "hunter22"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.Equal(email, body?.Data?.User.Email);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var email = await RegisterAndVerifyAsync(client, password: "hunter22");

        var response = await client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, null, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_BeforeVerifying_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var (_, email) = await RegisterAsync(client, password: "hunter22");

        var response = await client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, null, "hunter22"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_WithAValidToken_ReturnsANewPair()
    {
        var client = _factory.CreateClient();
        var email = await RegisterAndVerifyAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, null, "hunter22"));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();

        var response = await client.PostAsJsonAsync("/v1/auth/refresh-token", new RefreshTokenRequest(loginBody!.Data!.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TokenPairResponse>>();
        Assert.NotEqual(loginBody.Data.RefreshToken, body?.Data?.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_ReusedAfterRotation_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var email = await RegisterAndVerifyAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, null, "hunter22"));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        await client.PostAsJsonAsync("/v1/auth/refresh-token", new RefreshTokenRequest(loginBody!.Data!.RefreshToken));

        var response = await client.PostAsJsonAsync("/v1/auth/refresh-token", new RefreshTokenRequest(loginBody.Data.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_WithAnUnknownToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/refresh-token", new RefreshTokenRequest("not-a-real-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_AlwaysReturnsTheSameSuccessResponse()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/forgot-password", new ForgotPasswordRequest(UniqueEmail(), null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithTheRealToken_AllowsLoginWithTheNewPassword()
    {
        var client = _recordingFactory.CreateClient();
        var email = await RegisterAndVerifyAsync(client);
        _emailSender.Clear();

        var forgotResponse = await client.PostAsJsonAsync("/v1/auth/forgot-password", new ForgotPasswordRequest(email, null));
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);
        var sent = Assert.Single(_emailSender.SentMessages);
        var token = sent.Body.Split(' ').Last().TrimEnd('.');

        var resetResponse = await client.PostAsJsonAsync("/v1/auth/reset-password", new ResetPasswordRequest(token, "new-password"));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, null, "new-password"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithAnUnknownToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/reset-password", new ResetPasswordRequest("not-a-real-token", "new-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(string Email, string AccessToken, string RefreshToken)> RegisterAndVerifyWithTokensAsync(
        HttpClient client, string password = "hunter22")
    {
        var (userId, email) = await RegisterAsync(client, password);
        var code = await ReadVerificationCodeAsync(userId);
        var response = await client.PostAsJsonAsync("/v1/auth/verify", new VerifyAccountRequest(email, null, code));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return (email, body!.Data!.AccessToken, body.Data.RefreshToken);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsNoContent()
    {
        var client = _factory.CreateClient();
        var (_, accessToken, _) = await RegisterAndVerifyWithTokensAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/v1/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(true)),
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithoutConfirmation_ReturnsValidationError()
    {
        var client = _factory.CreateClient();
        var (_, accessToken, _) = await RegisterAndVerifyWithTokensAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/v1/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(false)),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal(ErrorCode.ValidationError, body?.Error?.Code);
    }

    [Fact]
    public async Task DeleteAccount_PreventsFutureLogin()
    {
        var client = _factory.CreateClient();
        var (email, accessToken, _) = await RegisterAndVerifyWithTokensAsync(client, password: "hunter22");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/v1/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(true)),
        });

        var loginResponse = await _factory.CreateClient().PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, null, "hunter22"));

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_RevokesTheRefreshToken()
    {
        var client = _factory.CreateClient();
        var (_, accessToken, refreshToken) = await RegisterAndVerifyWithTokensAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/v1/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(true)),
        });

        var refreshResponse = await _factory.CreateClient().PostAsJsonAsync("/v1/auth/refresh-token", new RefreshTokenRequest(refreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_CalledAgain_IsStillNoContent()
    {
        var client = _factory.CreateClient();
        var (_, accessToken, _) = await RegisterAndVerifyWithTokensAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/v1/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(true)),
        });

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/v1/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(true)),
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/v1/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(true)),
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
