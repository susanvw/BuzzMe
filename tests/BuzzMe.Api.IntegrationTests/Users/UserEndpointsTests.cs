using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuzzMe.Application.Users;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Users;
using Microsoft.Extensions.DependencyInjection;

namespace BuzzMe.Api.IntegrationTests.Users;

/// <summary>
/// GET/PATCH /v1/users/me against the real host and real MongoDB. There is no HTTP-reachable
/// provisioning endpoint (ProvisionAccountAsync exists at the Application layer only — see
/// SPRINT_8_REPORT.md), so each test seeds its User by resolving UserApplicationService
/// directly from the factory's service provider before exercising the real endpoints.
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

    private async Task ProvisionAsync(Guid userId, string? email, string? phone, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<UserApplicationService>();
        var result = await userService.ProvisionAccountAsync(userId, email, phone, displayName, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsTheProvisionedUser()
    {
        var userId = Guid.CreateVersion7();
        await ProvisionAsync(userId, $"{Guid.CreateVersion7()}@example.com", null, "Alice");
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
        var userId = Guid.CreateVersion7();
        await ProvisionAsync(userId, $"{Guid.CreateVersion7()}@example.com", null, "Alice");
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
        var userId = Guid.CreateVersion7();
        await ProvisionAsync(userId, $"{Guid.CreateVersion7()}@example.com", null, "Alice");
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
        await ProvisionAsync(Guid.CreateVersion7(), takenEmail, null, "Bob");
        var userId = Guid.CreateVersion7();
        await ProvisionAsync(userId, $"{Guid.CreateVersion7()}@example.com", null, "Alice");
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
