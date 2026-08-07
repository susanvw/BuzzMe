namespace BuzzMe.Domain.SeedWork;

/// <summary>
/// Base type for an aggregate root — the only kind of Domain type an Infrastructure
/// repository ever loads or saves directly (Implementation Spec §1). Collects the domain
/// events its own behavior raises; the application/infrastructure layer is responsible for
/// reading <see cref="DomainEvents"/> after a successful save and clearing them (never for
/// deciding when an event is raised — that decision belongs entirely to the aggregate).
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Optimistic concurrency token — every persisted update targets this exact value and
    /// increments it. A mismatch at the storage layer is the concurrency conflict described
    /// throughout the Implementation and Application Layer Specifications (e.g. Occurrence
    /// resolution's "already done by X" outcome).
    /// </summary>
    public long Version { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
