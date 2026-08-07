using BuzzMe.Application.Abstractions;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>A settable ICurrentUserContext — lets an Application Service test act as a specific, or no, authenticated User.</summary>
public sealed class FakeCurrentUserContext : ICurrentUserContext
{
    public bool IsAuthenticated { get; set; } = true;

    public Guid UserId { get; set; } = Guid.CreateVersion7();
}
