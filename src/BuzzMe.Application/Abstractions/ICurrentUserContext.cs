namespace BuzzMe.Application.Abstractions;

/// <summary>
/// The authenticated caller of the current use case. Declared here because Application
/// needs it; implemented in BuzzMe.Api against the current HttpContext, since that's
/// inherently a web-request concern, not a general Infrastructure one
/// (DEVELOPMENT_GUIDE.md §3, Infrastructure folder note).
/// </summary>
public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }
}
