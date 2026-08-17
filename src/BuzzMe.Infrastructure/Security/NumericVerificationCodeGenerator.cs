using System.Security.Cryptography;
using BuzzMe.Domain.Users;

namespace BuzzMe.Infrastructure.Security;

/// <summary>
/// A 6-digit, zero-padded numeric code — the conventional shape for a code delivered over
/// email/SMS and typed back by hand (no document specifies a length or alphabet).
/// </summary>
public sealed class NumericVerificationCodeGenerator : IVerificationCodeGenerator
{
    public string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
