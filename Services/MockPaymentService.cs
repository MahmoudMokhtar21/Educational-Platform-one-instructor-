namespace Educatinal_Platform.Services;

/// <summary>
/// Development-only payment provider. It never contacts a payment processor and
/// does not create an enrollment until the authenticated student explicitly
/// confirms the generated mock payment through the API.
/// </summary>
public sealed class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;
    private readonly IConfiguration _configuration;

    public MockPaymentService(
        ILogger<MockPaymentService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public string Provider => "Mock";

    public bool IsMock => true;
    public bool RequiresBillingPhone => false;

    public string Currency =>
        (_configuration["Payment:Currency"] ?? _configuration["Kashier:Currency"] ?? _configuration["Paymob:Currency"] ?? "EGP").Trim().ToUpperInvariant();

    public Task<PaymentCheckoutResult> CreateCheckoutAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Created development mock payment {PaymentReference} for course {CourseId}; amountCents={AmountCents}; currency={Currency}",
            request.PaymentReference,
            request.CourseId,
            request.AmountCents,
            request.Currency);

        return Task.FromResult(new PaymentCheckoutResult
        {
            Provider = Provider,
            IsMock = true
        });
    }

    public Task<PaymentProviderConfirmation> ConfirmDevelopmentPaymentAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Explicitly confirming development mock payment {PaymentReference} for course {CourseId}",
            request.PaymentReference,
            request.CourseId);

        return Task.FromResult(new PaymentProviderConfirmation
        {
            Provider = Provider,
            ProviderTransactionId = $"mock_{request.PaymentReference}",
            ProviderOrderId = request.PaymentReference,
            AmountCents = request.AmountCents,
            Currency = request.Currency,
            IsSuccessful = true,
            IsFinalFailure = false
        });
    }
}
