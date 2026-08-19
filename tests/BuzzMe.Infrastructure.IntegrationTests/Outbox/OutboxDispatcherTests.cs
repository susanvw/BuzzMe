using System.Text.Json;
using BuzzMe.Application.Abstractions;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.Reminders.Events;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Infrastructure.IntegrationTests.Workers.TestDoubles;
using BuzzMe.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.IntegrationTests.Outbox;

/// <summary>
/// Against a real, ephemeral MongoDB — exercises OutboxDispatcher's own claim/dispatch/
/// mark-processed mechanics in isolation, using a throwing fake Policy where the scenario
/// calls for one (not one of the real Sprint 17 Policies, which are covered end to end by
/// OccurrenceRepositoryTests/ReminderRepositoryTests' own outbox-write tests and
/// BuzzDeliveryWorkerTests' precedent-following style).
/// </summary>
[Collection(MongoIntegrationTestCollection.Name)]
public sealed class OutboxDispatcherTests(MongoIntegrationTestFixture fixture) : IAsyncLifetime
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero));

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    private OutboxDispatcher CreateDispatcher(IServiceProvider serviceProvider) =>
        new(fixture.Context, serviceProvider, _clock, NullLogger<OutboxDispatcher>.Instance);

    private async Task InsertRowAsync(string eventType, string payloadJson, DateTimeOffset availableAt)
    {
        await fixture.Context.Outbox.InsertOneAsync(new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            EventType = eventType,
            PayloadJson = payloadJson,
            OccurredAt = _clock.UtcNow,
            AvailableAt = availableAt,
        });
    }

    private sealed class RecordingPolicy : IPolicy<ReminderDeleted>
    {
        public int CallCount { get; private set; }

        public Task HandleAsync(ReminderDeleted domainEvent, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPolicy : IPolicy<ReminderDeleted>
    {
        public Task HandleAsync(ReminderDeleted domainEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated policy failure.");
    }

    [Fact]
    public async Task DispatchPendingBatchAsync_InvokesTheRegisteredPolicyAndMarksTheRowProcessed()
    {
        var reminderId = Guid.CreateVersion7();
        var domainEvent = new ReminderDeleted(Guid.CreateVersion7(), _clock.UtcNow, new ReminderId(reminderId), new(Guid.CreateVersion7()));
        await InsertRowAsync(nameof(ReminderDeleted), JsonSerializer.Serialize(domainEvent), _clock.UtcNow);
        var policy = new RecordingPolicy();
        var services = new ServiceCollection().AddSingleton<IPolicy<ReminderDeleted>>(policy).BuildServiceProvider();
        var dispatcher = CreateDispatcher(services);

        var claimedCount = await dispatcher.DispatchPendingBatchAsync(batchSize: 10, CancellationToken.None);

        Assert.True(claimedCount >= 1);
        Assert.Equal(1, policy.CallCount);
        var row = await fixture.Context.Outbox
            .Find(Builders<OutboxMessage>.Filter.Eq(d => d.EventType, nameof(ReminderDeleted))
                & Builders<OutboxMessage>.Filter.Eq(d => d.PayloadJson, JsonSerializer.Serialize(domainEvent)))
            .FirstOrDefaultAsync();
        Assert.NotNull(row);
        Assert.NotNull(row.ProcessedAt);
    }

    [Fact]
    public async Task DispatchPendingBatchAsync_ForAnUnrecognizedEventType_MarksTheRowProcessedWithoutInvokingAnything()
    {
        var payload = "{}";
        await InsertRowAsync("SomeFutureEventTypeNoPolicyHandlesYet", payload, _clock.UtcNow);
        var services = new ServiceCollection().BuildServiceProvider();
        var dispatcher = CreateDispatcher(services);

        await dispatcher.DispatchPendingBatchAsync(batchSize: 10, CancellationToken.None);

        var row = await fixture.Context.Outbox
            .Find(Builders<OutboxMessage>.Filter.Eq(d => d.EventType, "SomeFutureEventTypeNoPolicyHandlesYet"))
            .FirstOrDefaultAsync();
        Assert.NotNull(row);
        Assert.NotNull(row.ProcessedAt);
    }

    [Fact]
    public async Task DispatchPendingBatchAsync_WhenAPolicyThrows_LeavesTheRowUnprocessedForRetry()
    {
        var domainEvent = new ReminderDeleted(Guid.CreateVersion7(), _clock.UtcNow, new ReminderId(Guid.CreateVersion7()), new(Guid.CreateVersion7()));
        var payloadJson = JsonSerializer.Serialize(domainEvent);
        await InsertRowAsync(nameof(ReminderDeleted), payloadJson, _clock.UtcNow);
        var services = new ServiceCollection().AddSingleton<IPolicy<ReminderDeleted>>(new ThrowingPolicy()).BuildServiceProvider();
        var dispatcher = CreateDispatcher(services);

        await dispatcher.DispatchPendingBatchAsync(batchSize: 10, CancellationToken.None);

        var row = await fixture.Context.Outbox
            .Find(Builders<OutboxMessage>.Filter.Eq(d => d.EventType, nameof(ReminderDeleted))
                & Builders<OutboxMessage>.Filter.Eq(d => d.PayloadJson, payloadJson))
            .FirstOrDefaultAsync();
        Assert.NotNull(row);
        Assert.Null(row.ProcessedAt);
        Assert.Equal(1, row.Attempts);
        Assert.True(row.AvailableAt > _clock.UtcNow);
    }
}
