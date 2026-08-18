using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuzzMe.Contracts.V1.Boards;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Reminders;

namespace BuzzMe.Api.IntegrationTests.Reminders;

/// <summary>
/// The Sprint 2 acceptance scenario, end to end, against the real host and real MongoDB:
/// create a Reminder on an existing Board, retrieve it, see it in the list, delete it, and
/// confirm it no longer comes back.
/// </summary>
public sealed class ReminderEndpointsTests : IClassFixture<BuzzMeApiFactory>
{
    private readonly BuzzMeApiFactory _factory;

    public ReminderEndpointsTests(BuzzMeApiFactory factory) => _factory = factory;

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

    private static readonly CreateReminderRequest DefaultRequest =
        new("Emma's birthday", "yearly", new DateTime(2026, 7, 9, 16, 0, 0), "1DayBefore");

    private static async Task<ReminderResponse> CreateReminderAsync(HttpClient client, Guid boardId, CreateReminderRequest? request = null)
    {
        var response = await client.PostAsJsonAsync($"/v1/boards/{boardId}/reminders", request ?? DefaultRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReminderResponse>>();
        return body!.Data!;
    }

    [Fact]
    public async Task CreateReminder_PersistsItOnTheGivenBoard()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateAuthenticatedClient(userId);
        var board = await CreateBoardAsync(client, "Family");

        var response = await client.PostAsJsonAsync($"/v1/boards/{board.Id}/reminders", DefaultRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ApiResponse<ReminderResponse>>();
        Assert.NotNull(created?.Data);
        Assert.Equal(board.Id, created.Data.BoardId);
        Assert.Equal("Emma's birthday", created.Data.Title);
        Assert.Equal("yearly", created.Data.Recurrence);
        Assert.Equal("1DayBefore", created.Data.NotifyPreset);
        Assert.Null(created.Data.NextOccurrence);
    }

    [Fact]
    public async Task GetReminder_RetrievesTheReminderJustCreated()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var created = await CreateReminderAsync(client, board.Id);

        var response = await client.GetAsync($"/v1/reminders/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<ApiResponse<ReminderResponse>>();
        Assert.Equal(created.Id, fetched?.Data?.Id);
    }

    [Fact]
    public async Task ListBoardReminders_IncludesTheReminderJustCreated()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "CrossFit");
        var created = await CreateReminderAsync(client, board.Id, new CreateReminderRequest("Murph Saturday", "weekly", DateTime.UtcNow, "atTime"));

        var response = await client.GetAsync($"/v1/boards/{board.Id}/reminders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<ApiListResponse<ReminderResponse>>();
        Assert.Contains(list!.Data!, reminder => reminder.Id == created.Id);
    }

    [Fact]
    public async Task DeleteReminder_RemovesItAndItIsNoLongerReturned()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var created = await CreateReminderAsync(client, board.Id);

        var deleteResponse = await client.DeleteAsync($"/v1/reminders/{created.Id}");
        var getResponse = await client.GetAsync($"/v1/reminders/{created.Id}");
        var listResponse = await client.GetAsync($"/v1/boards/{board.Id}/reminders");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<ApiListResponse<ReminderResponse>>();
        Assert.DoesNotContain(list!.Data!, reminder => reminder.Id == created.Id);
    }

    [Fact]
    public async Task GetReminder_ReturnsNotFoundForSomeoneWhoIsNotAMemberOfTheOwningBoard()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");
        var created = await CreateReminderAsync(ownerClient, board.Id);

        var strangerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var response = await strangerClient.GetAsync($"/v1/reminders/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateReminder_OnABoardTheRequesterDoesNotBelongTo_ReturnsNotFound()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");

        var strangerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var response = await strangerClient.PostAsJsonAsync($"/v1/boards/{board.Id}/reminders", DefaultRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateReminder_WithAnInvalidRecurrence_ReturnsValidationError()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");

        var response = await client.PostAsJsonAsync(
            $"/v1/boards/{board.Id}/reminders", new CreateReminderRequest("Vet visit", "fortnightly", DateTime.UtcNow, "atTime"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReminderResponse>>();
        Assert.Equal(ErrorCode.ValidationError, body?.Error?.Code);
    }

    [Fact]
    public async Task CreateReminder_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/v1/boards/{Guid.CreateVersion7()}/reminders", DefaultRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateReminder_TitleOnly_UpdatesJustTheTitle()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var created = await CreateReminderAsync(client, board.Id);

        var response = await client.PatchAsJsonAsync(
            $"/v1/reminders/{created.Id}", new UpdateReminderRequest("Emma's actual birthday", null, null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReminderResponse>>();
        Assert.Equal("Emma's actual birthday", body?.Data?.Title);
        Assert.Equal(created.Recurrence, body?.Data?.Recurrence);
    }

    [Fact]
    public async Task UpdateReminder_WithNoFieldsProvided_IsANoOp()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var created = await CreateReminderAsync(client, board.Id);

        var response = await client.PatchAsJsonAsync($"/v1/reminders/{created.Id}", new UpdateReminderRequest(null, null, null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReminderResponse>>();
        Assert.Equal(created.Title, body?.Data?.Title);
    }

    [Fact]
    public async Task UpdateReminder_WithABoardIdInTheBody_ReturnsValidationError()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var created = await CreateReminderAsync(client, board.Id);

        var response = await client.PatchAsJsonAsync(
            $"/v1/reminders/{created.Id}", new { boardId = Guid.CreateVersion7(), title = "New title" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReminderResponse>>();
        Assert.Equal(ErrorCode.ValidationError, body?.Error?.Code);
        Assert.Null(body?.Data);
    }

    [Fact]
    public async Task UpdateReminder_WithAnInvalidRecurrence_ReturnsValidationError()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var created = await CreateReminderAsync(client, board.Id);

        var response = await client.PatchAsJsonAsync(
            $"/v1/reminders/{created.Id}", new UpdateReminderRequest(null, "fortnightly", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReminderResponse>>();
        Assert.Equal(ErrorCode.ValidationError, body?.Error?.Code);
    }

    [Fact]
    public async Task UpdateReminder_ReturnsNotFoundForSomeoneWhoIsNotAMemberOfTheOwningBoard()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");
        var created = await CreateReminderAsync(ownerClient, board.Id);
        var strangerClient = CreateAuthenticatedClient(Guid.CreateVersion7());

        var response = await strangerClient.PatchAsJsonAsync(
            $"/v1/reminders/{created.Id}", new UpdateReminderRequest("New title", null, null, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateReminder_ReturnsConflictWhenTheOwningBoardIsDeleted()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var created = await CreateReminderAsync(client, board.Id);
        await client.DeleteAsync($"/v1/boards/{board.Id}");

        var response = await client.PatchAsJsonAsync(
            $"/v1/reminders/{created.Id}", new UpdateReminderRequest("New title", null, null, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateReminder_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/v1/reminders/{Guid.CreateVersion7()}", new UpdateReminderRequest("New title", null, null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
