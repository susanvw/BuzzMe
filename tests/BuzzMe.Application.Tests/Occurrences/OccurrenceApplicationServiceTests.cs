using BuzzMe.Application.Occurrences;
using BuzzMe.Application.Reminders;
using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;

namespace BuzzMe.Application.Tests.Occurrences;

public sealed class OccurrenceApplicationServiceTests
{
    private readonly InMemoryBoardRepository _boardRepository = new();
    private readonly InMemoryReminderRepository _reminderRepository = new();
    private readonly InMemoryOccurrenceRepository _occurrenceRepository = new();
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly OccurrenceApplicationService _sut;
    private readonly ReminderApplicationService _reminderService;
    private readonly Guid _memberUserId = Guid.CreateVersion7();
    private readonly Board _board;

    public OccurrenceApplicationServiceTests()
    {
        var idGenerator = new FakeIdGenerator();
        _sut = new OccurrenceApplicationService(
            _occurrenceRepository, _reminderRepository, _boardRepository, _userRepository, idGenerator, _clock);
        _reminderService = new ReminderApplicationService(_reminderRepository, _boardRepository, idGenerator, _clock);

        _board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), _memberUserId, _clock.UtcNow);
        _boardRepository.AddAsync(_board, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task<(Guid ReminderId, Guid OccurrenceId)> CreateResolvableOccurrenceAsync(DateTimeOffset dueAt)
    {
        var reminderId = await CreateReminderAsync("once", dueAt.DateTime);
        var generated = await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);
        return (reminderId, generated.Value[0].Id);
    }

    private async Task<Guid> CreateReminderAsync(string recurrence, DateTime startDate)
    {
        var result = await _reminderService.CreateReminderAsync(
            _memberUserId, _board.Id.Value, "Test reminder", recurrence, startDate, "atTime", CancellationToken.None);
        return result.Value.Id;
    }

    [Fact]
    public async Task GenerateOccurrencesAsync_Once_GeneratesExactlyOneOccurrence()
    {
        var reminderId = await CreateReminderAsync("once", _clock.UtcNow.DateTime.AddDays(1));

        var result = await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task GenerateOccurrencesAsync_Once_CalledAgain_IsIdempotent()
    {
        var reminderId = await CreateReminderAsync("once", _clock.UtcNow.DateTime.AddDays(1));
        await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);

        var second = await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Empty(second.Value);
        Assert.Equal(1, await _occurrenceRepository.CountByReminderAsync(new ReminderId(reminderId), CancellationToken.None));
    }

    [Fact]
    public async Task GenerateOccurrencesAsync_Daily_CatchesUpToNowInOneCall()
    {
        // Reminder starts 3 days before "now" — a single call should generate every daily
        // occurrence up to and including the first one at or after "now" (SPRINT_3_REPORT.md §3).
        var startDate = _clock.UtcNow.DateTime.AddDays(-3);
        var reminderId = await CreateReminderAsync("daily", startDate);

        var result = await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Count); // day -3, -2, -1, 0 (the one that reaches "now")
        Assert.True(result.Value[^1].DueAt >= _clock.UtcNow);
    }

    [Fact]
    public async Task GenerateOccurrencesAsync_CalledAgainWithoutTimePassing_GeneratesNothingMore()
    {
        var reminderId = await CreateReminderAsync("daily", _clock.UtcNow.DateTime.AddDays(-3));
        await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);
        var countAfterFirstCall = await _occurrenceRepository.CountByReminderAsync(new ReminderId(reminderId), CancellationToken.None);

        var second = await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Empty(second.Value);
        Assert.Equal(countAfterFirstCall, await _occurrenceRepository.CountByReminderAsync(new ReminderId(reminderId), CancellationToken.None));
    }

    [Fact]
    public async Task GenerateOccurrencesAsync_AfterTimeAdvances_GeneratesTheNextOccurrence()
    {
        var reminderId = await CreateReminderAsync("daily", _clock.UtcNow.DateTime);
        await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);

        _clock.Advance(TimeSpan.FromDays(2));
        var result = await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count); // catches up day+1, day+2
    }

    [Fact]
    public async Task GenerateOccurrencesAsync_ReturnsNotFoundForAReminderThatDoesNotExist()
    {
        var result = await _sut.GenerateOccurrencesAsync(_memberUserId, Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task GenerateOccurrencesAsync_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var reminderId = await CreateReminderAsync("once", _clock.UtcNow.DateTime.AddDays(1));
        var strangerUserId = Guid.CreateVersion7();

        var result = await _sut.GenerateOccurrencesAsync(strangerUserId, reminderId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task ListOccurrencesAsync_ReturnsGeneratedOccurrencesForTheReminder()
    {
        var reminderId = await CreateReminderAsync("daily", _clock.UtcNow.DateTime.AddDays(-2));
        await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);

        var result = await _sut.ListOccurrencesAsync(_memberUserId, reminderId, cursor: null, limit: 20, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetOccurrenceAsync_ReturnsTheOccurrence()
    {
        var reminderId = await CreateReminderAsync("once", _clock.UtcNow.DateTime.AddDays(1));
        var generated = await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);
        var occurrenceId = generated.Value[0].Id;

        var result = await _sut.GetOccurrenceAsync(_memberUserId, occurrenceId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(occurrenceId, result.Value.Id);
    }

    [Fact]
    public async Task GetOccurrenceAsync_StillReturnsTheOccurrenceWhenTheOwningReminderIsSoftDeleted()
    {
        // Sprint 3.1 resolved the orphaned-Occurrence gap SPRINT_3_REPORT.md §3 discovered
        // under Sprint 2's hard delete: Delete Reminder is a soft delete
        // (REMINDER_LIFECYCLE_REVIEW.md / SOFT_DELETE_IMPACT_REVIEW.md), so historical
        // Occurrence reads must keep working after the owning Reminder is deleted.
        var reminderId = await CreateReminderAsync("once", _clock.UtcNow.DateTime.AddDays(1));
        var generated = await _sut.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);
        var occurrenceId = generated.Value[0].Id;
        await _reminderRepository.MarkDeletedAsync(new ReminderId(reminderId), _clock.UtcNow, CancellationToken.None);

        var result = await _sut.GetOccurrenceAsync(_memberUserId, occurrenceId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(occurrenceId, result.Value.Id);
    }

    [Fact]
    public async Task CompleteOccurrenceAsync_MarksCompletedAndStampsTheResolver()
    {
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(_clock.UtcNow.AddDays(1));

        var result = await _sut.CompleteOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.VersionConflict);
        Assert.Equal("completed", result.Value.Occurrence.Status);
        Assert.Equal(_memberUserId, result.Value.Occurrence.ResolvedByUserId);
    }

    [Fact]
    public async Task CompleteOccurrenceAsync_CalledAgainWithTheMatchingVersion_IsIdempotent()
    {
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(_clock.UtcNow.AddDays(1));
        await _sut.CompleteOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        var second = await _sut.CompleteOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.False(second.Value.VersionConflict);
        Assert.Equal("completed", second.Value.Occurrence.Status);
    }

    [Fact]
    public async Task CompleteOccurrenceAsync_AfterADismiss_ReturnsTheDismissedStateWithoutOverridingIt()
    {
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(_clock.UtcNow.AddDays(1));
        await _sut.DismissOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        var result = await _sut.CompleteOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.VersionConflict);
        Assert.Equal("dismissed", result.Value.Occurrence.Status);
    }

    [Fact]
    public async Task CompleteOccurrenceAsync_WithAStaleExpectedVersion_ReturnsAVersionConflict()
    {
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(_clock.UtcNow.AddDays(1));

        var result = await _sut.CompleteOccurrenceAsync(
            _memberUserId, reminderId, occurrenceId, expectedVersion: 999, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.VersionConflict);
        Assert.Equal("scheduled", result.Value.Occurrence.Status);
    }

    [Fact]
    public async Task CompleteOccurrenceAsync_ReturnsGoneWhenTheParentReminderIsDeleted()
    {
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(_clock.UtcNow.AddDays(1));
        await _reminderRepository.MarkDeletedAsync(new ReminderId(reminderId), _clock.UtcNow, CancellationToken.None);

        var result = await _sut.CompleteOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("GONE", result.Error.Code);
    }

    [Fact]
    public async Task CompleteOccurrenceAsync_ReturnsNotFoundForSomeoneWhoIsNotAMember()
    {
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(_clock.UtcNow.AddDays(1));
        var strangerUserId = Guid.CreateVersion7();

        var result = await _sut.CompleteOccurrenceAsync(strangerUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task CompleteOccurrenceAsync_ReturnsNotFoundForAnOccurrenceThatDoesNotExist()
    {
        var result = await _sut.CompleteOccurrenceAsync(
            _memberUserId, Guid.CreateVersion7(), Guid.CreateVersion7(), expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task CompleteOccurrenceAsync_ReturnsNotFoundWhenTheReminderIdInThePathDoesNotMatch()
    {
        var (_, occurrenceId) = await CreateResolvableOccurrenceAsync(_clock.UtcNow.AddDays(1));

        var result = await _sut.CompleteOccurrenceAsync(
            _memberUserId, Guid.CreateVersion7(), occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task DismissOccurrenceAsync_MarksDismissedAndStampsTheResolver()
    {
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(_clock.UtcNow.AddDays(1));

        var result = await _sut.DismissOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("dismissed", result.Value.Occurrence.Status);
        Assert.Equal(_memberUserId, result.Value.Occurrence.ResolvedByUserId);
    }

    [Fact]
    public async Task DismissOccurrenceAsync_CalledAgainWithTheMatchingVersion_IsIdempotent()
    {
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(_clock.UtcNow.AddDays(1));
        await _sut.DismissOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        var second = await _sut.DismissOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.False(second.Value.VersionConflict);
    }

    [Fact]
    public async Task ReopenOccurrenceAsync_RestoresAResolvedOccurrenceWithinTheGraceWindow()
    {
        var dueAt = _clock.UtcNow.AddHours(-1);
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(dueAt);
        await _sut.CompleteOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        var result = await _sut.ReopenOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.VersionConflict);
        Assert.Equal("due", result.Value.Occurrence.Status);
        Assert.Null(result.Value.Occurrence.ResolvedByUserId);
    }

    [Fact]
    public async Task ReopenOccurrenceAsync_ReturnsConflictWhenNotCurrentlyResolved()
    {
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(_clock.UtcNow.AddDays(1));

        var result = await _sut.ReopenOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public async Task ReopenOccurrenceAsync_ReturnsForbiddenPastTheGraceWindow()
    {
        var dueAt = _clock.UtcNow.AddHours(-1);
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(dueAt);
        await _sut.CompleteOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(25));

        var result = await _sut.ReopenOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error.Code);
    }

    [Fact]
    public async Task ReopenOccurrenceAsync_ReturnsGoneWhenTheParentReminderIsDeleted()
    {
        var dueAt = _clock.UtcNow.AddHours(-1);
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(dueAt);
        await _sut.CompleteOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);
        await _reminderRepository.MarkDeletedAsync(new ReminderId(reminderId), _clock.UtcNow, CancellationToken.None);

        var result = await _sut.ReopenOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("GONE", result.Error.Code);
    }

    [Fact]
    public async Task ReopenOccurrenceAsync_WithAStaleExpectedVersion_ReturnsAVersionConflict()
    {
        var dueAt = _clock.UtcNow.AddHours(-1);
        var (reminderId, occurrenceId) = await CreateResolvableOccurrenceAsync(dueAt);
        await _sut.CompleteOccurrenceAsync(_memberUserId, reminderId, occurrenceId, expectedVersion: 0, CancellationToken.None);

        var result = await _sut.ReopenOccurrenceAsync(
            _memberUserId, reminderId, occurrenceId, expectedVersion: 999, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.VersionConflict);
        Assert.Equal("completed", result.Value.Occurrence.Status);
    }
}
