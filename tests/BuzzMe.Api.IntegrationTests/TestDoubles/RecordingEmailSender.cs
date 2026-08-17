using System.Collections.Concurrent;
using BuzzMe.Application.Abstractions;

namespace BuzzMe.Api.IntegrationTests.TestDoubles;

/// <summary>
/// Records instead of delivering — substituted for the real host's NullEmailSender in
/// AuthEndpointsTests so a test can read the verification code/reset token a real endpoint
/// call actually generated, the same way RecordingEmailSender does for Application-layer
/// tests (BuzzMe.Application.Tests.TestDoubles), just registered into the real DI container
/// instead of constructed by hand.
/// </summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private ConcurrentBag<(string ToAddress, string Subject, string Body)> _sentMessages = [];

    public IReadOnlyCollection<(string ToAddress, string Subject, string Body)> SentMessages => _sentMessages;

    public Task<bool> SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        _sentMessages.Add((toAddress, subject, body));
        return Task.FromResult(true);
    }

    public void Clear() => _sentMessages = [];
}
