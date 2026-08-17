using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Users;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Users;
using Microsoft.Extensions.DependencyInjection;

namespace BuzzMe.Api.IntegrationTests.Users;

/// <summary>
/// GET/PATCH /v1/users/me against the real host and real MongoDB. Each test seeds its User
/// directly against IUserRepository/IBoardRepository (resolved from the factory's service
/// provider) rather than through AuthApplicationService's real Register/VerifyAccount
/// flow — that flow's verification code is randomly generated and delivered only through
/// the (Null, logging-only) email/SMS senders wired into the real host, with no way for a
/// test to observe it. This file is about the two profile endpoints, not Auth's own HTTP
/// surface (that's AuthEndpointsTests' job, seeded end-to-end for real).
/// </summary>
public sealed class UserEndpointsTests : IClassFixture<BuzzMeApiFactory>
{
    private readonly BuzzMeApiFactory _factory;

    public UserEndpointsTests(BuzzMeApiFactory factory) => _factory = factory;

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.CreateAccessTokenFor(userId));
        return client;
    }

    private async Task<Guid> SeedActiveUserAsync(string? email, string? phone, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var boardRepository = scope.ServiceProvider.GetRequiredService<IBoardRepository>();

        var now = DateTimeOffset.UtcNow;
        var user = User.Register(
            new UserId(Guid.CreateVersion7()), email, phone, "hashed-password", new DisplayName(displayName),
            "000000", now.AddMinutes(15), now);
        user.Verify(now);

        var personalBoard = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Personal"), user.Id.Value, now);
        await boardRepository.AddAsync(personalBoard, CancellationToken.None);
        user.CompleteProvisioning(personalBoard.Id);

        await userRepository.AddAsync(user, CancellationToken.None);
        return user.Id.Value;
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsTheProvisionedUser()
    {
        var userId = await SeedActiveUserAsync($"{Guid.CreateVersion7()}@example.com", null, "Alice");
        var client = CreateAuthenticatedClient(userId);

        var response = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Equal(userId, body?.Data?.Id);
        Assert.Equal("Alice", body?.Data?.DisplayName);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsNotFoundWhenNeverProvisioned()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());

        var response = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Equal(ErrorCode.NotFound, body?.Error?.Code);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_PersistsTheDisplayNameChange()
    {
        var userId = await SeedActiveUserAsync($"{Guid.CreateVersion7()}@example.com", null, "Alice");
        var client = CreateAuthenticatedClient(userId);

        var response = await client.PatchAsJsonAsync(
            "/v1/users/me", new UpdateProfileRequest("Alicia", null, null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Equal("Alicia", body?.Data?.DisplayName);
    }

    [Fact]
    public async Task UpdateProfile_WithAnEmptyDisplayName_ReturnsValidationError()
    {
        var userId = await SeedActiveUserAsync($"{Guid.CreateVersion7()}@example.com", null, "Alice");
        var client = CreateAuthenticatedClient(userId);

        var response = await client.PatchAsJsonAsync(
            "/v1/users/me", new UpdateProfileRequest("", null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Equal(ErrorCode.ValidationError, body?.Error?.Code);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsConflictWhenTheEmailIsAlreadyRegistered()
    {
        var takenEmail = $"{Guid.CreateVersion7()}@example.com";
        await SeedActiveUserAsync(takenEmail, null, "Bob");
        var userId = await SeedActiveUserAsync($"{Guid.CreateVersion7()}@example.com", null, "Alice");
        var client = CreateAuthenticatedClient(userId);

        var response = await client.PatchAsJsonAsync(
            "/v1/users/me", new UpdateProfileRequest(null, null, takenEmail, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Equal(ErrorCode.Conflict, body?.Error?.Code);
    }

    [Fact]
    public async Task UpdateProfile_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            "/v1/users/me", new UpdateProfileRequest("Alicia", null, null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
