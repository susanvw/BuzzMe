using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.Reminders.Events;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Infrastructure.IntegrationTests.Workers.TestDoubles;
using BuzzMe.Infrastructure.Persistence.Migrations.Steps;
using BuzzMe.Infrastructure.Persistence.Mongo.Reminders;
using BuzzMe.Infrastructure.Persistence.Outbox;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.IntegrationTests.Reminders;

/// <summary>Against a real, ephemeral MongoDB — Sprint 2's explicit "do not mock repositories."</summary>
[Collection(MongoIntegrationTestCollection.Name)]
public sealed class ReminderRepositoryTests(MongoIntegrationTestFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly BoardId SomeBoardId = new(Guid.CreateVersion7());

    private ReminderRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _repository = new ReminderRepository(fixture.Context, new MongoOutboxWriter(fixture.Context, new TestClock(Now)));
        await new CreateReminderIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static Reminder NewReminder(BoardId? boardId = null, string title = "Vet visit") => Reminder.Create(
        new ReminderId(Guid.CreateVersion7()),
        boardId ?? SomeBoardId,
        new ReminderTitle(title),
        new ReminderSchedule(Recurrence.Yearly, new DateTime(2026, 7, 9, 16, 0, 0), "UTC"),
        NotifyPreset.OneDayBefore,
        Now);

    [Fact]
    public async Task AddAsync_PersistsTheReminderAtVersionZero()
    {
        var reminder = NewReminder();

        await _repository.AddAsync(reminder, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(reminder.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.Version);
        Assert.Equal("Vet visit", reloaded.Title.Value);
        Assert.Equal(Recurrence.Yearly, reloaded.Schedule.Recurrence);
        Assert.Equal(NotifyPreset.OneDayBefore, reloaded.NotifyPreset);
        Assert.Equal("UTC", reloaded.Schedule.ReferenceTimezone);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForAnUnknownId()
    {
        var result = await _repository.GetByIdAsync(new ReminderId(Guid.CreateVersion7()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListByBoardAsync_OnlyReturnsRemindersOnTheGivenBoard()
    {
        var boardId = new BoardId(Guid.CreateVersion7());
        var otherBoardId = new BoardId(Guid.CreateVersion7());
        var ownReminder = NewReminder(boardId, "Board reminder");
        var otherReminder = NewReminder(otherBoardId, "Other board's reminder");
        await _repository.AddAsync(ownReminder, CancellationToken.None);
        await _repository.AddAsync(otherReminder, CancellationToken.None);

        var results = await _repository.ListByBoardAsync(boardId, afterId: null, limit: 20, CancellationToken.None);

        var found = Assert.Single(results);
        Assert.Equal(ownReminder.Id, found.Id);
    }

    [Fact]
    public async Task MarkDeletedAsync_ExcludesTheReminderFromNormalReadsButPreservesTheDocument()
    {
        // Soft delete (Sprint 3.1) — MarkDeletedAsync is a targeted update, not a document
        // removal. Normal reads (GetByIdAsync) must exclude it; GetByIdIncludingDeletedAsync
        // must still find it, with DeletedAt set.
        var reminder = NewReminder();
        await _repository.AddAsync(reminder, CancellationToken.None);

        reminder.Delete(Now);
        await _repository.MarkDeletedAsync(reminder, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(reminder.Id, CancellationToken.None);
        var reloadedIncludingDeleted = await _repository.GetByIdIncludingDeletedAsync(reminder.Id, CancellationToken.None);

        Assert.Null(reloaded);
        Assert.NotNull(reloadedIncludingDeleted);
        Assert.Equal(Now, reloadedIncludingDeleted.DeletedAt);
    }

    [Fact]
    public async Task MarkDeletedAsync_IsIdempotentWhenCalledAgainOnAnAlreadyDeletedReminder()
    {
        var reminder = NewReminder();
        await _repository.AddAsync(reminder, CancellationToken.None);
        reminder.Delete(Now);
        await _repository.MarkDeletedAsync(reminder, CancellationToken.None);

        reminder.Delete(Now);
        await _repository.MarkDeletedAsync(reminder, CancellationToken.None);
        var reloadedIncludingDeleted = await _repository.GetByIdIncludingDeletedAsync(reminder.Id, CancellationToken.None);

        Assert.NotNull(reloadedIncludingDeleted);
        Assert.Equal(Now, reloadedIncludingDeleted.DeletedAt);
    }

    [Fact]
    public async Task ListByBoardAsync_ExcludesSoftDeletedReminders()
    {
        var boardId = new BoardId(Guid.CreateVersion7());
        var reminder = NewReminder(boardId, "Soon to be deleted");
        await _repository.AddAsync(reminder, CancellationToken.None);

        reminder.Delete(Now);
        await _repository.MarkDeletedAsync(reminder, CancellationToken.None);
        var results = await _repository.ListByBoardAsync(boardId, afterId: null, limit: 20, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ListByBoardAsync_RespectsTheCursorAndLimit()
    {
        var boardId = new BoardId(Guid.CreateVersion7());
        for (var i = 0; i < 3; i++)
            await _repository.AddAsync(NewReminder(boardId, $"Reminder {i}"), CancellationToken.None);

        var firstPage = await _repository.ListByBoardAsync(boardId, afterId: null, limit: 2, CancellationToken.None);
        Assert.Equal(2, firstPage.Count);

        var secondPage = await _repository.ListByBoardAsync(boardId, afterId: firstPage[^1].Id.Value, limit: 2, CancellationToken.None);
        Assert.Single(secondPage);
        Assert.DoesNotContain(secondPage, reminder => firstPage.Select(item => item.Id).Contains(reminder.Id));
    }

    [Fact]
    public async Task UpdateAsync_PersistsAnEditAndIncrementsVersion()
    {
        var reminder = NewReminder();
        await _repository.AddAsync(reminder, CancellationToken.None);
        var newSchedule = new ReminderSchedule(Recurrence.Monthly, reminder.Schedule.StartDate, reminder.Schedule.ReferenceTimezone);
        reminder.Update(new ReminderTitle("Vet checkup"), newSchedule, NotifyPreset.OneHourBefore, Now.AddDays(1));

        await _repository.UpdateAsync(reminder, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(reminder.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(1, reloaded.Version);
        Assert.Equal("Vet checkup", reloaded.Title.Value);
        Assert.Equal(Recurrence.Monthly, reloaded.Schedule.Recurrence);
        Assert.Equal(NotifyPreset.OneHourBefore, reloaded.NotifyPreset);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsConcurrencyConflictExceptionWhenTheVersionIsStale()
    {
        var reminder = NewReminder();
        await _repository.AddAsync(reminder, CancellationToken.None);
        var staleCopy = await _repository.GetByIdAsync(reminder.Id, CancellationToken.None);
        Assert.NotNull(staleCopy);

        reminder.Update(new ReminderTitle("Vet checkup"), reminder.Schedule, reminder.NotifyPreset, Now.AddDays(1));
        await _repository.UpdateAsync(reminder, CancellationToken.None);

        staleCopy.Update(new ReminderTitle("Something else"), staleCopy.Schedule, staleCopy.NotifyPreset, Now.AddDays(2));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _repository.UpdateAsync(staleCopy, CancellationToken.None));
    }

    [Fact]
    public async Task MarkDeletedAsync_WritesReminderDeletedToTheOutboxInTheSameTransaction()
    {
        // DEVELOPMENT_GUIDE.md §7 — the DeletedAt update and the outbox row for
        // ReminderDeleted commit together (Sprint 17).
        var reminder = NewReminder();
        await _repository.AddAsync(reminder, CancellationToken.None);
        reminder.Delete(Now);
        var raisedEventId = reminder.DomainEvents.OfType<ReminderDeleted>().Single().EventId;

        await _repository.MarkDeletedAsync(reminder, CancellationToken.None);

        var outboxRow = await fixture.Context.Outbox
            .Find(Builders<OutboxMessage>.Filter.Eq(d => d.Id, raisedEventId))
            .FirstOrDefaultAsync();
        Assert.NotNull(outboxRow);
        Assert.Equal(nameof(ReminderDeleted), outboxRow.EventType);
        Assert.Null(outboxRow.ProcessedAt);
        Assert.Empty(reminder.DomainEvents);
    }
}
