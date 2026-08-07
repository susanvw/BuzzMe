using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuzzMe.Contracts.V1.Boards;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Invitations;

namespace BuzzMe.Api.IntegrationTests.Invitations;

/// <summary>
/// The Sprint 5 acceptance scenario, end to end, against the real host and real MongoDB:
/// invite, validate, accept/decline — exactly the four endpoints API_CONTRACT.md §5 defines.
/// </summary>
public sealed class InvitationEndpointsTests : IClassFixture<BuzzMeApiFactory>
{
    private readonly BuzzMeApiFactory _factory;

    public InvitationEndpointsTests(BuzzMeApiFactory factory) => _factory = factory;

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.CreateAccessTokenFor(userId));
        return client;
    }

    private static async Task<BoardResponse> CreateBoardAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest(name));
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BoardResponse>>();
        return body!.Data!;
    }

    private static async Task<InvitationResponse> InviteMemberAsync(HttpClient client, Guid boardId)
    {
        var response = await client.PostAsJsonAsync($"/v1/boards/{boardId}/invitations", new InviteMemberRequest("link", null));
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<InvitationResponse>>();
        return body!.Data!;
    }

    [Fact]
    public async Task InviteMember_CreatesAPendingInvitation()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");

        var response = await client.PostAsJsonAsync($"/v1/boards/{board.Id}/invitations", new InviteMemberRequest("link", null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ApiResponse<InvitationResponse>>();
        Assert.NotNull(created?.Data);
        Assert.Equal(board.Id, created.Data.BoardId);
        Assert.Equal("Family", created.Data.BoardName);
        Assert.Equal("pending", created.Data.Status);
        Assert.False(string.IsNullOrEmpty(created.Data.Token));
    }

    [Fact]
    public async Task InviteMember_OnABoardTheRequesterDoesNotBelongTo_ReturnsNotFound()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");

        var strangerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var response = await strangerClient.PostAsJsonAsync($"/v1/boards/{board.Id}/invitations", new InviteMemberRequest("link", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InviteMember_WithAnInvalidChannel_ReturnsValidationError()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");

        var response = await client.PostAsJsonAsync($"/v1/boards/{board.Id}/invitations", new InviteMemberRequest("carrier-pigeon", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<InvitationResponse>>();
        Assert.Equal(ErrorCode.ValidationError, body?.Error?.Code);
    }

    [Fact]
    public async Task InviteMember_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/v1/boards/{Guid.CreateVersion7()}/invitations", new InviteMemberRequest("link", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidateInvitation_ReturnsTheInvitationForAValidToken_WithoutAuthentication()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");
        var invitation = await InviteMemberAsync(ownerClient, board.Id);

        var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.GetAsync($"/v1/invitations/{invitation.Token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ValidateInvitationResponse>>();
        Assert.Equal("Family", body?.Data?.BoardName);
        Assert.Equal("pending", body?.Data?.Status);
    }

    [Fact]
    public async Task ValidateInvitation_ReturnsNotFoundForAnUnknownToken()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/v1/invitations/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_GrantsMembership()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");
        var invitation = await InviteMemberAsync(ownerClient, board.Id);
        var inviteeUserId = Guid.CreateVersion7();
        var inviteeClient = CreateAuthenticatedClient(inviteeUserId);

        var response = await inviteeClient.PostAsync($"/v1/invitations/{invitation.Token}/accept", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipResponse>>();
        Assert.Equal(board.Id, body?.Data?.BoardId);
        Assert.Equal(inviteeUserId, body?.Data?.UserId);
        Assert.Equal("Member", body?.Data?.Role);
    }

    [Fact]
    public async Task AcceptInvitation_CalledAgainBySameUser_ReturnsTheSameMembershipNotAConflict()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");
        var invitation = await InviteMemberAsync(ownerClient, board.Id);
        var inviteeClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        await inviteeClient.PostAsync($"/v1/invitations/{invitation.Token}/accept", content: null);

        var response = await inviteeClient.PostAsync($"/v1/invitations/{invitation.Token}/accept", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_WithoutAuthentication_ReturnsUnauthorized()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");
        var invitation = await InviteMemberAsync(ownerClient, board.Id);

        var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.PostAsync($"/v1/invitations/{invitation.Token}/accept", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeclineInvitation_ReturnsDeclinedStatus()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");
        var invitation = await InviteMemberAsync(ownerClient, board.Id);
        var inviteeClient = CreateAuthenticatedClient(Guid.CreateVersion7());

        var response = await inviteeClient.PostAsync($"/v1/invitations/{invitation.Token}/decline", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<DeclineInvitationResponse>>();
        Assert.Equal("declined", body?.Data?.Status);
    }

    [Fact]
    public async Task DeclineInvitation_WithoutAuthentication_ReturnsUnauthorized()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");
        var invitation = await InviteMemberAsync(ownerClient, board.Id);

        var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.PostAsync($"/v1/invitations/{invitation.Token}/decline", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
