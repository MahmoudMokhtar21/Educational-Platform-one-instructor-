namespace Educatinal_Platform.Services;

/// <summary>
/// Provider-neutral checkout boundary. Enrollment remains the owner of course
/// access; payment providers only create a checkout attempt or, in development,
/// simulate a confirmed payment through an explicit server endpoint.
/// </summary>
public interface IPaymentService
{
    string Provider { get; }
    bool IsMock { get; }
    bool RequiresBillingPhone { get; }
    string Currency { get; }

    Task<PaymentCheckoutResult> CreateCheckoutAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentProviderConfirmation> ConfirmDevelopmentPaymentAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PaymentCheckoutRequest
{
    public string PaymentReference { get; init; } = string.Empty;
    public string StudentId { get; init; } = string.Empty;
    public string CourseId { get; init; } = string.Empty;
    // Copied from the server-owned transaction; providers do not decide entitlement.
    public string AccessType { get; init; } = string.Empty;
    public string CourseSlug { get; init; } = string.Empty;
    public string CourseTitle { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public long AmountCents { get; init; }
    public string Currency { get; init; } = string.Empty;
    public PaymentCustomer Customer { get; init; } = new();
}

public sealed class PaymentCustomer
{
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
}

public sealed class PaymentCheckoutResult
{
    public string Provider { get; init; } = string.Empty;
    public bool IsMock { get; init; }
    public string? CheckoutUrl { get; init; }
    public string? ProviderOrderId { get; init; }
    public string? ProviderIntentionId { get; init; }
    public string? ProviderIntegrationId { get; init; }
}

public sealed class PaymentProviderConfirmation
{
    public string Provider { get; init; } = string.Empty;
    public string ProviderTransactionId { get; init; } = string.Empty;
    public string? ProviderOrderId { get; init; }
    public long AmountCents { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool IsSuccessful { get; init; }
    public bool IsFinalFailure { get; init; }
}

/// <summary>
/// A known, safe error that can be returned to API consumers without exposing
/// provider credentials or transport details.
/// </summary>
public sealed class PaymentCheckoutException : InvalidOperationException
{
    public PaymentCheckoutException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
