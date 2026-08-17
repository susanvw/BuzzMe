namespace BuzzMe.Application.Abstractions;

/// <summary>
/// Mints the short-lived Bearer access token API_CONTRACT.md §2 already assumes a client
/// holds (HttpCurrentUserContext validates one on every authenticated request; nothing
/// issued one until Sprint 9). Declared in Application so AuthApplicationService can depend
/// on it without depending on a JWT library directly — implemented in Infrastructure.
/// </summary>
public interface IAccessTokenIssuer
{
    string Issue(Guid userId, DateTimeOffset now);
}
