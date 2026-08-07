using BuzzMe.Domain.SeedWork;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Outbox;

/// <summary>
/// Writes an aggregate's raised domain events to the outbox. A future repository
/// implementation calls this inside the *same* Mongo session it uses to save the
/// aggregate's own document, so both writes commit together or not at all
/// (DEVELOPMENT_GUIDE.md §7/§9's "exactly one place a multi-document transaction is used").
/// Internal to Infrastructure — Application code never references this directly (see
/// MongoContext's remarks); it will be called from each repository's own SaveAsync.
/// </summary>
public interface IOutboxWriter
{
    Task WriteAsync(IClientSessionHandle session, IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}
