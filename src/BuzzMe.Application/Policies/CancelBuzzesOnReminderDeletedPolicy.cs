using BuzzMe.Application.Buzzes;
using BuzzMe.Domain.Reminders.Events;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Application.Policies;

/// <summary>
/// APPLICATION_LAYER_SPEC.md §7's "DeleteReminder → cancel pending Buzzes" — invoked by the
/// outbox dispatcher off <see cref="ReminderDeleted"/>, never called directly.
/// </summary>
public sealed class CancelBuzzesOnReminderDeletedPolicy(BuzzApplicationService buzzService) : IPolicy<ReminderDeleted>
{
    public Task HandleAsync(ReminderDeleted domainEvent, CancellationToken cancellationToken) =>
        buzzService.CancelBuzzesForReminderAsync(domainEvent.ReminderId.Value, domainEvent.OccurredAt, cancellationToken);
}
