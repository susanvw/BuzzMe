using BuzzMe.Application.Boards;
using BuzzMe.Application.Buzzes;
using BuzzMe.Application.Occurrences;
using BuzzMe.Application.Reminders;
using BuzzMe.Domain.Buzzes;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Infrastructure.Ids;
using BuzzMe.Infrastructure.IntegrationTests.Workers.TestDoubles;
using BuzzMe.Infrastructure.Persistence.Migrations.Steps;
using BuzzMe.Infrastructure.Persistence.Mongo.Boards;
using BuzzMe.Infrastructure.Persistence.Mongo.Buzzes;
using BuzzMe.Infrastructure.Persistence.Mongo.Occurrences;
using BuzzMe.Infrastructure.Persistence.Mongo.Reminders;
using BuzzMe.Infrastructure.Persistence.Mongo.Users;
using BuzzMe.Workers.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuzzMe.Infrastructure.IntegrationTests.Workers;

/// <summary>
/// Sprint 6's worker and end-to-end pipeline tests — against real MongoDB, real Application
/// Services, real BuzzDeliveryWorker; only INotificationDispatcher is mocked (the Sprint 6
/// brief's own instruction). Exercises BuzzDeliveryWorker.ProcessBatchAsync directly
/// (internal — see BuzzMe.Workers.csproj's InternalsVisibleTo), bypassing the PeriodicTimer
/// loop, which is trivial framework glue, not behavior worth testing here.
///
/// Assertions are written to tolerate other tests' leftover due Buzzes: this whole test
/// project shares one MongoDB database per collection (MongoIntegrationTestFixture), and
/// ClaimPendingAsync's query is deliberately global (real work-queue semantics, not scoped
/// to one Board/Occurrence) — so a batch here can legitimately also sweep up an unrelated
/// Buzz from another test class. Every assertion below checks for *this test's own* Buzz
/// specifically (by Occurrence, or by tracking its own BuzzId in the fake dispatcher),
/// never a raw total.
/// </summary>
[Collection(MongoIntegrationTestCollection.Name)]
public sealed class BuzzDeliveryWorkerTests(MongoIntegrationTestFixture fixture) : IAsyncLifetime
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero));
    private BuzzRepository _buzzRepository = null!;
    private BoardApplicationService _boardService = null!;
    private ReminderApplicationService _reminderService = null!;
    private OccurrenceApplicationService _occurrenceService = null!;
    private BuzzApplicationService _buzzService = null!;

    public async Task InitializeAsync()
    {
        var boardRepository = new BoardRepository(fixture.Context);
        var userRepository = new UserRepository(fixture.Context);
        var reminderRepository = new ReminderRepository(fixture.Context);
        var occurrenceRepository = new OccurrenceRepository(fixture.Context);
        _buzzRepository = new BuzzRepository(fixture.Context);

        await new CreateBoardIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
        await new CreateReminderIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
        await new CreateOccurrenceIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
        await new CreateBuzzIndexes(fixture.Context).ApplyAsync(CancellationToken.None);

        var idGenerator = new TimeSortableIdGenerator();
        _boardService = new BoardApplicationService(boardRepository, userRepository, idGenerator, _clock);
        _reminderService = new ReminderApplicationService(reminderRepository, boardRepository, idGenerator, _clock);
        _occurrenceService = new OccurrenceApplicationService(
            occurrenceRepository, reminderRepository, boardRepository, userRepository, idGenerator, _clock);
        _buzzService = new BuzzApplicationService(_buzzRepository, occurrenceRepository, reminderRepository, boardRepository, idGenerator, _clock);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private BuzzDeliveryWorker CreateWorker(FakeNotificationDispatcher dispatcher)
    {
        var provider = new ServiceCollection().AddSingleton(_buzzService).BuildServiceProvider();
        return new BuzzDeliveryWorker(provider.GetRequiredService<IServiceScopeFactory>(), dispatcher, NullLogger<BuzzDeliveryWorker>.Instance);
    }

    private async Task<(Guid occurrenceId, Guid buzzId)> SeedDueOccurrenceWithABuzzAsync(Guid ownerUserId)
    {
        var board = (await _boardService.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None)).Value;
        var reminder = (await _reminderService.CreateReminderAsync(
            ownerUserId, board.Id, "Vet visit", "once", _clock.UtcNow.DateTime.AddDays(1), "atTime", CancellationToken.None)).Value;
        var occurrences = (await _occurrenceService.GenerateOccurrencesAsync(ownerUserId, reminder.Id, CancellationToken.None)).Value;
        var occurrenceId = occurrences[0].Id;

        var generated = await _buzzService.GenerateBuzzesAsync(ownerUserId, occurrenceId, CancellationToken.None);
        return (occurrenceId, generated.Value[0].Id);
    }

    [Fact]
    public async Task ProcessBatchAsync_ClaimsDispatchesAndMarksDelivered()
    {
        var (occurrenceId, buzzId) = await SeedDueOccurrenceWithABuzzAsync(Guid.CreateVersion7());
        _clock.Advance(TimeSpan.FromDays(2));
        var dispatcher = new FakeNotificationDispatcher(succeeds: true);

        await CreateWorker(dispatcher).ProcessBatchAsync(CancellationToken.None);

        var buzz = Assert.Single(await _buzzRepository.ListByOccurrenceAsync(new OccurrenceId(occurrenceId), CancellationToken.None));
        Assert.Equal(BuzzStatus.Delivered, buzz.Status);
        Assert.Single(dispatcher.DispatchedBuzzIds, id => id == new BuzzId(buzzId));
    }

    [Fact]
    public async Task ProcessBatchAsync_MarksFailedWhenTheDispatcherReportsFailure()
    {
        var (occurrenceId, _) = await SeedDueOccurrenceWithABuzzAsync(Guid.CreateVersion7());
        _clock.Advance(TimeSpan.FromDays(2));
        var dispatcher = new FakeNotificationDispatcher(succeeds: false);

        await CreateWorker(dispatcher).ProcessBatchAsync(CancellationToken.None);

        var buzz = Assert.Single(await _buzzRepository.ListByOccurrenceAsync(new OccurrenceId(occurrenceId), CancellationToken.None));
        Assert.Equal(BuzzStatus.Failed, buzz.Status);
    }

    [Fact]
    public async Task ProcessBatchAsync_DoesNotClaimABuzzNotYetDue()
    {
        var (occurrenceId, buzzId) = await SeedDueOccurrenceWithABuzzAsync(Guid.CreateVersion7());
        var dispatcher = new FakeNotificationDispatcher(succeeds: true);

        await CreateWorker(dispatcher).ProcessBatchAsync(CancellationToken.None);

        var buzz = Assert.Single(await _buzzRepository.ListByOccurrenceAsync(new OccurrenceId(occurrenceId), CancellationToken.None));
        Assert.Equal(BuzzStatus.Scheduled, buzz.Status);
        Assert.DoesNotContain(new BuzzId(buzzId), dispatcher.DispatchedBuzzIds);
    }

    [Fact]
    public async Task ProcessBatchAsync_CalledTwice_DoesNotProcessTheSameBuzzTwice()
    {
        var (_, buzzId) = await SeedDueOccurrenceWithABuzzAsync(Guid.CreateVersion7());
        _clock.Advance(TimeSpan.FromDays(2));
        var dispatcher = new FakeNotificationDispatcher(succeeds: true);
        var worker = CreateWorker(dispatcher);
        await worker.ProcessBatchAsync(CancellationToken.None);

        await worker.ProcessBatchAsync(CancellationToken.None);

        Assert.Single(dispatcher.DispatchedBuzzIds, id => id == new BuzzId(buzzId));
    }

    [Fact]
    public async Task EndToEnd_ReminderToOccurrenceToBuzzToWorkerToDelivered()
    {
        // Sprint 6's explicitly required minimum verification: Reminder → Occurrence →
        // Buzz → Worker → Delivered, entirely through real Application Services and real
        // MongoDB.
        var ownerUserId = Guid.CreateVersion7();
        var (occurrenceId, buzzId) = await SeedDueOccurrenceWithABuzzAsync(ownerUserId);

        _clock.Advance(TimeSpan.FromDays(2));
        var dispatcher = new FakeNotificationDispatcher(succeeds: true);
        await CreateWorker(dispatcher).ProcessBatchAsync(CancellationToken.None);

        var buzz = Assert.Single(await _buzzRepository.ListByOccurrenceAsync(new OccurrenceId(occurrenceId), CancellationToken.None));
        Assert.Equal(buzzId, buzz.Id.Value);
        Assert.Equal(BuzzStatus.Delivered, buzz.Status);
        Assert.Equal(ownerUserId, buzz.RecipientUserId);
    }
}
