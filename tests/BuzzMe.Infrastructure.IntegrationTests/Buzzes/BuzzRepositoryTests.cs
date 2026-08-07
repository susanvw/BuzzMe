using BuzzMe.Domain.Buzzes;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Infrastructure.Persistence.Migrations.Steps;
using BuzzMe.Infrastructure.Persistence.Mongo.Buzzes;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.IntegrationTests.Buzzes;

/// <summary>Against a real, ephemeral MongoDB — Sprint 4's explicit "use a real MongoDB instance."</summary>
[Collection(MongoIntegrationTestCollection.Name)]
public sealed class BuzzRepositoryTests(MongoIntegrationTestFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly OccurrenceId SomeOccurrenceId = new(Guid.CreateVersion7());

    private BuzzRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _repository = new BuzzRepository(fixture.Context);
        await new CreateBuzzIndexes(fixture.Context).ApplyAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static Buzz NewBuzz(OccurrenceId? occurrenceId = null, Guid? recipientUserId = null, DateTimeOffset? scheduledAt = null) =>
        Buzz.Generate(new BuzzId(Guid.CreateVersion7()), occurrenceId ?? SomeOccurrenceId, recipientUserId ?? Guid.CreateVersion7(), scheduledAt ?? Now, Now);

    [Fact]
    public async Task AddAsync_PersistsTheBuzzAtVersionZero()
    {
        var buzz = NewBuzz();

        await _repository.AddAsync(buzz, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(buzz.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.Version);
        Assert.Equal(BuzzStatus.Scheduled, reloaded.Status);
        Assert.Equal(0, reloaded.AttemptCount);
        Assert.Equal(buzz.RecipientUserId, reloaded.RecipientUserId);
        Assert.Equal(buzz.ScheduledAt, reloaded.ScheduledAt);
    }

    [Fact]
    public async Task AddAsync_RejectsADuplicateOccurrenceIdAndRecipientUserIdCombination()
    {
        // The unique index is the real enforcement of "no duplicate Buzz generation"
        // (Sprint 4 brief's idempotency key) — verified here at the database level, not
        // just trusted from application logic. Same pattern as
        // OccurrenceRepositoryTests.AddAsync_RejectsADuplicateReminderIdAndDueAtCombination.
        var occurrenceId = new OccurrenceId(Guid.CreateVersion7());
        var recipientUserId = Guid.CreateVersion7();
        await _repository.AddAsync(NewBuzz(occurrenceId, recipientUserId), CancellationToken.None);

        var duplicate = NewBuzz(occurrenceId, recipientUserId);
        await Assert.ThrowsAsync<MongoWriteException>(() => _repository.AddAsync(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_AllowsTheSameOccurrenceForDifferentRecipients()
    {
        var occurrenceId = new OccurrenceId(Guid.CreateVersion7());

        await _repository.AddAsync(NewBuzz(occurrenceId, Guid.CreateVersion7()), CancellationToken.None);
        await _repository.AddAsync(NewBuzz(occurrenceId, Guid.CreateVersion7()), CancellationToken.None);

        var results = await _repository.ListByOccurrenceAsync(occurrenceId, CancellationToken.None);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForAnUnknownId()
    {
        var result = await _repository.GetByIdAsync(new BuzzId(Guid.CreateVersion7()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListByOccurrenceAsync_OnlyReturnsBuzzesForTheGivenOccurrence()
    {
        var occurrenceId = new OccurrenceId(Guid.CreateVersion7());
        var otherOccurrenceId = new OccurrenceId(Guid.CreateVersion7());
        var own = NewBuzz(occurrenceId);
        var other = NewBuzz(otherOccurrenceId);
        await _repository.AddAsync(own, CancellationToken.None);
        await _repository.AddAsync(other, CancellationToken.None);

        var results = await _repository.ListByOccurrenceAsync(occurrenceId, CancellationToken.None);

        var found = Assert.Single(results);
        Assert.Equal(own.Id, found.Id);
    }

    [Fact]
    public async Task ListPendingByRecipientAsync_OnlyReturnsTheGivenRecipientsScheduledBuzzes()
    {
        var recipientUserId = Guid.CreateVersion7();
        var ownBuzz = NewBuzz(recipientUserId: recipientUserId);
        var otherBuzz = NewBuzz(recipientUserId: Guid.CreateVersion7());
        await _repository.AddAsync(ownBuzz, CancellationToken.None);
        await _repository.AddAsync(otherBuzz, CancellationToken.None);

        var results = await _repository.ListPendingByRecipientAsync(recipientUserId, afterId: null, limit: 20, CancellationToken.None);

        var found = Assert.Single(results);
        Assert.Equal(ownBuzz.Id, found.Id);
    }

    [Fact]
    public async Task ListPendingByRecipientAsync_RespectsTheCursorAndLimit()
    {
        var recipientUserId = Guid.CreateVersion7();
        for (var i = 0; i < 3; i++)
            await _repository.AddAsync(NewBuzz(new OccurrenceId(Guid.CreateVersion7()), recipientUserId), CancellationToken.None);

        var firstPage = await _repository.ListPendingByRecipientAsync(recipientUserId, afterId: null, limit: 2, CancellationToken.None);
        Assert.Equal(2, firstPage.Count);

        var secondPage = await _repository.ListPendingByRecipientAsync(recipientUserId, afterId: firstPage[^1].Id.Value, limit: 2, CancellationToken.None);
        Assert.Single(secondPage);
        Assert.DoesNotContain(secondPage, buzz => firstPage.Select(item => item.Id).Contains(buzz.Id));
    }

    // These claim tests share this class's fixed `Now` with every other test here, and
    // this whole test project shares one MongoDB database per collection
    // (MongoIntegrationTestFixture) — so ClaimPendingAsync's deliberately global query
    // (real work-queue semantics, not scoped to one Buzz) can also sweep up unrelated
    // due Buzzes other tests inserted. Every assertion below therefore uses a generous
    // batchSize and checks specifically for *this test's own* Buzz by ID, never an exact
    // total count of the whole claimed list (ClaimPendingAsync_RespectsBatchSize is the
    // one exception — its own assertion is already pollution-proof by construction: the
    // return is never more than batchSize regardless of how much else is eligible).

    [Fact]
    public async Task ClaimPendingAsync_ClaimsADueBuzzAndTransitionsItToGenerated()
    {
        var buzz = NewBuzz(scheduledAt: Now);
        await _repository.AddAsync(buzz, CancellationToken.None);

        var claimed = await _repository.ClaimPendingAsync(Now, batchSize: 1000, CancellationToken.None);

        var found = Assert.Single(claimed, b => b.Id == buzz.Id);
        Assert.Equal(BuzzStatus.Generated, found.Status);
        Assert.Equal(1, found.AttemptCount);
        Assert.Equal(1, found.Version);
    }

    [Fact]
    public async Task ClaimPendingAsync_DoesNotClaimABuzzScheduledInTheFuture()
    {
        var buzz = NewBuzz(scheduledAt: Now.AddHours(1));
        await _repository.AddAsync(buzz, CancellationToken.None);

        var claimed = await _repository.ClaimPendingAsync(Now, batchSize: 1000, CancellationToken.None);

        Assert.DoesNotContain(claimed, b => b.Id == buzz.Id);
    }

    [Fact]
    public async Task ClaimPendingAsync_CalledAgain_DoesNotReclaimAnAlreadyClaimedBuzz()
    {
        var buzz = NewBuzz(scheduledAt: Now);
        await _repository.AddAsync(buzz, CancellationToken.None);
        await _repository.ClaimPendingAsync(Now, batchSize: 1000, CancellationToken.None);

        var second = await _repository.ClaimPendingAsync(Now, batchSize: 1000, CancellationToken.None);

        Assert.DoesNotContain(second, b => b.Id == buzz.Id);
    }

    [Fact]
    public async Task ClaimPendingAsync_RespectsBatchSize()
    {
        for (var i = 0; i < 3; i++)
            await _repository.AddAsync(NewBuzz(new OccurrenceId(Guid.CreateVersion7()), scheduledAt: Now), CancellationToken.None);

        var claimed = await _repository.ClaimPendingAsync(Now, batchSize: 2, CancellationToken.None);

        Assert.Equal(2, claimed.Count);
    }

    [Fact]
    public async Task ClaimPendingAsync_ConcurrentCallsNeverClaimTheSameBuzzTwice()
    {
        // "Claim work safely" / duplicate-processing prevention, verified against real
        // MongoDB: five due Buzzes, two "workers" racing to claim a shared pool
        // concurrently — every one of *these five* must appear exactly once across the
        // two results combined (never zero, never twice), because each underlying claim
        // is one atomic find-and-modify (IBuzzRepository.ClaimPendingAsync's own doc
        // comment). A generous batchSize means each call could also pick up unrelated
        // leftover Buzzes from other tests — that's fine and expected; only these five
        // IDs are asserted on.
        var myBuzzIds = new List<BuzzId>();
        for (var i = 0; i < 5; i++)
        {
            var buzz = NewBuzz(new OccurrenceId(Guid.CreateVersion7()), scheduledAt: Now);
            await _repository.AddAsync(buzz, CancellationToken.None);
            myBuzzIds.Add(buzz.Id);
        }

        var claimTask1 = _repository.ClaimPendingAsync(Now, batchSize: 1000, CancellationToken.None);
        var claimTask2 = _repository.ClaimPendingAsync(Now, batchSize: 1000, CancellationToken.None);
        var results = await Task.WhenAll(claimTask1, claimTask2);

        var allClaimedIds = results.SelectMany(claimed => claimed.Select(buzz => buzz.Id)).ToList();
        foreach (var myBuzzId in myBuzzIds)
            Assert.Single(allClaimedIds, id => id == myBuzzId);
    }

    [Fact]
    public async Task UpdateAsync_PersistsTheNewStatusAndIncrementsVersion()
    {
        var buzz = NewBuzz(scheduledAt: Now);
        await _repository.AddAsync(buzz, CancellationToken.None);
        var claimedBatch = await _repository.ClaimPendingAsync(Now, batchSize: 1000, CancellationToken.None);
        var claimed = claimedBatch.Single(b => b.Id == buzz.Id);
        claimed.MarkDelivered(Now);

        await _repository.UpdateAsync(claimed, CancellationToken.None);
        var reloaded = await _repository.GetByIdAsync(claimed.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(BuzzStatus.Delivered, reloaded.Status);
        Assert.Equal(2, reloaded.Version);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsConcurrencyConflictExceptionWhenTheVersionIsStale()
    {
        // Sprint 6's first real exercise of AggregateRoot.Version as an enforced
        // optimistic-concurrency gate (SPRINT_5_REPORT.md §4.3/§5.1) — simulates two
        // in-memory copies of the same claimed Buzz, one of which persists its outcome
        // first, making the second's Version stale.
        var buzz = NewBuzz(scheduledAt: Now);
        await _repository.AddAsync(buzz, CancellationToken.None);
        var claimedBatch = await _repository.ClaimPendingAsync(Now, batchSize: 1000, CancellationToken.None);
        var claimed = claimedBatch.Single(b => b.Id == buzz.Id);
        var staleCopy = await _repository.GetByIdAsync(claimed.Id, CancellationToken.None);
        Assert.NotNull(staleCopy);

        claimed.MarkDelivered(Now);
        await _repository.UpdateAsync(claimed, CancellationToken.None);

        staleCopy.MarkFailed(Now);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _repository.UpdateAsync(staleCopy, CancellationToken.None));
    }
}
