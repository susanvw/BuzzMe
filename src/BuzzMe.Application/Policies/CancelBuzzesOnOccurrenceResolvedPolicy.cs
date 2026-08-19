using BuzzMe.Application.Buzzes;
using BuzzMe.Domain.Occurrences.Events;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Application.Policies;

/// <summary>
/// APPLICATION_LAYER_SPEC.md §7's "CompleteReminder/DismissReminder → cancel the
/// Occurrence's own pending Buzz" — invoked by the outbox dispatcher off
/// <see cref="OccurrenceCompleted"/>/<see cref="OccurrenceDismissed"/>, never called
/// directly. One class handling both events: the side effect (cancel this Occurrence's
/// still-pending Buzzes) is identical regardless of which of the two resolved it.
/// </summary>
public sealed class CancelBuzzesOnOccurrenceResolvedPolicy(BuzzApplicationService buzzService)
    : IPolicy<OccurrenceCompleted>, IPolicy<OccurrenceDismissed>
{
    public Task HandleAsync(OccurrenceCompleted domainEvent, CancellationToken cancellationToken) =>
        buzzService.CancelBuzzesForOccurrenceAsync(domainEvent.OccurrenceId.Value, domainEvent.OccurredAt, cancellationToken);

    public Task HandleAsync(OccurrenceDismissed domainEvent, CancellationToken cancellationToken) =>
        buzzService.CancelBuzzesForOccurrenceAsync(domainEvent.OccurrenceId.Value, domainEvent.OccurredAt, cancellationToken);
}
