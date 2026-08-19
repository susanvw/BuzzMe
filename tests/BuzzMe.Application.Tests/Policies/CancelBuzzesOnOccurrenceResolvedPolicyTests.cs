using BuzzMe.Application.Buzzes;
using BuzzMe.Application.Occurrences;
using BuzzMe.Application.Policies;
using BuzzMe.Application.Reminders;
using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Occurrences.Events;
using BuzzMe.Domain.Reminders;

namespace BuzzMe.Application.Tests.Policies;

/// <summary>
/// Verifies the Policy actually reaches BuzzApplicationService.CancelBuzzesForOccurrenceAsync
/// with the event's own OccurrenceId/OccurredAt — against a real BuzzApplicationService and
/// in-memory repositories, not a mock, same "exercise the real orchestration" posture as
/// every other Application-layer test in this codebase.
/// </summary>
public sealed class CancelBuzzesOnOccurrenceResolvedPolicyTests
{
    private readonly InMemoryBoardRepository _boardRepository = new();
    private readonly InMemoryReminderRepository _reminderRepository = new();
    private readonly InMemoryOccurrenceRepository _occurrenceRepository = new();
    private readonly InMemoryBuzzRepository _buzzRepository = new();
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly CancelBuzzesOnOccurrenceResolvedPolicy _sut;
    private readonly ReminderApplicationService _reminderService;
    private readonly OccurrenceApplicationService _occurrenceService;
    private readonly BuzzApplicationService _buzzService;
    private readonly Guid _memberUserId = Guid.CreateVersion7();
    private readonly Board _board;

    public CancelBuzzesOnOccurrenceResolvedPolicyTests()
    {
        var idGenerator = new FakeIdGenerator();
        _buzzService = new BuzzApplicationService(_buzzRepository, _occurrenceRepository, _reminderRepository, _boardRepository, idGenerator, _clock);
        _reminderService = new ReminderApplicationService(_reminderRepository, _boardRepository, idGenerator, _clock);
        _occurrenceService = new OccurrenceApplicationService(
            _occurrenceRepository, _reminderRepository, _boardRepository, _userRepository, idGenerator, _clock);
        _sut = new CancelBuzzesOnOccurrenceResolvedPolicy(_buzzService);

        _board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), _memberUserId, _clock.UtcNow);
        _boardRepository.AddAsync(_board, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task<(Guid OccurrenceId, Guid BuzzId)> SeedOccurrenceWithABuzzAsync()
    {
        var reminder = await _reminderService.CreateReminderAsync(
            _memberUserId, _board.Id.Value, "Vet visit", "once", _clock.UtcNow.DateTime.AddDays(1), "atTime", CancellationToken.None);
        var generated = await _occurrenceService.GenerateOccurrencesAsync(_memberUserId, reminder.Value.Id, CancellationToken.None);
        var occurrenceId = generated.Value[0].Id;
        var buzz = await _buzzService.GenerateBuzzesAsync(_memberUserId, occurrenceId, CancellationToken.None);
        return (occurrenceId, buzz.Value[0].Id);
    }

    [Fact]
    public async Task HandleAsync_OccurrenceCompleted_CancelsThatOccurrencesBuzz()
    {
        var (occurrenceId, buzzId) = await SeedOccurrenceWithABuzzAsync();
        var domainEvent = new OccurrenceCompleted(Guid.CreateVersion7(), _clock.UtcNow, new(occurrenceId), new(Guid.CreateVersion7()), _memberUserId);

        await _sut.HandleAsync(domainEvent, CancellationToken.None);

        var buzz = await _buzzService.GetBuzzAsync(_memberUserId, buzzId, CancellationToken.None);
        Assert.Equal("cancelled", buzz.Value.Status);
    }

    [Fact]
    public async Task HandleAsync_OccurrenceDismissed_CancelsThatOccurrencesBuzz()
    {
        var (occurrenceId, buzzId) = await SeedOccurrenceWithABuzzAsync();
        var domainEvent = new OccurrenceDismissed(Guid.CreateVersion7(), _clock.UtcNow, new(occurrenceId), new(Guid.CreateVersion7()), _memberUserId);

        await _sut.HandleAsync(domainEvent, CancellationToken.None);

        var buzz = await _buzzService.GetBuzzAsync(_memberUserId, buzzId, CancellationToken.None);
        Assert.Equal("cancelled", buzz.Value.Status);
    }
}
