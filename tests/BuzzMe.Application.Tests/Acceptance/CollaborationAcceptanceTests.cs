using BuzzMe.Application.Boards;
using BuzzMe.Application.Buzzes;
using BuzzMe.Application.Invitations;
using BuzzMe.Application.Occurrences;
using BuzzMe.Application.Reminders;
using BuzzMe.Application.Tests.TestDoubles;
using BuzzMe.Domain.Boards;

namespace BuzzMe.Application.Tests.Acceptance;

/// <summary>
/// Sprint 5's explicit permanent acceptance test: Owner creates a Board, invites User A,
/// User A accepts — the Board now has two Members — an existing Reminder's existing
/// Occurrence generates exactly one Buzz per Member, and generating again produces no
/// duplicates. This is the first test in the suite that exercises Board, Reminder,
/// Occurrence, Buzz, and Invitation together — the full "Reminder → Occurrence → Buzz"
/// pipeline (Sprint 4) now driven by real collaboration (Sprint 5) instead of a
/// single-Member Board.
/// </summary>
public sealed class CollaborationAcceptanceTests
{
    private readonly InMemoryBoardRepository _boardRepository = new();
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly InMemoryReminderRepository _reminderRepository = new();
    private readonly InMemoryOccurrenceRepository _occurrenceRepository = new();
    private readonly InMemoryBuzzRepository _buzzRepository = new();
    private readonly InMemoryInvitationRepository _invitationRepository = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero));

    private readonly BoardApplicationService _boardService;
    private readonly ReminderApplicationService _reminderService;
    private readonly OccurrenceApplicationService _occurrenceService;
    private readonly BuzzApplicationService _buzzService;
    private readonly InvitationApplicationService _invitationService;

    public CollaborationAcceptanceTests()
    {
        var idGenerator = new FakeIdGenerator();
        _boardService = new BoardApplicationService(_boardRepository, _userRepository, idGenerator, _clock);
        _reminderService = new ReminderApplicationService(_reminderRepository, _boardRepository, idGenerator, _clock);
        _occurrenceService = new OccurrenceApplicationService(_occurrenceRepository, _reminderRepository, _boardRepository, idGenerator, _clock);
        _buzzService = new BuzzApplicationService(_buzzRepository, _occurrenceRepository, _reminderRepository, _boardRepository, idGenerator, _clock);
        _invitationService = new InvitationApplicationService(
            _invitationRepository, _boardRepository, new FakeInvitationTokenGenerator(), idGenerator, _clock);
    }

    [Fact]
    public async Task OwnerInvitesAMember_BothReceiveExactlyOneBuzzEach_AndRegeneratingCreatesNoDuplicates()
    {
        var ownerUserId = Guid.CreateVersion7();
        var userAId = Guid.CreateVersion7();
        var cancellationToken = CancellationToken.None;

        // Owner creates Board.
        var board = (await _boardService.CreateBoardAsync(ownerUserId, "Family", cancellationToken)).Value;

        // Owner invites User A.
        var invitation = (await _invitationService.InviteMemberAsync(
            ownerUserId, board.Id, "link", null, cancellationToken)).Value;

        // User A accepts.
        var acceptResult = await _invitationService.AcceptInvitationAsync(userAId, invitation.Token, cancellationToken);
        Assert.True(acceptResult.IsSuccess);

        // Board now has two Members.
        var reloadedBoard = await _boardRepository.GetByIdAsync(new BoardId(board.Id), cancellationToken);
        Assert.NotNull(reloadedBoard);
        Assert.Equal(2, reloadedBoard.Memberships.Count);
        Assert.True(reloadedBoard.HasMember(ownerUserId));
        Assert.True(reloadedBoard.HasMember(userAId));

        // Reminder already exists.
        var reminder = (await _reminderService.CreateReminderAsync(
            ownerUserId, board.Id, "Bin day", "weekly", _clock.UtcNow.DateTime.AddDays(1), "atTime", cancellationToken)).Value;

        // Occurrence already exists.
        var generatedOccurrences = (await _occurrenceService.GenerateOccurrencesAsync(ownerUserId, reminder.Id, cancellationToken)).Value;
        var occurrenceId = generatedOccurrences[0].Id;

        // GenerateBuzzes() creates exactly two Buzzes.
        var firstGeneration = await _buzzService.GenerateBuzzesAsync(ownerUserId, occurrenceId, cancellationToken);
        Assert.True(firstGeneration.IsSuccess);
        Assert.Equal(2, firstGeneration.Value.Count);
        Assert.Contains(firstGeneration.Value, buzz => buzz.RecipientUserId == ownerUserId);
        Assert.Contains(firstGeneration.Value, buzz => buzz.RecipientUserId == userAId);

        // Running GenerateBuzzes() again creates no duplicates.
        var secondGeneration = await _buzzService.GenerateBuzzesAsync(ownerUserId, occurrenceId, cancellationToken);
        Assert.True(secondGeneration.IsSuccess);
        Assert.Empty(secondGeneration.Value);

        var allBuzzesForOccurrence = await _buzzRepository.ListByOccurrenceAsync(
            new Domain.Occurrences.OccurrenceId(occurrenceId), cancellationToken);
        Assert.Equal(2, allBuzzesForOccurrence.Count);
    }
}
