namespace BuzzMe.Application.Abstractions;

/// <summary>
/// Transactional email only (verification codes, recovery tokens, Invitation delivery) —
/// see Event Storming §M. BuzzMe sends no marketing or digest email.
/// </summary>
public interface IEmailSender
{
    Task<bool> SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken);
}
