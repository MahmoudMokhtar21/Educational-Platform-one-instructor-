using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Educatinal_Platform.Services;

/// <summary>
/// Creates Paymob Intention API checkouts. This service never grants access:
/// only the HMAC-verified server callback can do that.
/// </summary>
public sealed class PaymobPaymentService : IPaymentService
{
    private const string ProviderName = "Paymob";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymobPaymentService> _logger;

    public PaymobPaymentService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PaymobPaymentService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public string Provider => ProviderName;
    public bool IsMock => false;
    public bool RequiresBillingPhone => true;
    public string Currency => ReadCurrency();

    public async Task<PaymentCheckoutResult> CreateCheckoutAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Customer.PhoneNumber))
        {
            throw new PaymentCheckoutException(
                "payment_phone_required",
                "A billing phone number is required before starting a Paymob payment.");
        }

        if (request.AmountCents <= 0)
        {
            throw new PaymentCheckoutException(
                "payment_amount_invalid",
                "The payment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.CourseSlug))
        {
            throw new PaymentCheckoutException(
                "payment_course_invalid",
                "The course cannot be prepared for checkout.");
        }

        var settings = ReadSettings();
        if (!string.Equals(request.Currency, settings.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentCheckoutException(
                "payment_currency_mismatch",
                "The requested payment currency does not match the configured Paymob currency.");
        }

        var notificationUrl = $"{settings.PublicApiBaseUrl}/api/payments/paymob/webhook";
        var returnUrl = BuildReturnUrl(settings.FrontendBaseUrl, request.CourseSlug);

        // Paymob documents billing phone number as required. The application does
        // not invent customer identity/address data; optional address fields are
        // intentionally blank until the product collects them.
        var payload = new
        {
            amount = request.AmountCents,
            currency = settings.Currency,
            payment_methods = new[] { settings.IntegrationId },
            items = new[]
            {
                new
                {
                    name = request.CourseTitle,
                    amount = request.AmountCents,
                    description = request.CourseTitle,
                    quantity = 1
                }
            },
            billing_data = new
            {
                apartment = string.Empty,
                floor = string.Empty,
                street = string.Empty,
                building = string.Empty,
                shipping_method = string.Empty,
                city = string.Empty,
                country = string.Empty,
                state = string.Empty,
                postal_code = string.Empty,
                first_name = request.Customer.FirstName,
                last_name = request.Customer.LastName,
                email = request.Customer.Email,
                phone_number = request.Customer.PhoneNumber.Trim()
            },
            special_reference = request.PaymentReference,
            expiration = 3600,
            notification_url = notificationUrl,
            redirection_url = returnUrl
        };

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.ApiBaseUrl}/v1/intention/");

        // Despite the legacy configuration key name, this value must be the
        // Paymob *Secret Key* from the dashboard. It never reaches the browser.
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Token",
            settings.SecretKey);
        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not reach Paymob while creating checkout for course {CourseId}",
                request.CourseId);
            throw new PaymentCheckoutException(
                "payment_provider_unavailable",
                "The payment provider is temporarily unavailable. Please try again later.");
        }

        using (response)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Paymob intention creation failed with status {StatusCode} for course {CourseId}",
                    (int)response.StatusCode,
                    request.CourseId);
                throw new PaymentCheckoutException(
                    "payment_provider_error",
                    "The payment provider could not create a checkout. Please verify the Paymob configuration and try again.");
            }

            try
            {
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;
                var clientSecret = GetRequiredString(root, "client_secret");
                var orderId = GetRequiredString(root, "intention_order_id");
                var intentionId = GetRequiredString(root, "id");

                // Unified Checkout is a hosted Paymob page. The checkout host is
                // configurable because it varies by Paymob region; it is not the
                // same setting as the Intention API base URL.
                var checkoutUrl =
                    $"{settings.CheckoutBaseUrl}/?publicKey={Uri.EscapeDataString(settings.PublicKey)}&clientSecret={Uri.EscapeDataString(clientSecret)}";

                _logger.LogInformation(
                    "Created Paymob intention {IntentionId} and order {OrderId} for course {CourseId}; amountCents={AmountCents}; currency={Currency}",
                    intentionId,
                    orderId,
                    request.CourseId,
                    request.AmountCents,
                    settings.Currency);

                return new PaymentCheckoutResult
                {
                    Provider = Provider,
                    IsMock = false,
                    CheckoutUrl = checkoutUrl,
                    ProviderOrderId = orderId,
                    ProviderIntentionId = intentionId,
                    ProviderIntegrationId = settings.IntegrationId.ToString(CultureInfo.InvariantCulture)
                };
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Paymob returned an invalid intention response for course {CourseId}", request.CourseId);
                throw new PaymentCheckoutException(
                    "payment_provider_error",
                    "The payment provider returned an invalid checkout response.");
            }
            catch (InvalidOperationException exception) when (exception is not PaymentCheckoutException)
            {
                _logger.LogWarning(exception, "Paymob intention response was missing required fields for course {CourseId}", request.CourseId);
                throw new PaymentCheckoutException(
                    "payment_provider_error",
                    "The payment provider returned an incomplete checkout response.");
            }
        }
    }

    public Task<PaymentProviderConfirmation> ConfirmDevelopmentPaymentAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Paymob payments are confirmed only by verified Paymob callbacks.");

    private PaymobSettings ReadSettings()
    {
        var secretKey = ReadRequired("Paymob:ApiKey");
        var publicKey = ReadRequired("Paymob:PublicKey");
        var hmacSecret = ReadRequired("Paymob:HmacSecret");
        var integrationIdText = ReadRequired("Paymob:IntegrationId");
        var apiBaseUrl = ReadAbsoluteHttpsUrl("Paymob:BaseUrl");
        var checkoutBaseUrl = ReadAbsoluteHttpsUrl(
            "Paymob:CheckoutBaseUrl",
            "https://eg.checkout.paymob.com");
        var publicApiBaseUrl = ReadAbsoluteHttpsUrl("Payment:PublicApiBaseUrl");
        var frontendBaseUrl = ReadAbsoluteHttpsUrl("Frontend:BaseUrl");

        if (!long.TryParse(integrationIdText, out var integrationId) || integrationId <= 0)
        {
            throw new PaymentCheckoutException(
                "payment_provider_configuration_error",
                "Paymob:IntegrationId must be a positive numeric integration ID.");
        }

        return new PaymobSettings(
            secretKey,
            publicKey,
            hmacSecret,
            integrationId,
            apiBaseUrl,
            checkoutBaseUrl,
            publicApiBaseUrl,
            frontendBaseUrl,
            ReadCurrency());
    }

    private string ReadCurrency()
    {
        var currency = (_configuration["Paymob:Currency"] ?? "EGP").Trim();
        if (currency.Length != 3 || !currency.All(char.IsLetter))
        {
            throw new PaymentCheckoutException(
                "payment_provider_configuration_error",
                "Paymob:Currency must be a three-letter ISO currency code.");
        }

        return currency.ToUpperInvariant();
    }

    private string ReadRequired(string key)
    {
        var value = _configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PaymentCheckoutException(
                "payment_provider_configuration_error",
                $"{key} must be configured before enabling the Paymob provider.");
        }

        return value;
    }

    private string ReadAbsoluteHttpsUrl(string key, string? defaultValue = null)
    {
        var value = _configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value)) value = defaultValue;

        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentCheckoutException(
                "payment_provider_configuration_error",
                $"{key} must be an absolute HTTPS URL before enabling the Paymob provider.");
        }

        return value.TrimEnd('/');
    }

    private static string BuildReturnUrl(string frontendBaseUrl, string courseSlug)
    {
        var root = new Uri($"{frontendBaseUrl.TrimEnd('/')}/", UriKind.Absolute);
        var relative = $"courses/{Uri.EscapeDataString(courseSlug)}?payment=return";
        return new Uri(root, relative).ToString();
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"Missing Paymob response property '{propertyName}'.");
        }

        var value = property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.GetRawText();

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Empty Paymob response property '{propertyName}'.")
            : value;
    }

    private sealed record PaymobSettings(
        string SecretKey,
        string PublicKey,
        string HmacSecret,
        long IntegrationId,
        string ApiBaseUrl,
        string CheckoutBaseUrl,
        string PublicApiBaseUrl,
        string FrontendBaseUrl,
        string Currency);
}
