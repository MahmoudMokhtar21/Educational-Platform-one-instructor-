using Educatinal_Platform.Models;

namespace Educatinal_Platform.Services;

public sealed record IssuedAccessToken(string Token, DateTime ExpiresAt);

public interface IAccessTokenService
{
    IssuedAccessToken Create(User user);
}
