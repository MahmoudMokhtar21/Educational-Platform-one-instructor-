namespace Educatinal_Platform.Options;

/// <summary>
/// Settings used exclusively for issuing application access tokens. Refresh-token
/// lifetime is intentionally configured separately in <see cref="RefreshTokenOptions"/>.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "JWT";
    public const int AccessTokenLifetimeMinutes = 360;

    public string SecretKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; } = AccessTokenLifetimeMinutes;
}
