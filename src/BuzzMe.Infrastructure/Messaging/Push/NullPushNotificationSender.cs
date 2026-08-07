using BuzzMe.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BuzzMe.Infrastructure.Messaging.Push;

/// <summary>
/// Logs instead of delivering. The default registration until a real APNs/FCM adapter is
/// implemented (DEVELOPMENT_GUIDE.md §7) — deliberately never silently a no-op, so a
/// misconfigured environment is visible in the logs rather than just producing silence.
/// </summary>
public sealed class NullPushNotificationSender(ILogger<NullPushNotificationSender> logger) : IPushNotificationSender
{
    public Task<bool> SendAsync(string pushToken, string title, string body, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "No push provider configured — would have sent {Title} to device {PushToken}", title, pushToken);
        return Task.FromResult(false);
    }
}
