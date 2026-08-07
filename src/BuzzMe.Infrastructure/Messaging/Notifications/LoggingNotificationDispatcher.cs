using BuzzMe.Application.Abstractions;
using BuzzMe.Domain.Buzzes;
using Microsoft.Extensions.Logging;

namespace BuzzMe.Infrastructure.Messaging.Notifications;

/// <summary>
/// The default, temporary <see cref="INotificationDispatcher"/> — records (logs) that a
/// Buzz was processed and always reports success. Deliberately the opposite default from
/// NullPushNotificationSender/NullEmailSender/NullSmsSender, which return `false` because
/// silently claiming a real send succeeded would be dishonest. This type isn't claiming a
/// real send happened at all — it exists only so BuzzMe.Workers' orchestration (claim →
/// dispatch → mark outcome) can be built and proven correct before any real provider
/// exists, per the Sprint 6 brief's explicit instruction: "records successful processing
/// only." Delete this type once a real dispatch step replaces it (SPRINT_6_REPORT.md §5).
/// </summary>
public sealed class LoggingNotificationDispatcher(ILogger<LoggingNotificationDispatcher> logger) : INotificationDispatcher
{
    public Task<bool> DispatchAsync(Buzz buzz, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "No real notification provider configured — recording Buzz {BuzzId} for recipient {RecipientUserId} as processed",
            buzz.Id, buzz.RecipientUserId);
        return Task.FromResult(true);
    }
}
