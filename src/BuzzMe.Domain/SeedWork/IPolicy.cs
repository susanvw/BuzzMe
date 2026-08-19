namespace BuzzMe.Domain.SeedWork;

/// <summary>
/// APPLICATION_LAYER_SPEC.md §7's Policies — the reactive glue between a domain event and
/// its eventually-consistent side effect (DEVELOPMENT_GUIDE.md §2's Application component
/// table: "Policies (the reactive glue from Application Layer Spec §7)"). Implemented in
/// Application, invoked only by the outbox dispatcher (DEVELOPMENT_GUIDE.md §7) — never
/// called directly by the UI/Api. Declared here, alongside <see cref="IDomainEvent"/>,
/// since both Application (implements) and Infrastructure (resolves/invokes, reflectively,
/// by the runtime event type) need to reference it without Infrastructure depending on
/// Application's own Policies namespace.
/// </summary>
public interface IPolicy<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
