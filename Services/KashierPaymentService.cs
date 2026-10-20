using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Educatinal_Platform.Services;

/// <summary>Creates Kashier hosted payment sessions without granting course access.</summary>
public sealed class KashierPaymentService : IPaymentService
{
    private const string ProviderName = "Kashier";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KashierPaymentService> _logger;

    public KashierPaymentService(HttpClient httpClient, IConfiguration configuration, ILogger<KashierPaymentService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public string Provider => ProviderName;
    public bool IsMock => false;
    public bool RequiresBillingPhone => false;
    public string Currency => ReadCurrency();

    public async Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0 || request.AmountCents <= 0)
            throw new PaymentCheckoutException("payment_amount_invalid", "The payment amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.PaymentReference) || string.IsNullOrWhiteSpace(request.CourseSlug))
            throw new PaymentCheckoutException("payment_course_invalid", "The course cannot be prepared for checkout.");

        var settings = ReadSettings();
        if (!string.Equals(request.Currency, settings.Currency, StringComparison.OrdinalIgnoreCase))
            throw new PaymentCheckoutException("payment_currency_mismatch", "The requested payment currency does not match the configured Kashier currency.");

        if (string.IsNullOrWhiteSpace(request.Customer?.Email))
        {
            _logger.LogError("payment_customer_email_missing for payment {PaymentId}", request.PaymentReference);
            throw new InvalidOperationException("payment_customer_email_missing");
        }

        if (!string.Equals(settings.Display, "en", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(settings.Display, "ar", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Kashier:Display must be 'en' or 'ar'.");
        }

        var returnUrl = BuildReturnUrl(settings.FrontendBaseUrl, request.CourseSlug);
        var webhookUrl = BuildWebhookUrl(settings.PublicApiBaseUrl);
        var payload = new
        {
            expireAt = DateTime.UtcNow.AddMinutes(30).ToString("o"),
            maxFailureAttempts = 3,
            amount = request.Amount.ToString("F2", CultureInfo.InvariantCulture),
            currency = "EGP",
            // Kashier payment-session documentation uses `order` in its request example.
            order = request.PaymentReference,
            merchantRedirect = returnUrl,
            // Per-session webhook ensures Kashier notifies this server even when
            // the merchant dashboard webhook is not configured or is stale.
            serverWebhook = webhookUrl,
            allowedMethods = settings.AllowedMethods,
            type = "one-time",
            merchantId = settings.MerchantId,
            display = settings.Display.ToLowerInvariant(),
            customer = new { email = request.Customer.Email, reference = request.StudentId }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{settings.ApiBaseUrl}/v3/payment/sessions");
        var secretKey = settings.SecretKey.Trim();
        if (secretKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) secretKey = secretKey[7..].TrimStart();
        message.Headers.TryAddWithoutValidation("Authorization", secretKey);
        message.Headers.TryAddWithoutValidation("api-key", settings.PaymentApiKey);
        var json = JsonSerializer.Serialize(payload);
        _logger.LogDebug("Kashier outgoing body: {Body}", MaskSecrets(json, settings));
        message.Content = new StringContent(json, Encoding.UTF8);
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        HttpResponseMessage response;
        try { response = await _httpClient.SendAsync(message, cancellationToken); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PaymentCheckoutException("payment_provider_unavailable", "The payment provider timed out while creating checkout.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Could not reach Kashier while creating checkout for course {CourseId}", request.CourseId);
            throw new PaymentCheckoutException("payment_provider_unavailable", "The payment provider is temporarily unavailable. Please try again later.");
        }

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Kashier error {Status}: {Body}", (int)response.StatusCode, content);
                throw new PaymentCheckoutException("payment_provider_error", "The payment provider could not create a checkout. Please verify the Kashier configuration and try again.");
            }

            try
            {
                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;
                var sessionUrl = RequiredString(root, "sessionUrl");
                var sessionId = OptionalString(root, "_id") ?? OptionalString(root, "id");
                if (string.IsNullOrWhiteSpace(sessionId)) throw new InvalidOperationException("Missing Kashier session identifier.");
                if (!IsTrustedCheckoutUrl(sessionUrl)) throw new InvalidOperationException("Kashier returned an untrusted checkout URL.");

                _logger.LogInformation("Created Kashier session {SessionId} for course {CourseId}; payment={PaymentReference}; amountCents={AmountCents}; currency={Currency}", sessionId, request.CourseId, request.PaymentReference, request.AmountCents, settings.Currency);
                return new PaymentCheckoutResult
                {
                    Provider = Provider,
                    IsMock = false,
                    CheckoutUrl = sessionUrl,
                    // `order` is our durable merchant reference; use it for webhook correlation.
                    ProviderOrderId = request.PaymentReference,
                    ProviderIntentionId = sessionId
                };
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Kashier returned invalid payment-session JSON for course {CourseId}", request.CourseId);
                throw new PaymentCheckoutException("payment_provider_error", "The payment provider returned an invalid checkout response.");
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(exception, "Kashier returned an incomplete or unsafe checkout response for course {CourseId}", request.CourseId);
                throw new PaymentCheckoutException("payment_provider_error", "The payment provider returned an invalid checkout response.");
            }
        }
    }

    public Task<PaymentProviderConfirmation> ConfirmDevelopmentPaymentAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Kashier payments are confirmed only by verified Kashier webhooks.");

    private KashierSettings ReadSettings()
    {
        var expiry = ReadPositiveInt("Kashier:SessionExpiryMinutes", 60, 1, 1440);
        var attempts = ReadPositiveInt("Kashier:MaxFailureAttempts", 3, 1, 20);
        return new KashierSettings(
            ReadRequired("Kashier:SecretKey"), ReadRequired("Kashier:PaymentApiKey"), ReadRequired("Kashier:MerchantId"),
            ReadAbsoluteHttpsUrl("Kashier:BaseUrl", "https://test-api.kashier.io"),
            ReadAbsoluteHttpsUrl("Payment:PublicApiBaseUrl"), ReadAbsoluteHttpsUrl("Frontend:BaseUrl"),
            ReadCurrency(), ReadRequired("Kashier:AllowedMethods"), expiry, attempts,
            (_configuration["Kashier:Display"] ?? "en").Trim());
    }

    private string ReadCurrency()
    {
        var value = (_configuration["Kashier:Currency"] ?? "EGP").Trim();
        if (value.Length != 3 || !value.All(char.IsLetter)) throw new PaymentCheckoutException("payment_provider_configuration_error", "Kashier:Currency must be a three-letter ISO currency code.");
        return value.ToUpperInvariant();
    }
    private string ReadRequired(string key)
    {
        var value = _configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value)) throw new PaymentCheckoutException("payment_provider_configuration_error", $"{key} must be configured before enabling the Kashier provider.");
        return value;
    }
    private string ReadAbsoluteHttpsUrl(string key, string? defaultValue = null)
    {
        var value = _configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value)) value = defaultValue;
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) throw new PaymentCheckoutException("payment_provider_configuration_error", $"{key} must be an absolute HTTPS URL before enabling the Kashier provider.");
        return value.TrimEnd('/');
    }
    private int ReadPositiveInt(string key, int defaultValue, int min, int max)
    {
        var raw = _configuration[key]; if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (!int.TryParse(raw, out var value) || value < min || value > max) throw new PaymentCheckoutException("payment_provider_configuration_error", $"{key} must be between {min} and {max}.");
        return value;
    }
    private static string BuildReturnUrl(string frontendBaseUrl, string courseSlug) => new Uri(new Uri($"{frontendBaseUrl.TrimEnd('/')}/", UriKind.Absolute), $"courses/{Uri.EscapeDataString(courseSlug)}?payment=return").ToString();

    // Kashier stores this per-session URL verbatim and cannot be told a new one,
    // so a doubled path here silently loses every payment notification for the
    // session. Deployments have been observed with the full webhook path in
    // Payment:PublicApiBaseUrl, so appending must stay idempotent.
    private const string WebhookPath = "/api/payments/kashier/webhook";
    internal static string BuildWebhookUrl(string publicApiBaseUrl)
    {
        var normalized = publicApiBaseUrl.TrimEnd('/');
        return normalized.EndsWith(WebhookPath, StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + WebhookPath;
    }
    private static string MaskSecrets(string json, KashierSettings settings)
    {
        if (string.IsNullOrEmpty(json)) return json;
        return json
            .Replace(settings.SecretKey ?? "\0", "***")
            .Replace(settings.PaymentApiKey ?? "\0", "***")
            .Replace(settings.MerchantId ?? "\0", "***");
    }
    private static string TrimDescription(string value) => value.Length <= 120 ? value : value[..120];
    private static string RequiredString(JsonElement element, string property) => OptionalString(element, property) ?? throw new InvalidOperationException($"Missing Kashier response property '{property}'.");
    private static string? OptionalString(JsonElement element, string property) => !element.TryGetProperty(property, out var item) || item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() : item.GetRawText().Trim();
    private static bool IsTrustedCheckoutUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) && (string.Equals(uri.Host, "payments.kashier.io", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".kashier.io", StringComparison.OrdinalIgnoreCase));
    private sealed record KashierSettings(string SecretKey, string PaymentApiKey, string MerchantId, string ApiBaseUrl, string PublicApiBaseUrl, string FrontendBaseUrl, string Currency, string AllowedMethods, int SessionExpiryMinutes, int MaxFailureAttempts, string Display);
}
