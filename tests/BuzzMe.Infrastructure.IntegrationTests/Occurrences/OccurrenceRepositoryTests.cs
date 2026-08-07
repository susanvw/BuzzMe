using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;
using BuzzMe.Infrastructure.Persistence.Migrations.Steps;
using BuzzMe.Infrastructure.Persistence.Mongo.Occurrences;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.IntegrationTests.Occurrences;

/// <summary>Against a real, ephemeral MongoDB — Sprint 3's explicit "use real MongoDB."</summary>
[Collection(MongoIntegrationTestCollection.Name)]
public sealed class OccurrenceRepositoryTests(MongoIntegrationTestFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly ReminderId SomeReminderId = new(Guid.CreateVersion7());

    private OccurrenceRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _repository = new OccurrenceRepository(fixture.Context);
        await new CreateOccurrenceIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static Occurrence NewOccurrence(ReminderId? reminderId = null, DateTimeOffset? dueAt = null) =>
        Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), reminderId ?? SomeReminderId, dueAt ?? Now, Now);

    [Fact]
    public async Task AddAsync_PersistsTheOccurrenceAtVersionZero()
    {
        var occurrence = NewOccurrence();

        await _repository.AddAsync(occurrence, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(occurrence.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.Version);
        Assert.Equal(OccurrenceStatus.Scheduled, reloaded.Status);
        Assert.Equal(occurrence.DueAt, reloaded.DueAt);
    }

    [Fact]
    public async Task AddAsync_RejectsADuplicateReminderIdAndDueAtCombination()
    {
        // The unique index is the real enforcement of "cannot generate duplicate
        // occurrences" (IMPLEMENTATION_SPEC.md §1's idempotency key) — verified here at the
        // database level, not just trusted from application logic.
        var reminderId = new ReminderId(Guid.CreateVersion7());
        var dueAt = Now;
        await _repository.AddAsync(Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), reminderId, dueAt, Now), CancellationToken.None);

        var duplicate = Occurrence.Generate(new OccurrenceId(Guid.CreateVersion7()), reminderId, dueAt, Now);
        await Assert.ThrowsAsync<MongoWriteException>(() => _repository.AddAsync(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForAnUnknownId()
    {
        var result = await _repository.GetByIdAsync(new OccurrenceId(Guid.CreateVersion7()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListByReminderAsync_OnlyReturnsOccurrencesForTheGivenReminder()
    {
        var reminderId = new ReminderId(Guid.CreateVersion7());
        var otherReminderId = new ReminderId(Guid.CreateVersion7());
        var own = NewOccurrence(reminderId, Now);
        var other = NewOccurrence(otherReminderId, Now);
        await _repository.AddAsync(own, CancellationToken.None);
        await _repository.AddAsync(other, CancellationToken.None);

        var results = await _repository.ListByReminderAsync(reminderId, afterId: null, limit: 20, CancellationToken.None);

        var found = Assert.Single(results);
        Assert.Equal(own.Id, found.Id);
    }

    [Fact]
    public async Task CountByReminderAsync_CountsOnlyThatRemindersOccurrences()
    {
        var reminderId = new ReminderId(Guid.CreateVersion7());
        await _repository.AddAsync(NewOccurrence(reminderId, Now), CancellationToken.None);
        await _repository.AddAsync(NewOccurrence(reminderId, Now.AddDays(1)), CancellationToken.None);
        await _repository.AddAsync(NewOccurrence(new ReminderId(Guid.CreateVersion7()), Now), CancellationToken.None);

        var count = await _repository.CountByReminderAsync(reminderId, CancellationToken.None);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetLatestByReminderAsync_ReturnsTheOccurrenceWithTheHighestDueAt()
    {
        var reminderId = new ReminderId(Guid.CreateVersion7());
        await _repository.AddAsync(NewOccurrence(reminderId, Now), CancellationToken.None);
        var latest = NewOccurrence(reminderId, Now.AddDays(5));
        await _repository.AddAsync(latest, CancellationToken.None);
        await _repository.AddAsync(NewOccurrence(reminderId, Now.AddDays(2)), CancellationToken.None);

        var result = await _repository.GetLatestByReminderAsync(reminderId, CancellationToken.None);

        Assert.Equal(latest.Id, result?.Id);
    }

    [Fact]
    public async Task GetLatestByReminderAsync_ReturnsNullWhenNoneExist()
    {
        var result = await _repository.GetLatestByReminderAsync(new ReminderId(Guid.CreateVersion7()), CancellationToken.None);

        Assert.Null(result);
    }
}
