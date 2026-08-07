namespace BuzzMe.Api.Configuration;

/// <summary>Bound from the "Jwt" configuration section (API_CONTRACT.md §2 — Bearer access/refresh tokens).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SigningKey { get; init; }

    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    public int RefreshTokenLifetimeDays { get; init; } = 30;
}
