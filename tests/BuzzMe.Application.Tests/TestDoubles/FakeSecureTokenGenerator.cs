using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Application.Tests.TestDoubles;

public sealed class FakeSecureTokenGenerator : ISecureTokenGenerator
{
    public string NewToken() => Guid.NewGuid().ToString("N");
}
