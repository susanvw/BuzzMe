namespace BuzzMe.Application.Abstractions;

/// <summary>Phone verification, SMS-channel Invitations and Buzzes — see Event Storming §M.</summary>
public interface ISmsSender
{
    Task<bool> SendAsync(string toPhoneNumber, string body, CancellationToken cancellationToken);
}
