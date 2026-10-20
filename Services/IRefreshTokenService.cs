namespace Educatinal_Platform.Services;

public sealed record IssuedRefreshToken(string Token, string CsrfToken, DateTime ExpiresAt);
public sealed record RotatedRefreshToken(string UserId, string Token, string CsrfToken, DateTime ExpiresAt);

public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(string userId, CancellationToken cancellationToken = default);
    Task<RotatedRefreshToken?> RotateAsync(string rawToken, string csrfToken, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string rawToken, string csrfToken, string reason, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(string userId, string reason, CancellationToken cancellationToken = default);
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
}
