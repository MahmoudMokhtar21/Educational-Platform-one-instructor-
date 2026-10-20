namespace Educatinal_Platform.DTOs;

/// <summary>
/// Optional contact data supplied only to create a real hosted checkout.
/// Course, price, currency, and student identity remain server-owned.
/// </summary>
public sealed class EnrollmentPaymentRequest
{
    public string? PhoneNumber { get; init; }
    // This is accepted only at checkout creation and validated against server-owned course pricing.
    public string? AccessType { get; init; }
}

public sealed class PaymentCompletionResult
{
    public bool Success { get; init; }
    public string? EnrollmentId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string AccessType { get; init; } = string.Empty;
    public bool IsMock { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Code { get; init; }
}

/// <summary>
/// Read-only payment status returned to the authenticated student so the
/// frontend can discover enrollment after returning from a hosted checkout.
/// </summary>
public sealed class PaymentStatusResult
{
    public string PaymentReference { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? EnrollmentId { get; init; }
    public string? AccessType { get; init; }
    public string Message { get; init; } = string.Empty;
}
