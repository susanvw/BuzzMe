using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Application.Tests.TestDoubles;

public sealed class FakeIdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}
