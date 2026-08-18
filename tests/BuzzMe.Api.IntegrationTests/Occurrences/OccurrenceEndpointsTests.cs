using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuzzMe.Contracts.V1.Boards;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Occurrences;
using BuzzMe.Contracts.V1.Reminders;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;
using Microsoft.Extensions.DependencyInjection;

namespace BuzzMe.Api.IntegrationTests.Occurrences;

/// <summary>
/// API_CONTRACT.md §5's Complete/Dismiss/Reopen Reminder row (Sprint 15), end to end
/// against the real host and real MongoDB.
/// </summary>
public sealed class OccurrenceEndpointsTests : IClassFixture<BuzzMeApiFactory>
{
    private readonly BuzzMeApiFactory _factory;

    public OccurrenceEndpointsTests(BuzzMeApiFactory factory) => _factory = factory;

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
        new("Emma's birthday", "once", new DateTime(2026, 7, 9, 16, 0, 0), "atTime");

    private static async Task<ReminderResponse> CreateReminderAsync(HttpClient client, Guid boardId)
    {
        var response = await client.PostAsJsonAsync($"/v1/boards/{boardId}/reminders", DefaultRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReminderResponse>>();
        return body!.Data!;
    }

    /// <summary>
    /// No HTTP-reachable way to generate an Occurrence (GenerateOccurrencesAsync has no
    /// endpoint — Sprint 3's own scope decision, still true as of Sprint 15) — seeds
    /// directly against the real repository, same pattern as BoardEndpointsTests'
    /// AddMemberDirectlyAsync.
    /// </summary>
    private async Task<Guid> SeedOccurrenceAsync(Guid reminderId, DateTimeOffset dueAt)
    {
        using var scope = _factory.Services.CreateScope();
        var occurrenceRepository = scope.ServiceProvider.GetRequiredService<IOccurrenceRepository>();
        var occurrence = Occurrence.Generate(
            new OccurrenceId(Guid.CreateVersion7()), new ReminderId(reminderId), dueAt, DateTimeOffset.UtcNow);
        await occurrenceRepository.AddAsync(occurrence, CancellationToken.None);
        return occurrence.Id.Value;
    }

    private async Task DeleteReminderDirectlyAsync(Guid reminderId)
    {
        using var scope = _factory.Services.CreateScope();
        var reminderRepository = scope.ServiceProvider.GetRequiredService<IReminderRepository>();
        await reminderRepository.MarkDeletedAsync(new ReminderId(reminderId), DateTimeOffset.UtcNow, CancellationToken.None);
    }

    [Fact]
    public async Task CompleteOccurrence_MarksItCompletedAndReturnsTheUpdatedOccurrence()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var reminder = await CreateReminderAsync(client, board.Id);
        var occurrenceId = await SeedOccurrenceAsync(reminder.Id, DateTimeOffset.UtcNow.AddDays(1));

        var response = await client.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/complete", new ResolveOccurrenceRequest(0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OccurrenceResponse>>();
        Assert.Equal("completed", body?.Data?.Status);
        Assert.NotNull(body?.Data?.ResolvedBy);
        Assert.Equal(1, body.Data.Version);
    }

    [Fact]
    public async Task CompleteOccurrence_CalledAgainWithTheMatchingVersion_IsStillOk()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var reminder = await CreateReminderAsync(client, board.Id);
        var occurrenceId = await SeedOccurrenceAsync(reminder.Id, DateTimeOffset.UtcNow.AddDays(1));
        var first = await client.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/complete", new ResolveOccurrenceRequest(0));
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponse<OccurrenceResponse>>();

        var response = await client.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/complete",
            new ResolveOccurrenceRequest(firstBody!.Data!.Version));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOccurrence_WithAStaleExpectedVersion_ReturnsConflictWithTheResolvedState()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var reminder = await CreateReminderAsync(client, board.Id);
        var occurrenceId = await SeedOccurrenceAsync(reminder.Id, DateTimeOffset.UtcNow.AddDays(1));
        await client.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/complete", new ResolveOccurrenceRequest(0));

        var response = await client.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/dismiss", new ResolveOccurrenceRequest(0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OccurrenceResponse>>();
        Assert.Equal(ErrorCode.Conflict, body?.Error?.Code);
        Assert.Equal("completed", body?.Data?.Status);
    }

    [Fact]
    public async Task DismissOccurrence_MarksItDismissed()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var reminder = await CreateReminderAsync(client, board.Id);
        var occurrenceId = await SeedOccurrenceAsync(reminder.Id, DateTimeOffset.UtcNow.AddDays(1));

        var response = await client.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/dismiss", new ResolveOccurrenceRequest(0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OccurrenceResponse>>();
        Assert.Equal("dismissed", body?.Data?.Status);
    }

    [Fact]
    public async Task ReopenOccurrence_RestoresAResolvedOccurrence()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var reminder = await CreateReminderAsync(client, board.Id);
        var occurrenceId = await SeedOccurrenceAsync(reminder.Id, DateTimeOffset.UtcNow.AddHours(-1));
        await client.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/complete", new ResolveOccurrenceRequest(0));

        var response = await client.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/reopen", new ResolveOccurrenceRequest(1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OccurrenceResponse>>();
        Assert.Equal("due", body?.Data?.Status);
        Assert.Null(body?.Data?.ResolvedBy);
    }

    [Fact]
    public async Task ReopenOccurrence_WhenNotCurrentlyResolved_ReturnsConflict()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var reminder = await CreateReminderAsync(client, board.Id);
        var occurrenceId = await SeedOccurrenceAsync(reminder.Id, DateTimeOffset.UtcNow.AddDays(1));

        var response = await client.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/reopen", new ResolveOccurrenceRequest(0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOccurrence_ReturnsGoneWhenTheParentReminderIsDeleted()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var reminder = await CreateReminderAsync(client, board.Id);
        var occurrenceId = await SeedOccurrenceAsync(reminder.Id, DateTimeOffset.UtcNow.AddDays(1));
        await DeleteReminderDirectlyAsync(reminder.Id);

        var response = await client.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/complete", new ResolveOccurrenceRequest(0));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOccurrence_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(ownerClient, "Family");
        var reminder = await CreateReminderAsync(ownerClient, board.Id);
        var occurrenceId = await SeedOccurrenceAsync(reminder.Id, DateTimeOffset.UtcNow.AddDays(1));
        var strangerClient = CreateAuthenticatedClient(Guid.CreateVersion7());

        var response = await strangerClient.PostAsJsonAsync(
            $"/v1/reminders/{reminder.Id}/occurrences/{occurrenceId}/complete", new ResolveOccurrenceRequest(0));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOccurrence_ReturnsNotFoundWhenTheReminderIdInThePathDoesNotMatch()
    {
        var client = CreateAuthenticatedClient(Guid.CreateVersion7());
        var board = await CreateBoardAsync(client, "Family");
        var reminder = await CreateReminderAsync(client, board.Id);
        var occurrenceId = await SeedOccurrenceAsync(reminder.Id, DateTimeOffset.UtcNow.AddDays(1));

        var response = await client.PostAsJsonAsync(
            $"/v1/reminders/{Guid.CreateVersion7()}/occurrences/{occurrenceId}/complete", new ResolveOccurrenceRequest(0));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOccurrence_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/v1/reminders/{Guid.CreateVersion7()}/occurrences/{Guid.CreateVersion7()}/complete", new ResolveOccurrenceRequest(0));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
