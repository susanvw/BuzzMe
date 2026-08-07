using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuzzMe.Contracts.V1.Boards;
using BuzzMe.Contracts.V1.Common;

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

    private static async Task<BoardResponse> CreateBoardAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest(name));
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BoardResponse>>();
        return body!.Data!;
    }
}
