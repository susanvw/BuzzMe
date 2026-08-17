using BuzzMe.Application.Abstractions;

namespace BuzzMe.Application.Tests.TestDoubles;

public sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public string Issue(Guid userId, DateTimeOffset now) => $"access-token:{userId}:{now:O}";
}
