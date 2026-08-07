using BuzzMe.Application.Abstractions;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>
/// Spy implementations of the three external-messaging abstractions — record every call
/// instead of delivering, so a test can assert "an invitation email was sent to X" without
/// touching a real provider (DEVELOPMENT_GUIDE.md §8 — external side effects are always
/// asserted separately from domain state changes).
/// </summary>
public sealed class RecordingPushNotificationSender : IPushNotificationSender
{
    public List<(string PushToken, string Title, string Body)> SentMessages { get; } = [];

    public Task<bool> SendAsync(string pushToken, string title, string body, CancellationToken cancellationToken)
    {
        SentMessages.Add((pushToken, title, body));
        return Task.FromResult(true);
    }
}

public sealed class RecordingEmailSender : IEmailSender
{
    public List<(string ToAddress, string Subject, string Body)> SentMessages { get; } = [];

    public Task<bool> SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        SentMessages.Add((toAddress, subject, body));
        return Task.FromResult(true);
    }
}

public sealed class RecordingSmsSender : ISmsSender
{
    public List<(string ToPhoneNumber, string Body)> SentMessages { get; } = [];

    public Task<bool> SendAsync(string toPhoneNumber, string body, CancellationToken cancellationToken)
    {
        SentMessages.Add((toPhoneNumber, body));
        return Task.FromResult(true);
    }
}
