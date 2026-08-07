using System.Security.Claims;
using BuzzMe.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace BuzzMe.Api.Identity;

/// <summary>
/// Reads the authenticated User from the current request's claims. Lives in BuzzMe.Api,
/// not BuzzMe.Infrastructure, because it's inherently tied to HttpContext
/// (DEVELOPMENT_GUIDE.md §3's Infrastructure folder note).
/// </summary>
public sealed class HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new InvalidOperationException("No authenticated user on the current request.");
}
