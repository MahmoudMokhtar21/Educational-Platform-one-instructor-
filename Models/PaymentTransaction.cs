using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Educatinal_Platform.Models;

/// <summary>
/// Server-owned payment attempt. This creates a durable correlation between a
/// checkout request and a provider callback without putting entitlement state in
/// the browser.
/// </summary>
public sealed class PaymentTransaction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Provider { get; set; } = string.Empty;
    public bool IsMock { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    // Saved before checkout so callbacks never trust a browser-supplied plan.
    public string? AccessType { get; set; }
    public decimal Amount { get; set; }
    public long AmountCents { get; set; }
    public string Currency { get; set; } = string.Empty;

    // pending, succeeded, failed, cancelled
    public string Status { get; set; } = "pending";

    // Provider-generated values are saved only after checkout creation succeeds.
    public string? ProviderOrderId { get; set; }
    public string? ProviderIntentionId { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderIntegrationId { get; set; }
    public string? CheckoutUrl { get; set; }
    // Present only while this is the one active checkout for the student/course.
    public string? ActiveCheckoutKey { get; set; }
    public string? FailureCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // A compare-and-set claim prevents duplicate provider callbacks from
    // creating two enrollments for the same payment attempt.
    public string? FulfillmentClaimToken { get; set; }
    public DateTime? FulfillmentClaimedAt { get; set; }
    public string? EnrollmentId { get; set; }
}
