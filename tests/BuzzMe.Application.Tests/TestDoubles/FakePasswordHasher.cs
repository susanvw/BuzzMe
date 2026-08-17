using BuzzMe.Application.Abstractions;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>A deterministic, non-cryptographic stand-in — real hashing is exercised by Pbkdf2PasswordHasher's own use (no dedicated Infrastructure test exists for it, same as every other pure-computation Infrastructure class in this codebase).</summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";

    public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
}
