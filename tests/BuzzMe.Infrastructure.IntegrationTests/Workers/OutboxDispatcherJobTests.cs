using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Boards;
using BuzzMe.Application.Buzzes;
using BuzzMe.Application.Occurrences;
using BuzzMe.Application.Policies;
using BuzzMe.Application.Reminders;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Buzzes;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Occurrences.Events;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.Reminders.Events;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Domain.Users;
using BuzzMe.Infrastructure.Ids;
using BuzzMe.Infrastructure.IntegrationTests.Workers.TestDoubles;
using BuzzMe.Infrastructure.Persistence.Migrations.Steps;
using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Mongo.Boards;
using BuzzMe.Infrastructure.Persistence.Mongo.Buzzes;
using BuzzMe.Infrastructure.Persistence.Mongo.Occurrences;
using BuzzMe.Infrastructure.Persistence.Mongo.Reminders;
using BuzzMe.Infrastructure.Persistence.Mongo.Users;
using BuzzMe.Infrastructure.Persistence.Outbox;
using BuzzMe.Workers.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuzzMe.Infrastructure.IntegrationTests.Workers;

/// <summary>
/// The Sprint 17 end-to-end pipeline, against real MongoDB and the real DI-wired Policies —
/// mirrors BuzzDeliveryWorkerTests' own "exercise the real orchestration, only the outer
/// PeriodicTimer loop is bypassed" posture. Complete/DismissOccurrence and DeleteReminder
/// each raise their event through the real transactional-outbox write path; a single
/// OutboxDispatcherJob.ProcessBatchAsync call (simulating one poll tick) is then enough to
/// prove the whole "aggregate write → outbox → Policy → Buzz cancelled" chain actually
/// works together, not just each piece in isolation.
/// </summary>
[Collection(MongoIntegrationTestCollection.Name)]
public sealed class OutboxDispatcherJobTests(MongoIntegrationTestFixture fixture) : IAsyncLifetime
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero));
    private ServiceProvider _services = null!;
    private IServiceScope _scope = null!;
    private BoardApplicationService _boardService = null!;
    private ReminderApplicationService _reminderService = null!;
    private OccurrenceApplicationService _occurrenceService = null!;
    private BuzzApplicationService _buzzService = null!;
    private OutboxDispatcherJob _dispatcherJob = null!;

    public async Task InitializeAsync()
    {
        await new CreateBoardIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
        await new CreateReminderIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
        await new CreateOccurrenceIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
        await new CreateBuzzIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
        await new CreateOutboxIndexes(fixture.Context).ApplyAsync(CancellationToken.None);

        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddSingleton(fixture.Context);
        collection.AddSingleton<IClock>(_clock);
        collection.AddSingleton<IIdGenerator, TimeSortableIdGenerator>();
        collection.AddSingleton<IOutboxWriter, MongoOutboxWriter>();
        collection.AddScoped<IOutboxDispatcher, OutboxDispatcher>();

        collection.AddScoped<IBoardRepository, BoardRepository>();
        collection.AddScoped<IReminderRepository, ReminderRepository>();
        collection.AddScoped<IOccurrenceRepository, OccurrenceRepository>();
        collection.AddScoped<IBuzzRepository, BuzzRepository>();
        collection.AddScoped<IUserRepository, UserRepository>();

        collection.AddScoped<BoardApplicationService>();
        collection.AddScoped<ReminderApplicationService>();
        collection.AddScoped<OccurrenceApplicationService>();
        collection.AddScoped<BuzzApplicationService>();

        collection.AddScoped<CancelBuzzesOnOccurrenceResolvedPolicy>();
        collection.AddScoped<IPolicy<OccurrenceCompleted>>(sp => sp.GetRequiredService<CancelBuzzesOnOccurrenceResolvedPolicy>());
        collection.AddScoped<IPolicy<OccurrenceDismissed>>(sp => sp.GetRequiredService<CancelBuzzesOnOccurrenceResolvedPolicy>());
        collection.AddScoped<IPolicy<ReminderDeleted>, CancelBuzzesOnReminderDeletedPolicy>();

        _services = collection.BuildServiceProvider();

        _scope = _services.CreateScope();
        _boardService = _scope.ServiceProvider.GetRequiredService<BoardApplicationService>();
        _reminderService = _scope.ServiceProvider.GetRequiredService<ReminderApplicationService>();
        _occurrenceService = _scope.ServiceProvider.GetRequiredService<OccurrenceApplicationService>();
        _buzzService = _scope.ServiceProvider.GetRequiredService<BuzzApplicationService>();

        _dispatcherJob = new OutboxDispatcherJob(_services.GetRequiredService<IServiceScopeFactory>(), NullLogger<OutboxDispatcherJob>.Instance);
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        _services.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CompletingAnOccurrence_EventuallyCancelsItsOwnPendingBuzz()
    {
        var ownerUserId = Guid.CreateVersion7();
        var board = await _boardService.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);
        var reminder = await _reminderService.CreateReminderAsync(
            ownerUserId, board.Value.Id, "Vet visit", "once", _clock.UtcNow.DateTime.AddDays(1), "atTime", CancellationToken.None);
        var generated = await _occurrenceService.GenerateOccurrencesAsync(ownerUserId, reminder.Value.Id, CancellationToken.None);
        var occurrenceId = generated.Value[0].Id;
        var buzzResult = await _buzzService.GenerateBuzzesAsync(ownerUserId, occurrenceId, CancellationToken.None);
        var buzzId = buzzResult.Value[0].Id;

        var completeResult = await _occurrenceService.CompleteOccurrenceAsync(
            ownerUserId, reminder.Value.Id, occurrenceId, expectedVersion: 0, CancellationToken.None);
        Assert.True(completeResult.IsSuccess);

        await _dispatcherJob.ProcessBatchAsync(CancellationToken.None);

        var buzz = await _buzzService.GetBuzzAsync(ownerUserId, buzzId, CancellationToken.None);
        Assert.Equal("cancelled", buzz.Value.Status);
    }

    [Fact]
    public async Task DeletingAReminder_EventuallyCancelsPendingBuzzesForItsUnresolvedOccurrences()
    {
        var ownerUserId = Guid.CreateVersion7();
        var board = await _boardService.CreateBoardAsync(ownerUserId, "Family", CancellationToken.None);
        var reminder = await _reminderService.CreateReminderAsync(
            ownerUserId, board.Value.Id, "Daily standup", "daily", _clock.UtcNow.DateTime.AddDays(-1), "atTime", CancellationToken.None);
        var generated = await _occurrenceService.GenerateOccurrencesAsync(ownerUserId, reminder.Value.Id, CancellationToken.None);
        Assert.True(generated.Value.Count >= 1);
        var occurrenceId = generated.Value[0].Id;
        var buzzResult = await _buzzService.GenerateBuzzesAsync(ownerUserId, occurrenceId, CancellationToken.None);
        var buzzId = buzzResult.Value[0].Id;

        var deleteResult = await _reminderService.DeleteReminderAsync(ownerUserId, reminder.Value.Id, CancellationToken.None);
        Assert.True(deleteResult.IsSuccess);

        await _dispatcherJob.ProcessBatchAsync(CancellationToken.None);

        var buzz = await _buzzService.GetBuzzAsync(ownerUserId, buzzId, CancellationToken.None);
        Assert.Equal("cancelled", buzz.Value.Status);
    }
}
