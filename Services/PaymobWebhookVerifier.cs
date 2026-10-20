using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Educatinal_Platform.Services;

/// <summary>
/// Validates Paymob Transaction Processed Callback HMAC values. Paymob sends
/// the HMAC as the `hmac` query parameter; it is calculated over the documented
/// ordered callback fields, not over the raw JSON body.
/// </summary>
public sealed class PaymobWebhookVerifier
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymobWebhookVerifier> _logger;

    public PaymobWebhookVerifier(
        IConfiguration configuration,
        ILogger<PaymobWebhookVerifier> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool TryVerifyAndParse(
        string body,
        string providedHmac,
        out PaymobTransactionCallback? callback,
        out string failureCode)
    {
        callback = null;
        failureCode = "payment_callback_invalid";

        var hmacSecret = _configuration["Paymob:HmacSecret"]?.Trim();
        if (string.IsNullOrWhiteSpace(hmacSecret))
        {
            _logger.LogWarning("Rejected Paymob callback because Paymob:HmacSecret is not configured.");
            failureCode = "payment_callback_not_configured";
            return false;
        }

        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(providedHmac))
        {
            failureCode = "payment_callback_missing_signature";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("obj", out var transaction) || transaction.ValueKind != JsonValueKind.Object ||
                !transaction.TryGetProperty("order", out var order) || order.ValueKind != JsonValueKind.Object ||
                !transaction.TryGetProperty("source_data", out var sourceData) || sourceData.ValueKind != JsonValueKind.Object)
            {
                failureCode = "payment_callback_shape_invalid";
                return false;
            }

            var signedPayload = string.Concat(
                Scalar(transaction, "amount_cents"),
                Scalar(transaction, "created_at"),
                Scalar(transaction, "currency"),
                Scalar(transaction, "error_occured"),
                Scalar(transaction, "has_parent_transaction"),
                Scalar(transaction, "id"),
                Scalar(transaction, "integration_id"),
                Scalar(transaction, "is_3d_secure"),
                Scalar(transaction, "is_auth"),
                Scalar(transaction, "is_capture"),
                Scalar(transaction, "is_refunded"),
                Scalar(transaction, "is_standalone_payment"),
                Scalar(transaction, "is_voided"),
                Scalar(order, "id"),
                Scalar(transaction, "owner"),
                Scalar(transaction, "pending"),
                Scalar(sourceData, "pan"),
                Scalar(sourceData, "sub_type"),
                Scalar(sourceData, "type"),
                Scalar(transaction, "success"));

            var computed = HMACSHA512.HashData(
                Encoding.UTF8.GetBytes(hmacSecret),
                Encoding.UTF8.GetBytes(signedPayload));

            if (!MatchesHmac(providedHmac, computed))
            {
                failureCode = "payment_callback_signature_invalid";
                return false;
            }

            var amountCents = RequiredLong(transaction, "amount_cents");
            callback = new PaymobTransactionCallback(
                ProviderTransactionId: RequiredScalar(transaction, "id"),
                ProviderOrderId: RequiredScalar(order, "id"),
                AmountCents: amountCents,
                Currency: RequiredScalar(transaction, "currency").ToUpperInvariant(),
                IntegrationId: RequiredScalar(transaction, "integration_id"),
                Success: RequiredBoolean(transaction, "success"),
                Pending: RequiredBoolean(transaction, "pending"),
                ErrorOccurred: RequiredBoolean(transaction, "error_occured"),
                IsAuth: RequiredBoolean(transaction, "is_auth"),
                IsRefunded: RequiredBoolean(transaction, "is_refunded"),
                IsVoided: RequiredBoolean(transaction, "is_voided"),
                IsStandalonePayment: RequiredBoolean(transaction, "is_standalone_payment"));
            return true;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Rejected malformed Paymob callback JSON.");
            failureCode = "payment_callback_json_invalid";
            return false;
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Rejected Paymob callback with missing required transaction fields.");
            failureCode = "payment_callback_fields_invalid";
            return false;
        }
        catch (FormatException)
        {
            failureCode = "payment_callback_signature_invalid";
            return false;
        }
    }

    private static bool MatchesHmac(string value, byte[] expected)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) normalized = normalized[2..];
        var supplied = Convert.FromHexString(normalized);
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private static string Scalar(JsonElement objectElement, string propertyName)
    {
        if (!objectElement.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : property.GetRawText();
    }

    private static string RequiredScalar(JsonElement objectElement, string propertyName)
    {
        var value = Scalar(objectElement, propertyName);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Missing callback field '{propertyName}'.")
            : value;
    }

    private static long RequiredLong(JsonElement objectElement, string propertyName)
    {
        var value = RequiredScalar(objectElement, propertyName);
        return long.TryParse(value, out var parsed) && parsed >= 0
            ? parsed
            : throw new InvalidOperationException($"Invalid numeric callback field '{propertyName}'.");
    }

    private static bool RequiredBoolean(JsonElement objectElement, string propertyName)
    {
        if (!objectElement.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidOperationException($"Invalid boolean callback field '{propertyName}'.");
        }

        return property.GetBoolean();
    }
}

public sealed record PaymobTransactionCallback(
    string ProviderTransactionId,
    string ProviderOrderId,
    long AmountCents,
    string Currency,
    string IntegrationId,
    bool Success,
    bool Pending,
    bool ErrorOccurred,
    bool IsAuth,
    bool IsRefunded,
    bool IsVoided,
    bool IsStandalonePayment)
{
    public bool IsConfirmedSuccess =>
        Success &&
        !Pending &&
        !ErrorOccurred &&
        !IsAuth &&
        !IsRefunded &&
        !IsVoided &&
        IsStandalonePayment;

    public bool IsFinalFailure =>
        !Pending &&
        (!Success || ErrorOccurred || IsRefunded || IsVoided || IsAuth || !IsStandalonePayment);
}
