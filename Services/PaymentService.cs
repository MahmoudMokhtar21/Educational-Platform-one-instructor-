namespace Educatinal_Platform.Services;
using Stripe;
public interface IPaymentService
{
    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string studentId,
        string courseId);
}

public class PaymentIntentResult
{
    public string PaymentIntentId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}

public class PaymentService : IPaymentService
{
    private readonly IConfiguration _configuration;

    public PaymentService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string studentId,
        string courseId)
    {
        var secretKey =
            _configuration["Stripe:SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException(
                "Stripe Secret Key is not configured.");

        StripeConfiguration.ApiKey = secretKey;

        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(amount * 100),
            Currency = currency,
            PaymentMethodTypes = new List<string>
                {
                    "card"
                },
            Metadata = new Dictionary<string, string>
            {
                { "StudentId", studentId },
                { "CourseId", courseId }
            }
        };

        var service = new PaymentIntentService();

        var paymentIntent =
            await service.CreateAsync(options);

        return new PaymentIntentResult
        {
            PaymentIntentId = paymentIntent.Id,
            ClientSecret = paymentIntent.ClientSecret
        };
    }
}