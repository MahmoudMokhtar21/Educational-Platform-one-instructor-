using Microsoft.AspNetCore.Http;

namespace Educatinal_Platform.Options;

/// <summary>
/// Configuration for the opaque refresh-token cookie and its server-side record.
/// The access token has its own fixed policy in <see cref="JwtOptions"/>.
/// </summary>
public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    public int LifetimeDays { get; init; } = 7;
    public string CookieName { get; init; } = "edu_refresh";
    public string CookieSameSite { get; init; } = nameof(SameSiteMode.Lax);
}
