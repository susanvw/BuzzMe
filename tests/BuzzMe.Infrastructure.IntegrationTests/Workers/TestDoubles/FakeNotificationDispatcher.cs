using BuzzMe.Application.Abstractions;
using BuzzMe.Domain.Buzzes;

namespace BuzzMe.Infrastructure.IntegrationTests.Workers.TestDoubles;

/// <summary>
/// The one thing Sprint 6's brief allows to be mocked — everything else in these tests is
/// real (real MongoDB, real Application Services). Records every dispatched Buzz's ID
/// (not just a raw count): BuzzMe.Infrastructure.IntegrationTests shares one MongoDB
/// database across every test class in its collection (MongoIntegrationTestFixture), and
/// ClaimPendingAsync's query is deliberately global (matches real work-queue semantics),
/// so a worker test's own batch can legitimately also sweep up an unrelated leftover due
/// Buzz from another test. Asserting "was my own Buzz ID dispatched exactly once" is
/// robust to that; asserting a raw total call count is not.
/// </summary>
public sealed class FakeNotificationDispatcher(bool succeeds) : INotificationDispatcher
{
    private readonly List<BuzzId> _dispatchedBuzzIds = [];

    public IReadOnlyList<BuzzId> DispatchedBuzzIds => _dispatchedBuzzIds.AsReadOnly();

    public Task<bool> DispatchAsync(Buzz buzz, CancellationToken cancellationToken)
    {
        _dispatchedBuzzIds.Add(buzz.Id);
        return Task.FromResult(succeeds);
    }
}
