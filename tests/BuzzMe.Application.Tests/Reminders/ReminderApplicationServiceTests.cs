using BuzzMe.Application.Reminders;
using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Domain.Boards;

namespace BuzzMe.Application.Tests.Reminders;

public sealed class ReminderApplicationServiceTests
{
    private readonly InMemoryBoardRepository _boardRepository = new();
    private readonly InMemoryReminderRepository _reminderRepository = new();
    private readonly ReminderApplicationService _sut;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));
    private readonly Guid _memberUserId = Guid.CreateVersion7();
    private readonly Board _board;

    public ReminderApplicationServiceTests()
    {
        _sut = new ReminderApplicationService(_reminderRepository, _boardRepository, new FakeIdGenerator(), _clock);
        _board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), _memberUserId, _clock.UtcNow);
        _boardRepository.AddAsync(_board, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreateReminderAsync_PersistsTheReminderOnTheGivenBoard()
    {
        var result = await _sut.CreateReminderAsync(
            _memberUserId, _board.Id.Value, "Emma's birthday", "yearly", new DateTime(2026, 7, 9, 16, 0, 0), "1DayBefore", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_board.Id.Value, result.Value.BoardId);
        Assert.Equal("Emma's birthday", result.Value.Title);
        Assert.Equal("yearly", result.Value.Recurrence);
        Assert.Equal("1DayBefore", result.Value.NotifyPreset);
    }

    [Fact]
    public async Task CreateReminderAsync_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var strangerUserId = Guid.CreateVersion7();

        var result = await _sut.CreateReminderAsync(
            strangerUserId, _board.Id.Value, "Vet visit", "once", DateTime.UtcNow, "atTime", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task CreateReminderAsync_ReturnsValidationErrorForAnUnknownRecurrenceCode()
    {
        var result = await _sut.CreateReminderAsync(
            _memberUserId, _board.Id.Value, "Vet visit", "fortnightly", DateTime.UtcNow, "atTime", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task CreateReminderAsync_ReturnsValidationErrorForAnUnknownNotifyPresetCode()
    {
        var result = await _sut.CreateReminderAsync(
            _memberUserId, _board.Id.Value, "Vet visit", "once", DateTime.UtcNow, "2WeeksBefore", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task GetReminderAsync_ReturnsTheReminderForAMember()
    {
        var created = await CreateAsync();

        var result = await _sut.GetReminderAsync(_memberUserId, created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(created.Id, result.Value.Id);
    }

    [Fact]
    public async Task GetReminderAsync_ReturnsNotFoundForSomeoneWhoIsNotAMemberOfTheOwningBoard()
    {
        var created = await CreateAsync();
        var strangerUserId = Guid.CreateVersion7();

        var result = await _sut.GetReminderAsync(strangerUserId, created.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task ListBoardRemindersAsync_OnlyReturnsRemindersOnTheGivenBoard()
    {
        await CreateAsync("Family reminder");
        var otherBoard = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("CrossFit"), _memberUserId, _clock.UtcNow);
        await _boardRepository.AddAsync(otherBoard, CancellationToken.None);
        await _sut.CreateReminderAsync(_memberUserId, otherBoard.Id.Value, "Murph Saturday", "weekly", DateTime.UtcNow, "atTime", CancellationToken.None);

        var result = await _sut.ListBoardRemindersAsync(_memberUserId, _board.Id.Value, cursor: null, limit: 20, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("Family reminder", item.Title);
    }

    [Fact]
    public async Task DeleteReminderAsync_RemovesTheReminder()
    {
        var created = await CreateAsync();

        var deleteResult = await _sut.DeleteReminderAsync(_memberUserId, created.Id, CancellationToken.None);
        var getResult = await _sut.GetReminderAsync(_memberUserId, created.Id, CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        Assert.True(getResult.IsFailure);
        Assert.Equal("NOT_FOUND", getResult.Error.Code);
    }

    [Fact]
    public async Task DeleteReminderAsync_ReturnsNotFoundForAReminderThatDoesNotExist()
    {
        var result = await _sut.DeleteReminderAsync(_memberUserId, Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task DeleteReminderAsync_SucceedsAsANoOpWhenTheReminderIsAlreadyDeleted()
    {
        // API_CONTRACT.md §5 — deleting an already-deleted Reminder is the natural
        // idempotent no-op ("already-deleted → 204"), not a NotFound.
        var created = await CreateAsync();
        await _sut.DeleteReminderAsync(_memberUserId, created.Id, CancellationToken.None);

        var result = await _sut.DeleteReminderAsync(_memberUserId, created.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private async Task<Application.Reminders.Models.ReminderResult> CreateAsync(string title = "Vet visit")
    {
        var result = await _sut.CreateReminderAsync(
            _memberUserId, _board.Id.Value, title, "once", new DateTime(2026, 8, 1, 16, 0, 0), "atTime", CancellationToken.None);
        return result.Value;
    }

    [Fact]
    public async Task UpdateReminderAsync_TitleOnly_UpdatesJustTheTitle()
    {
        var created = await CreateAsync();

        var result = await _sut.UpdateReminderAsync(
            _memberUserId, created.Id, "Vet checkup", null, null, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Vet checkup", result.Value.Title);
        Assert.Equal(created.Recurrence, result.Value.Recurrence);
        Assert.Equal(created.NotifyPreset, result.Value.NotifyPreset);
    }

    [Fact]
    public async Task UpdateReminderAsync_RecurrenceOnly_UpdatesJustTheRecurrence()
    {
        var created = await CreateAsync();

        var result = await _sut.UpdateReminderAsync(
            _memberUserId, created.Id, null, "weekly", null, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("weekly", result.Value.Recurrence);
        Assert.Equal(created.Title, result.Value.Title);
    }

    [Fact]
    public async Task UpdateReminderAsync_StartDateOnly_UpdatesJustTheStartDate()
    {
        var created = await CreateAsync();
        var newStartDate = new DateTime(2027, 3, 1, 10, 0, 0);

        var result = await _sut.UpdateReminderAsync(
            _memberUserId, created.Id, null, null, newStartDate, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(newStartDate, result.Value.StartDate);
    }

    [Fact]
    public async Task UpdateReminderAsync_NotifyPresetOnly_UpdatesJustTheNotifyPreset()
    {
        var created = await CreateAsync();

        var result = await _sut.UpdateReminderAsync(
            _memberUserId, created.Id, null, null, null, "1HourBefore", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("1HourBefore", result.Value.NotifyPreset);
    }

    [Fact]
    public async Task UpdateReminderAsync_WithNoFieldsProvided_IsANoOp()
    {
        var created = await CreateAsync();

        var result = await _sut.UpdateReminderAsync(_memberUserId, created.Id, null, null, null, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(created.Title, result.Value.Title);
        Assert.Equal(created.UpdatedAt, result.Value.UpdatedAt);
    }

    [Fact]
    public async Task UpdateReminderAsync_ReturnsValidationErrorForAnUnknownRecurrenceCode()
    {
        var created = await CreateAsync();

        var result = await _sut.UpdateReminderAsync(
            _memberUserId, created.Id, null, "fortnightly", null, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task UpdateReminderAsync_ReturnsValidationErrorForAnUnknownNotifyPresetCode()
    {
        var created = await CreateAsync();

        var result = await _sut.UpdateReminderAsync(
            _memberUserId, created.Id, null, null, null, "2WeeksBefore", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task UpdateReminderAsync_ReturnsNotFoundForAReminderThatDoesNotExist()
    {
        var result = await _sut.UpdateReminderAsync(
            _memberUserId, Guid.CreateVersion7(), "New title", null, null, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task UpdateReminderAsync_ReturnsNotFoundForSomeoneWhoIsNotAMemberOfTheOwningBoard()
    {
        var created = await CreateAsync();
        var strangerUserId = Guid.CreateVersion7();

        var result = await _sut.UpdateReminderAsync(
            strangerUserId, created.Id, "New title", null, null, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task UpdateReminderAsync_ReturnsConflictWhenTheOwningBoardIsDeleted()
    {
        var created = await CreateAsync();
        _board.Delete(_clock.UtcNow);

        var result = await _sut.UpdateReminderAsync(
            _memberUserId, created.Id, "New title", null, null, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }
}
