using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Educatinal_Platform.Services;

/// <summary>Validates the documented Kashier x-kashier-signature webhook HMAC.</summary>
public sealed class KashierWebhookVerifier
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KashierWebhookVerifier> _logger;
    public KashierWebhookVerifier(IConfiguration configuration, ILogger<KashierWebhookVerifier> logger) { _configuration = configuration; _logger = logger; }

    public bool TryVerifyAndParse(string body, string signature, out KashierTransactionCallback? callback, out string failureCode)
    {
        callback = null; failureCode = "payment_callback_invalid";
        var paymentApiKey = _configuration["Kashier:PaymentApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(paymentApiKey)) { failureCode = "payment_callback_not_configured"; return false; }
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(signature)) { failureCode = "payment_callback_missing_signature"; return false; }
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object || !root.TryGetProperty("event", out var eventProperty)) { failureCode = "payment_callback_shape_invalid"; return false; }
            var eventName = Scalar(eventProperty);
            if (!data.TryGetProperty("signatureKeys", out var keysElement) || keysElement.ValueKind != JsonValueKind.Array) { failureCode = "payment_callback_signature_keys_missing"; return false; }
            var keys = keysElement.EnumerateArray().Select(Scalar).Where(key => !string.IsNullOrWhiteSpace(key)).ToList();
            if (keys.Count == 0 || keys.Count != keys.Distinct(StringComparer.Ordinal).Count()) { failureCode = "payment_callback_signature_keys_invalid"; return false; }
            keys.Sort(StringComparer.Ordinal);
            var pairs = new List<string>(keys.Count);
            foreach (var key in keys)
            {
                if (!data.TryGetProperty(key, out var value) || value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined) { failureCode = "payment_callback_signature_keys_invalid"; return false; }
                pairs.Add($"{key}={Uri.EscapeDataString(Scalar(value))}");
            }
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(paymentApiKey), Encoding.UTF8.GetBytes(string.Join("&", pairs)));
            if (!MatchesSignature(signature, expected)) { failureCode = "payment_callback_signature_invalid"; return false; }

            var amount = RequiredDecimal(data, "amount");
            var amountCents = ToCents(amount);
            callback = new KashierTransactionCallback(
                EventName: eventName,
                ProviderTransactionId: RequiredScalar(data, "transactionId"),
                ProviderOrderId: RequiredScalar(data, "merchantOrderId"),
                KashierOrderId: RequiredScalar(data, "kashierOrderId"),
                AmountCents: amountCents,
                Currency: RequiredScalar(data, "currency").ToUpperInvariant(),
                Status: RequiredScalar(data, "status").ToUpperInvariant());
            return true;
        }
        catch (JsonException exception) { _logger.LogWarning(exception, "Rejected malformed Kashier webhook JSON."); failureCode = "payment_callback_json_invalid"; return false; }
        catch (InvalidOperationException exception) { _logger.LogWarning(exception, "Rejected Kashier callback with invalid required fields."); failureCode = "payment_callback_fields_invalid"; return false; }
        catch (FormatException) { failureCode = "payment_callback_signature_invalid"; return false; }
        catch (OverflowException) { failureCode = "payment_callback_amount_invalid"; return false; }
    }
    private static bool MatchesSignature(string signature, byte[] expected)
    {
        var normalized = signature.Trim(); if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) normalized = normalized[2..];
        var supplied = Convert.FromHexString(normalized); return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
    private static string Scalar(JsonElement element) => element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText();
    private static string RequiredScalar(JsonElement element, string property) => element.TryGetProperty(property, out var value) && !string.IsNullOrWhiteSpace(Scalar(value)) ? Scalar(value) : throw new InvalidOperationException($"Missing callback field '{property}'.");
    private static decimal RequiredDecimal(JsonElement element, string property) => decimal.TryParse(RequiredScalar(element, property), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : throw new InvalidOperationException($"Invalid callback amount '{property}'.");
    private static long ToCents(decimal amount) => checked((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
}

public sealed record KashierTransactionCallback(string EventName, string ProviderTransactionId, string ProviderOrderId, string KashierOrderId, long AmountCents, string Currency, string Status)
{
    public bool IsConfirmedSuccess => string.Equals(EventName, "pay", StringComparison.OrdinalIgnoreCase) && string.Equals(Status, "SUCCESS", StringComparison.OrdinalIgnoreCase);
    public bool IsFinalFailure => string.Equals(EventName, "pay", StringComparison.OrdinalIgnoreCase) && string.Equals(Status, "FAILURE", StringComparison.OrdinalIgnoreCase);
}
