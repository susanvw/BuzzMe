using BuzzMe.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BuzzMe.Infrastructure.Messaging.Sms;

/// <summary>Logs instead of delivering — see NullPushNotificationSender for the reasoning.</summary>
public sealed class NullSmsSender(ILogger<NullSmsSender> logger) : ISmsSender
{
    public Task<bool> SendAsync(string toPhoneNumber, string body, CancellationToken cancellationToken)
    {
        logger.LogWarning("No SMS provider configured — would have sent a message to {ToPhoneNumber}", toPhoneNumber);
        return Task.FromResult(false);
    }
}
