using BuzzMe.Domain.Occurrences.Events;
using BuzzMe.Domain.Reminders.Events;

namespace BuzzMe.Infrastructure.Persistence.Outbox;

/// <summary>
/// Maps an outbox row's stored <c>EventType</c> name back to the concrete <c>IDomainEvent</c>
/// CLR type, so its JSON payload can be deserialized to the right shape before being handed
/// to a Policy. Deliberately explicit rather than reflection-scanning every IDomainEvent in
/// the Domain assembly (this codebase's established preference for an explicit mapping
/// table over relying on a convention — see RecurrenceCodes/NotifyPresetCodes' own doc
/// comments) — and deliberately lists only the event types a write path in this codebase
/// actually puts in the outbox today (OccurrenceRepository.UpdateAsync,
/// ReminderRepository.MarkDeletedAsync), not every IDomainEvent that exists.
/// </summary>
internal static class OutboxEventTypeRegistry
{
    private static readonly Dictionary<string, Type> TypesByName = new()
    {
        [nameof(OccurrenceCompleted)] = typeof(OccurrenceCompleted),
        [nameof(OccurrenceDismissed)] = typeof(OccurrenceDismissed),
        [nameof(OccurrenceUndone)] = typeof(OccurrenceUndone),
        [nameof(ReminderDeleted)] = typeof(ReminderDeleted),
    };

    /// <summary>False for an event type nothing in this codebase currently writes to the outbox, or one with no registered Policy — the dispatcher treats that as "nothing to do," not a fault.</summary>
    public static bool TryResolve(string eventType, out Type type) => TypesByName.TryGetValue(eventType, out type!);
}
