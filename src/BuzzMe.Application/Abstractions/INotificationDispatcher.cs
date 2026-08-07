using BuzzMe.Domain.Buzzes;

namespace BuzzMe.Application.Abstractions;

/// <summary>
/// A temporary stand-in for real delivery (APNs/FCM/SignalR/email/SMS — none of which
/// exist yet), so <see cref="BuzzMe.Workers"/>' delivery pipeline can be built and tested
/// end to end before any real provider does. Deliberately not <see cref="IPushNotificationSender"/>/
/// <see cref="IEmailSender"/>/<see cref="ISmsSender"/> — those model one channel each and
/// are meant to survive into the real implementation; this interface exists to be deleted
/// once a real dispatch step (fanning out to the recipient's actual channels) replaces it
/// — see SPRINT_6_REPORT.md §5 for the replacement plan.
/// </summary>
public interface INotificationDispatcher
{
    Task<bool> DispatchAsync(Buzz buzz, CancellationToken cancellationToken);
}
