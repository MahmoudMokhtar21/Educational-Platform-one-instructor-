using Educatinal_Platform.Models;
using Educatinal_Platform.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace Educatinal_Platform.Services;

/// <summary>
/// Issues opaque, high-entropy refresh tokens and stores only their SHA-256
/// hashes. Rotation uses a compare-and-set update so an old token cannot be
/// exchanged more than once. Reuse of a rotated token revokes its entire family.
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly IMongoCollection<RefreshToken> _tokens;
    private readonly RefreshTokenOptions _options;

    public RefreshTokenService(IMongoDatabase database, IOptions<RefreshTokenOptions> options)
    {
        _tokens = database.GetCollection<RefreshToken>("RefreshTokens");
        _options = options.Value;
    }

    public async Task<IssuedRefreshToken> IssueAsync(string userId, CancellationToken cancellationToken = default)
    {
        var issued = CreateIssuedToken(userId, Guid.NewGuid().ToString("N"));
        await _tokens.InsertOneAsync(issued.Record, cancellationToken: cancellationToken);
        return new IssuedRefreshToken(issued.RawToken, issued.RawCsrfToken, issued.Record.ExpiresAt);
    }

    public async Task<RotatedRefreshToken?> RotateAsync(string rawToken, string csrfToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(csrfToken))
            return null;

        var now = DateTime.UtcNow;
        var tokenHash = Hash(rawToken);
        var existing = await _tokens.Find(token => token.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
            return null;

        // A CSRF mismatch must not rotate or revoke the family; an attacker can
        // otherwise force a logout using only the browser's cookie transport.
        if (!HashesMatch(existing.CsrfTokenHash, Hash(csrfToken)))
            return null;

        if (existing.ExpiresAt <= now)
            return null;

        if (existing.RevokedAt.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(existing.ReplacedByTokenHash))
                await RevokeFamilyAsync(existing.FamilyId, "Refresh token reuse detected.", cancellationToken);
            return null;
        }

        var replacement = CreateIssuedToken(existing.UserId, existing.FamilyId);
        await _tokens.InsertOneAsync(replacement.Record, cancellationToken: cancellationToken);

        var activeFilter = Builders<RefreshToken>.Filter.And(
            Builders<RefreshToken>.Filter.Eq(token => token.Id, existing.Id),
            Builders<RefreshToken>.Filter.Eq(token => token.TokenHash, tokenHash),
            Builders<RefreshToken>.Filter.Eq(token => token.RevokedAt, null),
            Builders<RefreshToken>.Filter.Gt(token => token.ExpiresAt, now));
        var revokeCurrent = Builders<RefreshToken>.Update
            .Set(token => token.RevokedAt, now)
            .Set(token => token.RevocationReason, "Rotated")
            .Set(token => token.ReplacedByTokenHash, replacement.Record.TokenHash);
        var rotation = await _tokens.UpdateOneAsync(activeFilter, revokeCurrent, cancellationToken: cancellationToken);

        if (rotation.ModifiedCount != 1)
        {
            await _tokens.DeleteOneAsync(token => token.Id == replacement.Record.Id, cancellationToken);
            var latest = await _tokens.Find(token => token.Id == existing.Id).FirstOrDefaultAsync(cancellationToken);
            if (latest?.RevokedAt.HasValue == true && !string.IsNullOrWhiteSpace(latest.ReplacedByTokenHash))
                await RevokeFamilyAsync(latest.FamilyId, "Refresh token reuse detected.", cancellationToken);
            return null;
        }

        return new RotatedRefreshToken(existing.UserId, replacement.RawToken, replacement.RawCsrfToken, replacement.Record.ExpiresAt);
    }

    public async Task<bool> RevokeAsync(string rawToken, string csrfToken, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(csrfToken))
            return false;

        var tokenHash = Hash(rawToken);
        var token = await _tokens.Find(candidate => candidate.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);
        if (token is null || !HashesMatch(token.CsrfTokenHash, Hash(csrfToken)))
            return false;

        if (!string.IsNullOrWhiteSpace(token.ReplacedByTokenHash))
        {
            await RevokeFamilyAsync(token.FamilyId, reason, cancellationToken);
            return true;
        }

        var update = Builders<RefreshToken>.Update
            .Set(candidate => candidate.RevokedAt, DateTime.UtcNow)
            .Set(candidate => candidate.RevocationReason, reason);
        var result = await _tokens.UpdateOneAsync(
            candidate => candidate.Id == token.Id && candidate.RevokedAt == null,
            update,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task RevokeAllForUserAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        var update = Builders<RefreshToken>.Update
            .Set(token => token.RevokedAt, DateTime.UtcNow)
            .Set(token => token.RevocationReason, reason);
        await _tokens.UpdateManyAsync(
            token => token.UserId == userId && token.RevokedAt == null,
            update,
            cancellationToken: cancellationToken);
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<RefreshToken>(
                Builders<RefreshToken>.IndexKeys.Ascending(token => token.TokenHash),
                new CreateIndexOptions { Unique = true, Name = "ux_refresh_token_hash" }),
            new CreateIndexModel<RefreshToken>(
                Builders<RefreshToken>.IndexKeys.Ascending(token => token.UserId).Ascending(token => token.ExpiresAt),
                new CreateIndexOptions { Name = "ix_refresh_token_user_expiry" }),
            new CreateIndexModel<RefreshToken>(
                Builders<RefreshToken>.IndexKeys.Ascending(token => token.FamilyId),
                new CreateIndexOptions { Name = "ix_refresh_token_family" }),
            new CreateIndexModel<RefreshToken>(
                Builders<RefreshToken>.IndexKeys.Ascending(token => token.ExpiresAt),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "ttl_refresh_token_expiry" })
        };

        await _tokens.Indexes.CreateManyAsync(indexes, cancellationToken: cancellationToken);
    }

    private (RefreshToken Record, string RawToken, string RawCsrfToken) CreateIssuedToken(string userId, string familyId)
    {
        var rawToken = CreateOpaqueValue();
        var rawCsrfToken = CreateOpaqueValue();
        return (new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            CsrfTokenHash = Hash(rawCsrfToken),
            FamilyId = familyId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.LifetimeDays)
        }, rawToken, rawCsrfToken);
    }

    private async Task RevokeFamilyAsync(string familyId, string reason, CancellationToken cancellationToken)
    {
        var update = Builders<RefreshToken>.Update
            .Set(token => token.RevokedAt, DateTime.UtcNow)
            .Set(token => token.RevocationReason, reason);
        await _tokens.UpdateManyAsync(
            token => token.FamilyId == familyId && token.RevokedAt == null,
            update,
            cancellationToken: cancellationToken);
    }

    private static string CreateOpaqueValue() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool HashesMatch(string storedHash, string calculatedHash)
    {
        if (storedHash.Length != calculatedHash.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedHash),
            Encoding.UTF8.GetBytes(calculatedHash));
    }
}
