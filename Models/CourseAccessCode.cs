using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Educatinal_Platform.Models;

/// <summary>
/// A one-time, course-bound entitlement. The redeemable value is never stored;
/// only its SHA-256 digest and a non-sensitive display hint are persisted.
/// </summary>
public sealed class CourseAccessCode
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string CourseId { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public string CodeHint { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public string? RedeemedByStudentId { get; set; }
    public string? RedemptionClaimToken { get; set; }
    public string? RedemptionClaimedByStudentId { get; set; }
    public DateTime? RedemptionClaimedAt { get; set; }
}