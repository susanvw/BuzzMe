using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuzzMe.Contracts.V1.Boards;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Users;
using Microsoft.Extensions.DependencyInjection;

namespace BuzzMe.Api.IntegrationTests.Boards;

/// <summary>
/// The Sprint 1 acceptance scenario, end to end, against the real host and real MongoDB:
/// create a Board, the creator becomes Owner and Member, it's retrievable by id, and it
/// appears in List Boards.
/// </summary>
public sealed class BoardEndpointsTests : IClassFixture<BuzzMeApiFactory>
{
    private readonly BuzzMeApiFactory _factory;

    public BoardEndpointsTests(BuzzMeApiFactory factory) => _factory = factory;

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.CreateAccessTokenFor(userId));
        return client;
    }

    [Fact]
    public async Task CreateBoard_PersistsTheBoardAndMakesTheCreatorOwnerAndMember()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateAuthenticatedClient(userId);

        var createResponse = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("Family"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<BoardResponse>>();
        Assert.NotNull(created?.Data);
        Assert.Equal("Family", created.Data.Name);
        Assert.Equal(userId, created.Data.OwnerUserId);
    }

    [Fact]
    public async Task GetBoard_RetrievesTheBoardJustCreated()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateAuthenticatedClient(userId);
        var created = await CreateBoardAsync(client, "Family");

        var getResponse = await client.GetAsync($"/v1/boards/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<BoardResponse>>();
        Assert.Equal(created.Id, fetched?.Data?.Id);
        Assert.Equal("Family", fetched?.Data?.Name);
    }

    [Fact]
    public async Task ListBoards_IncludesTheBoardJustCreated()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateAuthenticatedClient(userId);
        var created = await CreateBoardAsync(client, "CrossFit");

        var listResponse = await client.GetAsync("/v1/boards");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<ApiListResponse<BoardResponse>>();
        Assert.NotNull(list?.Data);
        Assert.Contains(list.Data, board => board.Id == created.Id);
    }

    [Fact]
    public async Task GetBoard_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var ownerUserId = Guid.CreateVersion7();
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");

        var strangerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var response = await strangerClient.GetAsync($"/v1/boards/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BoardResponse>>();
        Assert.Equal(ErrorCode.NotFound, body?.Error?.Code);
    }

    [Fact]
    public async Task CreateBoard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("Family"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoard_WithAnEmptyName_ReturnsValidationError()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());

        var response = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BoardResponse>>();
        Assert.Equal(ErrorCode.ValidationError, body?.Error?.Code);
    }

    [Fact]
    public async Task MuteBoard_ReturnsNoContent()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var created = await CreateBoardAsync(client, "Family");

        var response = await client.PostAsync($"/v1/boards/{created.Id}/mute", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task MuteBoard_CalledAgain_IsStillNoContent()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var created = await CreateBoardAsync(client, "Family");
        await client.PostAsync($"/v1/boards/{created.Id}/mute", content: null);

        var response = await client.PostAsync($"/v1/boards/{created.Id}/mute", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task MuteBoard_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var created = await CreateBoardAsync(ownerClient, "Family");
        var strangerClient = CreateAuthenticatedClient(Guid.CreateVersion7());

        var response = await strangerClient.PostAsync($"/v1/boards/{created.Id}/mute", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MuteBoard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/v1/boards/{Guid.CreateVersion7()}/mute", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnmuteBoard_ReturnsNoContent()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var created = await CreateBoardAsync(client, "Family");
        await client.PostAsync($"/v1/boards/{created.Id}/mute", content: null);

        var response = await client.PostAsync($"/v1/boards/{created.Id}/unmute", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UnmuteBoard_WithoutHavingMuted_IsStillNoContent()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var created = await CreateBoardAsync(client, "Family");

        var response = await client.PostAsync($"/v1/boards/{created.Id}/unmute", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UnmuteBoard_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var created = await CreateBoardAsync(ownerClient, "Family");
        var strangerClient = CreateAuthenticatedClient(Guid.CreateVersion7());

        var response = await strangerClient.PostAsync($"/v1/boards/{created.Id}/unmute", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<BoardResponse> CreateBoardAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest(name));
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BoardResponse>>();
        return body!.Data!;
    }

    /// <summary>
    /// No HTTP-reachable way to add a second Member without a full Invitation accept flow
    /// (InvitationEndpointsTests' own job) — seeds directly against the real repository,
    /// same pattern as UserEndpointsTests' SeedActiveUserAsync.
    /// </summary>
    private async Task AddMemberDirectlyAsync(Guid boardId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var boardRepository = scope.ServiceProvider.GetRequiredService<IBoardRepository>();
        var board = await boardRepository.GetByIdAsync(new BoardId(boardId), CancellationToken.None);
        board!.GrantMembership(userId, DateTimeOffset.UtcNow);
        await boardRepository.UpdateAsync(board, CancellationToken.None);
    }

    [Fact]
    public async Task LeaveBoard_NonOwnerMember_ReturnsNullReassignedOwner()
    {
        var ownerUserId = Guid.CreateVersion7();
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");
        var leavingUserId = Guid.CreateVersion7();
        await AddMemberDirectlyAsync(created.Id, leavingUserId);
        var leavingClient = CreateAuthenticatedClient(leavingUserId);

        var response = await leavingClient.PostAsync($"/v1/boards/{created.Id}/leave", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LeaveBoardResponse>>();
        Assert.Null(body?.Data?.ReassignedOwnerUserId);
    }

    [Fact]
    public async Task LeaveBoard_SoleOwnerWithOtherMembers_ReturnsTheReassignedOwner()
    {
        var ownerUserId = Guid.CreateVersion7();
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");
        var otherMemberUserId = Guid.CreateVersion7();
        await AddMemberDirectlyAsync(created.Id, otherMemberUserId);

        var response = await ownerClient.PostAsync($"/v1/boards/{created.Id}/leave", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LeaveBoardResponse>>();
        Assert.Equal(otherMemberUserId, body?.Data?.ReassignedOwnerUserId);
    }

    [Fact]
    public async Task LeaveBoard_SoleMemberEntirely_ReturnsForbidden()
    {
        var ownerUserId = Guid.CreateVersion7();
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");

        var response = await ownerClient.PostAsync($"/v1/boards/{created.Id}/leave", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LeaveBoardResponse>>();
        Assert.Equal(ErrorCode.Forbidden, body?.Error?.Code);
    }

    [Fact]
    public async Task LeaveBoard_CalledAgain_IsStillOk()
    {
        var ownerUserId = Guid.CreateVersion7();
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");
        var leavingUserId = Guid.CreateVersion7();
        await AddMemberDirectlyAsync(created.Id, leavingUserId);
        var leavingClient = CreateAuthenticatedClient(leavingUserId);
        await leavingClient.PostAsync($"/v1/boards/{created.Id}/leave", content: null);

        var response = await leavingClient.PostAsync($"/v1/boards/{created.Id}/leave", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LeaveBoard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/v1/boards/{Guid.CreateVersion7()}/leave", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_OwnerRemovesAMember_ReturnsNoContent()
    {
        var ownerUserId = Guid.CreateVersion7();
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");
        var targetUserId = Guid.CreateVersion7();
        await AddMemberDirectlyAsync(created.Id, targetUserId);

        var response = await ownerClient.DeleteAsync($"/v1/boards/{created.Id}/members/{targetUserId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_ByANonOwner_ReturnsForbidden()
    {
        var ownerUserId = Guid.CreateVersion7();
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");
        var nonOwnerUserId = Guid.CreateVersion7();
        await AddMemberDirectlyAsync(created.Id, nonOwnerUserId);
        var targetUserId = Guid.CreateVersion7();
        await AddMemberDirectlyAsync(created.Id, targetUserId);
        var nonOwnerClient = CreateAuthenticatedClient(nonOwnerUserId);

        var response = await nonOwnerClient.DeleteAsync($"/v1/boards/{created.Id}/members/{targetUserId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_TargetingSelf_ReturnsConflict()
    {
        var ownerUserId = Guid.CreateVersion7();
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");

        var response = await ownerClient.DeleteAsync($"/v1/boards/{created.Id}/members/{ownerUserId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_CalledAgain_IsStillNoContent()
    {
        var ownerUserId = Guid.CreateVersion7();
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");
        var targetUserId = Guid.CreateVersion7();
        await AddMemberDirectlyAsync(created.Id, targetUserId);
        await ownerClient.DeleteAsync($"/v1/boards/{created.Id}/members/{targetUserId}");

        var response = await ownerClient.DeleteAsync($"/v1/boards/{created.Id}/members/{targetUserId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/v1/boards/{Guid.CreateVersion7()}/members/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Seeds an Active User (real repository, real MongoDB) so List Members' displayName/photoUrl lookup has something real to find.</summary>
    private async Task SeedUserAsync(Guid userId, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var now = DateTimeOffset.UtcNow;
        var user = User.Register(
            new UserId(userId), $"{Guid.CreateVersion7()}@example.com", null, "hashed-password", new DisplayName(displayName),
            "000000", now.AddMinutes(15), now);
        user.Verify(now);
        await userRepository.AddAsync(user, CancellationToken.None);
    }

    [Fact]
    public async Task ListMembers_ReturnsEveryActiveMemberWithTheirProfileFields()
    {
        var ownerUserId = Guid.CreateVersion7();
        await SeedUserAsync(ownerUserId, "Alice Owner");
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");
        var otherUserId = Guid.CreateVersion7();
        await SeedUserAsync(otherUserId, "Bob Member");
        await AddMemberDirectlyAsync(created.Id, otherUserId);

        var response = await ownerClient.GetAsync($"/v1/boards/{created.Id}/members");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiListResponse<MembershipResponse>>();
        Assert.NotNull(body?.Data);
        Assert.Equal(2, body.Data.Count);
        var owner = body.Data.Single(m => m.UserId == ownerUserId);
        Assert.Equal("Alice Owner", owner.DisplayName);
        Assert.Equal("Owner", owner.Role);
        var other = body.Data.Single(m => m.UserId == otherUserId);
        Assert.Equal("Bob Member", other.DisplayName);
        Assert.Equal("Member", other.Role);
    }

    [Fact]
    public async Task ListMembers_ExcludesAMemberWhoHasLeft()
    {
        var ownerUserId = Guid.CreateVersion7();
        var ownerClient = CreateAuthenticatedClient(ownerUserId);
        var created = await CreateBoardAsync(ownerClient, "Family");
        var leavingUserId = Guid.CreateVersion7();
        await AddMemberDirectlyAsync(created.Id, leavingUserId);
        var leavingClient = CreateAuthenticatedClient(leavingUserId);
        await leavingClient.PostAsync($"/v1/boards/{created.Id}/leave", content: null);

        var response = await ownerClient.GetAsync($"/v1/boards/{created.Id}/members");

        var body = await response.Content.ReadFromJsonAsync<ApiListResponse<MembershipResponse>>();
        Assert.NotNull(body?.Data);
        Assert.DoesNotContain(body.Data, m => m.UserId == leavingUserId);
    }

    [Fact]
    public async Task ListMembers_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var created = await CreateBoardAsync(ownerClient, "Family");
        var strangerClient = CreateAuthenticatedClient(Guid.CreateVersion7());

        var response = await strangerClient.GetAsync($"/v1/boards/{created.Id}/members");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListMembers_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/v1/boards/{Guid.CreateVersion7()}/members");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
