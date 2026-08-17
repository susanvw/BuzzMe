namespace BuzzMe.Application.Abstractions;

/// <summary>
/// Declared in Application (not Domain) so User's own constructor/factories never see a
/// plaintext password or need to know a hashing algorithm exists — Register and
/// ResetPassword both hash before calling into the aggregate. Implemented in Infrastructure.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
