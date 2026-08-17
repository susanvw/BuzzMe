using System.Security.Cryptography;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Infrastructure.Security;

/// <summary>A 256-bit, cryptographically random hex string per token — same construction as SecureInvitationTokenGenerator, generalized for RefreshToken and password reset tokens.</summary>
public sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    public string NewToken() => RandomNumberGenerator.GetHexString(64);
}
