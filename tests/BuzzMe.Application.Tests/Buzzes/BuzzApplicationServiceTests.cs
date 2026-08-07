using BuzzMe.Application.Buzzes;
using BuzzMe.Application.Occurrences;
using BuzzMe.Application.Reminders;
using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Buzzes;
using BuzzMe.Domain.Reminders;

namespace BuzzMe.Application.Tests.Buzzes;

public sealed class BuzzApplicationServiceTests
{
    private readonly InMemoryBoardRepository _boardRepository = new();
    private readonly InMemoryReminderRepository _reminderRepository = new();
    private readonly InMemoryOccurrenceRepository _occurrenceRepository = new();
    private readonly InMemoryBuzzRepository _buzzRepository = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly BuzzApplicationService _sut;
    private readonly ReminderApplicationService _reminderService;
    private readonly OccurrenceApplicationService _occurrenceService;
    private readonly Guid _memberUserId = Guid.CreateVersion7();
    private readonly Board _board;

    public BuzzApplicationServiceTests()
    {
        var idGenerator = new FakeIdGenerator();
        _sut = new BuzzApplicationService(_buzzRepository, _occurrenceRepository, _reminderRepository, _boardRepository, idGenerator, _clock);
        _reminderService = new ReminderApplicationService(_reminderRepository, _boardRepository, idGenerator, _clock);
        _occurrenceService = new OccurrenceApplicationService(_occurrenceRepository, _reminderRepository, _boardRepository, idGenerator, _clock);

        _board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), _memberUserId, _clock.UtcNow);
        _boardRepository.AddAsync(_board, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task<Guid> CreateReminderAsync(string notifyPreset = "atTime") =>
        (await _reminderService.CreateReminderAsync(
            _memberUserId, _board.Id.Value, "Vet visit", "once", _clock.UtcNow.DateTime.AddDays(1), notifyPreset, CancellationToken.None)).Value.Id;

    private async Task<Guid> CreateOccurrenceAsync(Guid reminderId)
    {
        var generated = await _occurrenceService.GenerateOccurrencesAsync(_memberUserId, reminderId, CancellationToken.None);
        return generated.Value[0].Id;
    }

    [Fact]
    public async Task GenerateBuzzesAsync_GeneratesOneBuzzForTheSoleBoardMember()
    {
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);

        var result = await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var buzz = Assert.Single(result.Value);
        Assert.Equal(occurrenceId, buzz.OccurrenceId);
        Assert.Equal(_memberUserId, buzz.RecipientUserId);
        Assert.Equal("scheduled", buzz.Status);
        Assert.Equal(0, buzz.AttemptCount);
    }

    [Fact]
    public async Task GenerateBuzzesAsync_CalledAgain_IsIdempotent()
    {
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);

        var second = await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Empty(second.Value);
    }

    [Fact]
    public async Task GenerateBuzzesAsync_ScheduledAtIsTheOccurrenceDueAtMinusTheNotifyPresetLeadTime()
    {
        var reminderId = await CreateReminderAsync("1DayBefore");
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        var occurrence = (await _occurrenceService.GetOccurrenceAsync(_memberUserId, occurrenceId, CancellationToken.None)).Value;

        var result = await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);

        var buzz = Assert.Single(result.Value);
        Assert.Equal(occurrence.DueAt.AddDays(-1), buzz.ScheduledAt);
    }

    [Fact]
    public async Task GenerateBuzzesAsync_ReturnsNotFoundForAnUnknownOccurrence()
    {
        var result = await _sut.GenerateBuzzesAsync(_memberUserId, Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task GenerateBuzzesAsync_ReturnsNotFoundWhenTheOwningReminderIsSoftDeleted()
    {
        // Sprint 3.1's soft delete: a deleted Reminder is "not found" for every normal path,
        // including Buzz generation — this satisfies the Sprint 4 rule that a Buzz is only
        // generated when the Reminder exists and is not deleted, without any new check
        // needed (ReminderRepository.GetByIdAsync already excludes it).
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        await _reminderRepository.MarkDeletedAsync(new ReminderId(reminderId), _clock.UtcNow, CancellationToken.None);

        var result = await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task GenerateBuzzesAsync_ReturnsNotFoundForSomeoneWhoIsNotAMemberOfTheOwningBoard()
    {
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        var strangerUserId = Guid.CreateVersion7();

        var result = await _sut.GenerateBuzzesAsync(strangerUserId, occurrenceId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task GetBuzzAsync_ReturnsTheBuzzForItsRecipient()
    {
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        var generated = await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);
        var buzzId = generated.Value[0].Id;

        var result = await _sut.GetBuzzAsync(_memberUserId, buzzId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(buzzId, result.Value.Id);
    }

    [Fact]
    public async Task GetBuzzAsync_ReturnsNotFoundForSomeoneWhoIsNotTheRecipient()
    {
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        var generated = await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);
        var buzzId = generated.Value[0].Id;
        var strangerUserId = Guid.CreateVersion7();

        var result = await _sut.GetBuzzAsync(strangerUserId, buzzId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task GetBuzzAsync_ReturnsNotFoundForAnUnknownBuzz()
    {
        var result = await _sut.GetBuzzAsync(_memberUserId, Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task ListPendingBuzzesAsync_ReturnsOnlyTheRequestingUsersOwnPendingBuzzes()
    {
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);
        var strangerUserId = Guid.CreateVersion7();

        var ownResult = await _sut.ListPendingBuzzesAsync(_memberUserId, cursor: null, limit: 20, CancellationToken.None);
        var strangerResult = await _sut.ListPendingBuzzesAsync(strangerUserId, cursor: null, limit: 20, CancellationToken.None);

        Assert.True(ownResult.IsSuccess);
        Assert.Single(ownResult.Value.Items);
        Assert.True(strangerResult.IsSuccess);
        Assert.Empty(strangerResult.Value.Items);
    }

    [Fact]
    public async Task ClaimPendingBuzzesAsync_ClaimsABuzzWhoseScheduledAtHasArrived()
    {
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);
        _clock.Advance(TimeSpan.FromDays(2));

        var claimed = await _sut.ClaimPendingBuzzesAsync(batchSize: 20, CancellationToken.None);

        var buzz = Assert.Single(claimed);
        Assert.Equal(BuzzStatus.Generated, buzz.Status);
        Assert.Equal(1, buzz.AttemptCount);
    }

    [Fact]
    public async Task ClaimPendingBuzzesAsync_DoesNotClaimABuzzScheduledInTheFuture()
    {
        // "atTime" notify preset + a Reminder due tomorrow → ScheduledAt is in the future
        // relative to _clock's current instant, so nothing should be claimable yet.
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);

        var claimed = await _sut.ClaimPendingBuzzesAsync(batchSize: 20, CancellationToken.None);

        Assert.Empty(claimed);
    }

    [Fact]
    public async Task ClaimPendingBuzzesAsync_CalledAgain_DoesNotReclaimAnAlreadyClaimedBuzz()
    {
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);
        _clock.Advance(TimeSpan.FromDays(2));
        await _sut.ClaimPendingBuzzesAsync(batchSize: 20, CancellationToken.None);

        var second = await _sut.ClaimPendingBuzzesAsync(batchSize: 20, CancellationToken.None);

        Assert.Empty(second);
    }

    [Fact]
    public async Task MarkDeliveredAsync_TransitionsTheClaimedBuzzToDelivered()
    {
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);
        _clock.Advance(TimeSpan.FromDays(2));
        var claimed = Assert.Single(await _sut.ClaimPendingBuzzesAsync(batchSize: 20, CancellationToken.None));

        await _sut.MarkDeliveredAsync(claimed, CancellationToken.None);

        Assert.Equal(BuzzStatus.Delivered, claimed.Status);
    }

    [Fact]
    public async Task MarkFailedAsync_TransitionsTheClaimedBuzzToFailed()
    {
        var reminderId = await CreateReminderAsync();
        var occurrenceId = await CreateOccurrenceAsync(reminderId);
        await _sut.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);
        _clock.Advance(TimeSpan.FromDays(2));
        var claimed = Assert.Single(await _sut.ClaimPendingBuzzesAsync(batchSize: 20, CancellationToken.None));

        await _sut.MarkFailedAsync(claimed, CancellationToken.None);

        Assert.Equal(BuzzStatus.Failed, claimed.Status);
    }
}
