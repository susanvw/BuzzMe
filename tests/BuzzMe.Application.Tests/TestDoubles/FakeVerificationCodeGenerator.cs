using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>Always returns the same fixed code — deterministic, so a test can verify without capturing what Register generated.</summary>
public sealed class FakeVerificationCodeGenerator : IVerificationCodeGenerator
{
    public const string Code = "123456";

    public string NewCode() => Code;
}
