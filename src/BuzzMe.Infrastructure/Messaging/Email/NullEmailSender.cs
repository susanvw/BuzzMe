using BuzzMe.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BuzzMe.Infrastructure.Messaging.Email;

/// <summary>Logs instead of delivering — see NullPushNotificationSender for the reasoning.</summary>
public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task<bool> SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "No email provider configured — would have sent {Subject} to {ToAddress}", subject, toAddress);
        return Task.FromResult(false);
    }
}
