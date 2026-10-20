using Educatinal_Platform.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories;

public interface IPaymentTransactionRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
    Task<PaymentTransaction> CreateAsync(PaymentTransaction payment, CancellationToken cancellationToken = default);
    Task<PaymentTransaction> GetOrCreatePendingAsync(PaymentTransaction payment, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetPendingByStudentAndCourseAsync(string provider, string studentId, string courseId, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetByProviderOrderIdAsync(string provider, string providerOrderId, CancellationToken cancellationToken = default);
    Task UpdateCheckoutAsync(string id, string? checkoutUrl, string? providerOrderId, string? providerIntentionId, string? providerIntegrationId, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(string id, string failureCode, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> MarkSucceededAsync(string id, string providerTransactionId, CancellationToken cancellationToken = default);
    Task<bool> TryClaimFulfillmentAsync(string id, string claimToken, CancellationToken cancellationToken = default);
    Task ReleaseFulfillmentClaimAsync(string id, string claimToken, CancellationToken cancellationToken = default);
    Task SetEnrollmentAsync(string id, string claimToken, string enrollmentId, CancellationToken cancellationToken = default);
}

public sealed class PaymentTransactionRepository : IPaymentTransactionRepository
{
    private static readonly TimeSpan FulfillmentClaimLease = TimeSpan.FromMinutes(5);
    private readonly IMongoCollection<PaymentTransaction> _payments;

    public PaymentTransactionRepository(IMongoDatabase database)
    {
        _payments = database.GetCollection<PaymentTransaction>("PaymentTransactions");
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        // Provider order/transaction values are unique only when present. Sparse
        // partial indexes keep Mock's pre-checkout records valid while protecting
        // against duplicate or replayed Paymob notifications.
        var orderIndex = new CreateIndexModel<PaymentTransaction>(
            Builders<PaymentTransaction>.IndexKeys
                .Ascending(payment => payment.Provider)
                .Ascending(payment => payment.ProviderOrderId),
            new CreateIndexOptions<PaymentTransaction>
            {
                Name = "ux_payment_provider_order",
                Unique = true,
                PartialFilterExpression = new BsonDocument(
                    nameof(PaymentTransaction.ProviderOrderId),
                    new BsonDocument("$type", "string"))
            });

        var transactionIndex = new CreateIndexModel<PaymentTransaction>(
            Builders<PaymentTransaction>.IndexKeys
                .Ascending(payment => payment.Provider)
                .Ascending(payment => payment.ProviderTransactionId),
            new CreateIndexOptions<PaymentTransaction>
            {
                Name = "ux_payment_provider_transaction",
                Unique = true,
                PartialFilterExpression = new BsonDocument(
                    nameof(PaymentTransaction.ProviderTransactionId),
                    new BsonDocument("$type", "string"))
            });

        var activeCheckoutIndex = new CreateIndexModel<PaymentTransaction>(
            Builders<PaymentTransaction>.IndexKeys.Ascending(payment => payment.ActiveCheckoutKey),
            new CreateIndexOptions<PaymentTransaction>
            {
                Name = "ux_payment_active_checkout",
                Unique = true,
                PartialFilterExpression = new BsonDocument(
                    nameof(PaymentTransaction.ActiveCheckoutKey),
                    new BsonDocument("$type", "string"))
            });

        return _payments.Indexes.CreateManyAsync(
            new[] { orderIndex, transactionIndex, activeCheckoutIndex },
            cancellationToken);
    }

    public async Task<PaymentTransaction> CreateAsync(
        PaymentTransaction payment,
        CancellationToken cancellationToken = default)
    {
        await _payments.InsertOneAsync(payment, cancellationToken: cancellationToken);
        return payment;
    }

    public async Task<PaymentTransaction> GetOrCreatePendingAsync(
        PaymentTransaction payment,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payment.ActiveCheckoutKey))
            throw new ArgumentException("An active checkout key is required.", nameof(payment));

        var update = Builders<PaymentTransaction>.Update
            .SetOnInsert(item => item.Id, payment.Id)
            .SetOnInsert(item => item.Provider, payment.Provider)
            .SetOnInsert(item => item.IsMock, payment.IsMock)
            .SetOnInsert(item => item.StudentId, payment.StudentId)
            .SetOnInsert(item => item.CourseId, payment.CourseId)
            .SetOnInsert(item => item.AccessType, payment.AccessType)
            .SetOnInsert(item => item.Amount, payment.Amount)
            .SetOnInsert(item => item.AmountCents, payment.AmountCents)
            .SetOnInsert(item => item.Currency, payment.Currency)
            .SetOnInsert(item => item.Status, payment.Status)
            .SetOnInsert(item => item.CreatedAt, payment.CreatedAt)
            .SetOnInsert(item => item.ActiveCheckoutKey, payment.ActiveCheckoutKey);

        return await _payments.FindOneAndUpdateAsync(
            item => item.ActiveCheckoutKey == payment.ActiveCheckoutKey,
            update,
            new FindOneAndUpdateOptions<PaymentTransaction>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<PaymentTransaction?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        await _payments.Find(payment => payment.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<PaymentTransaction?> GetPendingByStudentAndCourseAsync(
        string provider,
        string studentId,
        string courseId,
        CancellationToken cancellationToken = default) =>
        await _payments.Find(payment =>
                payment.Provider == provider &&
                payment.StudentId == studentId &&
                payment.CourseId == courseId &&
                payment.Status == "pending")
            .SortByDescending(payment => payment.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PaymentTransaction?> GetByProviderOrderIdAsync(
        string provider,
        string providerOrderId,
        CancellationToken cancellationToken = default) =>
        await _payments.Find(payment =>
                payment.Provider == provider &&
                payment.ProviderOrderId == providerOrderId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task UpdateCheckoutAsync(
        string id,
        string? checkoutUrl,
        string? providerOrderId,
        string? providerIntentionId,
        string? providerIntegrationId,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<PaymentTransaction>.Update
            .Set(payment => payment.CheckoutUrl, checkoutUrl)
            .Set(payment => payment.ProviderOrderId, providerOrderId)
            .Set(payment => payment.ProviderIntentionId, providerIntentionId)
            .Set(payment => payment.ProviderIntegrationId, providerIntegrationId);

        return _payments.UpdateOneAsync(
            payment => payment.Id == id && payment.Status == "pending",
            update,
            cancellationToken: cancellationToken);
    }

    public Task MarkFailedAsync(
        string id,
        string failureCode,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<PaymentTransaction>.Update
            .Set(payment => payment.Status, "failed")
            .Set(payment => payment.FailureCode, failureCode)
            .Set(payment => payment.ActiveCheckoutKey, null);

        return _payments.UpdateOneAsync(
            payment => payment.Id == id && payment.Status == "pending",
            update,
            cancellationToken: cancellationToken);
    }

    public async Task<PaymentTransaction?> MarkSucceededAsync(
        string id,
        string providerTransactionId,
        CancellationToken cancellationToken = default)
    {
        var current = await GetByIdAsync(id, cancellationToken);
        if (current == null) return null;
        if (current.Status == "succeeded") return current;
        if (current.Status is not ("pending" or "failed")) return null;

        // A later successful attempt for the same Paymob order can validly follow
        // an earlier failed callback. Enrollment remains protected by the claim.
        var update = Builders<PaymentTransaction>.Update
            .Set(payment => payment.Status, "succeeded")
            .Set(payment => payment.ProviderTransactionId, providerTransactionId)
            .Set(payment => payment.CompletedAt, DateTime.UtcNow)
            .Set(payment => payment.FailureCode, null)
            .Set(payment => payment.ActiveCheckoutKey, null);

        var updated = await _payments.FindOneAndUpdateAsync(
            payment => payment.Id == id &&
                       (payment.Status == "pending" || payment.Status == "failed") &&
                       payment.EnrollmentId == null,
            update,
            new FindOneAndUpdateOptions<PaymentTransaction>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        return updated ?? await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> TryClaimFulfillmentAsync(
        string id,
        string claimToken,
        CancellationToken cancellationToken = default)
    {
        var leaseExpiredBefore = DateTime.UtcNow.Subtract(FulfillmentClaimLease);
        var filter = Builders<PaymentTransaction>.Filter.And(
            Builders<PaymentTransaction>.Filter.Eq(payment => payment.Id, id),
            Builders<PaymentTransaction>.Filter.Eq(payment => payment.Status, "succeeded"),
            Builders<PaymentTransaction>.Filter.Eq(payment => payment.EnrollmentId, null),
            Builders<PaymentTransaction>.Filter.Or(
                Builders<PaymentTransaction>.Filter.Eq(payment => payment.FulfillmentClaimToken, null),
                Builders<PaymentTransaction>.Filter.Lt(payment => payment.FulfillmentClaimedAt, leaseExpiredBefore)));

        var update = Builders<PaymentTransaction>.Update
            .Set(payment => payment.FulfillmentClaimToken, claimToken)
            .Set(payment => payment.FulfillmentClaimedAt, DateTime.UtcNow);
        var result = await _payments.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public Task ReleaseFulfillmentClaimAsync(
        string id,
        string claimToken,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<PaymentTransaction>.Update
            .Set(payment => payment.FulfillmentClaimToken, null)
            .Set(payment => payment.FulfillmentClaimedAt, null);

        return _payments.UpdateOneAsync(
            payment => payment.Id == id &&
                       payment.EnrollmentId == null &&
                       payment.FulfillmentClaimToken == claimToken,
            update,
            cancellationToken: cancellationToken);
    }

    public Task SetEnrollmentAsync(
        string id,
        string claimToken,
        string enrollmentId,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<PaymentTransaction>.Update
            .Set(payment => payment.EnrollmentId, enrollmentId)
            .Set(payment => payment.FulfillmentClaimToken, null)
            .Set(payment => payment.FulfillmentClaimedAt, null);

        return _payments.UpdateOneAsync(
            payment => payment.Id == id &&
                       payment.FulfillmentClaimToken == claimToken &&
                       payment.EnrollmentId == null,
            update,
            cancellationToken: cancellationToken);
    }
}
