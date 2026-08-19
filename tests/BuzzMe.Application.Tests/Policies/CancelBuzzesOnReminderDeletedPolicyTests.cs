using BuzzMe.Application.Buzzes;
using BuzzMe.Application.Occurrences;
using BuzzMe.Application.Policies;
using BuzzMe.Application.Reminders;
using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Reminders.Events;

namespace BuzzMe.Application.Tests.Policies;

public sealed class CancelBuzzesOnReminderDeletedPolicyTests
{
    private readonly InMemoryBoardRepository _boardRepository = new();
    private readonly InMemoryReminderRepository _reminderRepository = new();
    private readonly InMemoryOccurrenceRepository _occurrenceRepository = new();
    private readonly InMemoryBuzzRepository _buzzRepository = new();
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly CancelBuzzesOnReminderDeletedPolicy _sut;
    private readonly ReminderApplicationService _reminderService;
    private readonly OccurrenceApplicationService _occurrenceService;
    private readonly BuzzApplicationService _buzzService;
    private readonly Guid _memberUserId = Guid.CreateVersion7();
    private readonly Board _board;

    public CancelBuzzesOnReminderDeletedPolicyTests()
    {
        var idGenerator = new FakeIdGenerator();
        _buzzService = new BuzzApplicationService(_buzzRepository, _occurrenceRepository, _reminderRepository, _boardRepository, idGenerator, _clock);
        _reminderService = new ReminderApplicationService(_reminderRepository, _boardRepository, idGenerator, _clock);
        _occurrenceService = new OccurrenceApplicationService(
            _occurrenceRepository, _reminderRepository, _boardRepository, _userRepository, idGenerator, _clock);
        _sut = new CancelBuzzesOnReminderDeletedPolicy(_buzzService);

        _board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), _memberUserId, _clock.UtcNow);
        _boardRepository.AddAsync(_board, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task HandleAsync_CancelsPendingBuzzesAcrossTheRemindersOccurrences()
    {
        var reminder = await _reminderService.CreateReminderAsync(
            _memberUserId, _board.Id.Value, "Vet visit", "once", _clock.UtcNow.DateTime.AddDays(1), "atTime", CancellationToken.None);
        var generated = await _occurrenceService.GenerateOccurrencesAsync(_memberUserId, reminder.Value.Id, CancellationToken.None);
        var occurrenceId = generated.Value[0].Id;
        var buzz = await _buzzService.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);
        var buzzId = buzz.Value[0].Id;
        var domainEvent = new ReminderDeleted(Guid.CreateVersion7(), _clock.UtcNow, new(reminder.Value.Id), new(_board.Id.Value));

        await _sut.HandleAsync(domainEvent, CancellationToken.None);

        var cancelledBuzz = await _buzzService.GetBuzzAsync(_memberUserId, buzzId, CancellationToken.None);
        Assert.Equal("cancelled", cancelledBuzz.Value.Status);
    }
}
