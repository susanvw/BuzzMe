using System.Security.Cryptography;
using BuzzMe.Application.Abstractions;

namespace BuzzMe.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256, no external dependency — .NET's built-in Rfc2898DeriveBytes.Pbkdf2
/// is a vetted implementation, and this codebase otherwise keeps its dependency footprint
/// deliberately small (DEVELOPMENT_GUIDE.md's whole package list is MongoDB.Driver,
/// FluentValidation, and the JWT Bearer packages). No document specifies a hashing
/// algorithm or parameters (IMPLEMENTATION_SPEC.md §2 only says "password meets minimum
/// policy") — 210,000 iterations, a 128-bit salt, and a 256-bit derived key match OWASP's
/// 2023 PBKDF2-HMAC-SHA256 recommendation, an implementation choice, not a derived
/// requirement, same category as InvitationApplicationService's token TTL.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
            return false;

        var salt = Convert.FromBase64String(parts[1]);
        var expectedKey = Convert.FromBase64String(parts[2]);
        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
