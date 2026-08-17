namespace BuzzMe.Infrastructure.Security;

/// <summary>
/// Infrastructure's own binding of the "Jwt" configuration section — a second class from
/// Api's own JwtOptions (Api/Configuration/JwtOptions.cs), not a shared one: Infrastructure
/// must never depend on Api (DEVELOPMENT_GUIDE.md §2's dependency direction), and Api
/// already owns the JwtBearer *validation* config. This is the same section, read a second
/// time, for *issuance* — same pattern as MongoOptions being Infrastructure-owned.
/// </summary>
public sealed class JwtIssuerOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SigningKey { get; init; }

    public int AccessTokenLifetimeMinutes { get; init; } = 15;
}
