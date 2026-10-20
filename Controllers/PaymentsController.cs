using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Security.Claims;

namespace Educatinal_Platform.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IEnrollmentService _enrollmentService;
    private readonly PaymobWebhookVerifier _paymobWebhookVerifier;
    private readonly KashierWebhookVerifier _kashierWebhookVerifier;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IEnrollmentService enrollmentService,
        PaymobWebhookVerifier paymobWebhookVerifier,
        KashierWebhookVerifier kashierWebhookVerifier,
        ILogger<PaymentsController> logger)
    {
        _enrollmentService = enrollmentService;
        _paymobWebhookVerifier = paymobWebhookVerifier;
        _kashierWebhookVerifier = kashierWebhookVerifier;
        _logger = logger;
    }

    /// <summary>
    /// Explicit development-only confirmation. The browser never creates an
    /// enrollment itself; the active Mock provider confirms it server-side.
    /// </summary>
    [Authorize]
    [HttpPost("mock/{paymentReference}/confirm")]
    public async Task<IActionResult> ConfirmMockPayment(
        string paymentReference,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(paymentReference, out _))
        {
            return BadRequest(new { code = "payment_not_found", message = "The development payment could not be found." });
        }

        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId)) return Unauthorized();

        var result = await _enrollmentService.ConfirmMockPaymentAsync(
            studentId,
            paymentReference,
            cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = result.Code, message = result.Message });
        }

        return Ok(new
        {
            success = true,
            enrollmentId = result.EnrollmentId,
            provider = result.Provider,
            isMock = result.IsMock,
            accessType = result.AccessType,
            message = result.Message
        });
    }

    /// <summary>
    /// Paymob Transaction Processed Callback. Browser redirects are not trusted
    /// for enrollment: a valid callback HMAC and exact stored payment match are
    /// required before fulfillment can occur.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("paymob/webhook")]
    public async Task<IActionResult> PaymobWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var hmac = Request.Query["hmac"].ToString();

        if (!_paymobWebhookVerifier.TryVerifyAndParse(body, hmac, out var callback, out var failureCode))
        {
            _logger.LogWarning("Rejected Paymob callback with code {FailureCode}", failureCode);
            return Unauthorized(new { code = failureCode, message = "Invalid payment callback." });
        }

        var result = await _enrollmentService.ConfirmPaymobPaymentAsync(callback!, cancellationToken);
        if (result == null)
        {
            _logger.LogWarning(
                "Received verified Paymob callback for unknown order {OrderId}",
                callback!.ProviderOrderId);
            // Acknowledge a valid but unrelated callback without provisioning any access.
            return Ok(new { received = true });
        }

        if (!result.Success)
        {
            _logger.LogWarning(
                "Paymob callback {TransactionId} did not activate enrollment: {Code}",
                callback!.ProviderTransactionId,
                result.Code);
        }

        // Returning 200 avoids retrying a known failed/cancelled payment. A server
        // exception remains a 5xx and can be retried safely by Paymob.
        return Ok(new { received = true });
    }
    /// <summary>
    /// Kashier webhook. The browser return route is never trusted for payment
    /// fulfillment; only a valid signed SUCCESS pay event can activate access.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("kashier/webhook")]
    public async Task<IActionResult> KashierWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["x-kashier-signature"].ToString();

        if (!_kashierWebhookVerifier.TryVerifyAndParse(body, signature, out var callback, out var failureCode))
        {
            _logger.LogWarning("Rejected Kashier callback with code {FailureCode}", failureCode);
            return Unauthorized();
        }

        var result = await _enrollmentService.ConfirmKashierPaymentAsync(callback!, cancellationToken);
        if (result == null)
        {
            _logger.LogWarning("Received verified Kashier callback for unknown order reference {OrderReference}", callback!.ProviderOrderId);
            return Ok();
        }

        if (!result.Success)
        {
            _logger.LogWarning("Kashier callback {TransactionId} did not activate enrollment: {Code}", callback!.ProviderTransactionId, result.Code);
        }

        // Kashier treats 200 as acknowledged. The enrollment finalizer is
        // idempotent, so repeated valid callbacks do not create duplicate access.
        return Ok();
    }

    /// <summary>
    /// Returns the current payment status to the authenticated student.
    /// The frontend calls this after returning from a hosted checkout to
    /// discover whether the webhook has been processed and an enrollment exists.
    /// </summary>
    [Authorize]
    [HttpGet("{paymentReference}/status")]
    public async Task<IActionResult> GetPaymentStatus(
        string paymentReference,
        CancellationToken cancellationToken)
    {
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId)) return Unauthorized();

        var result = await _enrollmentService.GetPaymentStatusAsync(
            studentId,
            paymentReference,
            cancellationToken);

        if (string.Equals(result.Status, "not_found", StringComparison.Ordinal))
        {
            return NotFound(new { status = result.Status, message = result.Message });
        }

        return Ok(new
        {
            paymentReference = result.PaymentReference,
            status = result.Status,
            enrollmentId = result.EnrollmentId,
            accessType = result.AccessType,
            message = result.Message
        });
    }
}
