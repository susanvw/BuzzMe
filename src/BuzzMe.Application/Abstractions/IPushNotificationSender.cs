namespace BuzzMe.Application.Abstractions;

/// <summary>
/// A single Buzz delivery attempt to one device (Application Layer Spec §8's external
/// side effects; Development Guide's Messaging/Push implementations). Never awaited inside
/// the same transaction as a domain state change — dispatched after commit, from the
/// Workers outbox dispatcher.
/// </summary>
public interface IPushNotificationSender
{
    Task<bool> SendAsync(string pushToken, string title, string body, CancellationToken cancellationToken);
}
