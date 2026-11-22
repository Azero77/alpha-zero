using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Checkout;

public enum CheckoutSessionStatus { Pending, Completed, Expired }

public sealed class CheckoutSession : AggregateRoot
{
    private CheckoutSession() { }
    private CheckoutSession(Guid id, Guid tenantId, Guid userId, Guid productId, Guid priceId,
        Guid providerCurrencyRouteId, CheckoutSessionStatus status, string? externalCheckoutId,
        DateTime createdAt, DateTime? completedAt, DateTime? expiredAt) : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        ProductId = productId;
        PriceId = priceId;
        ProviderCurrencyRouteId = providerCurrencyRouteId;
        Status = status;
        ExternalCheckoutId = externalCheckoutId;
        CreatedAt = createdAt;
        CompletedAt = completedAt;
        ExpiredAt = expiredAt;
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid PriceId { get; private set; }
    public Guid ProviderCurrencyRouteId { get; private set; }
    public CheckoutSessionStatus Status { get; private set; }
    public string? ExternalCheckoutId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? ExpiredAt { get; private set; }
    public CheckoutSessionStatus? ExpectedPreviousStatus { get; private set; }

    public static ErrorOr<CheckoutSession> Create(Guid id, Guid tenantId, Guid userId, Guid productId,
        Guid priceId, Guid providerCurrencyRouteId, DateTime now)
    {
        if (new[] { id, tenantId, userId, productId, priceId, providerCurrencyRouteId }.Any(value => value == Guid.Empty))
            return Error.Validation("CheckoutSession.Identity", "All Checkout Session identities are required.");
        var session = new CheckoutSession(id, tenantId, userId, productId, priceId, providerCurrencyRouteId,
            CheckoutSessionStatus.Pending, null, now, null, null);
        session.AddDomainEvent(new CheckoutSessionCreatedDomainEvent(id, tenantId, userId, productId, priceId, providerCurrencyRouteId, now));
        return session;
    }

    public static CheckoutSession Reconstitute(Guid id, Guid tenantId, Guid userId, Guid productId, Guid priceId,
        Guid providerCurrencyRouteId, CheckoutSessionStatus status, string? externalCheckoutId, DateTime createdAt,
        DateTime? completedAt, DateTime? expiredAt) => new(id, tenantId, userId, productId, priceId,
        providerCurrencyRouteId, status, externalCheckoutId, createdAt, completedAt, expiredAt);

    public ErrorOr<Success> StampProviderSession(string externalCheckoutId)
    {
        if (string.IsNullOrWhiteSpace(externalCheckoutId) || externalCheckoutId.Length > 255)
            return Error.Validation("CheckoutSession.ExternalId", "Provider checkout identity is invalid.");
        if (Status != CheckoutSessionStatus.Pending)
            return Error.Conflict("CheckoutSession.NotPending", "Provider session can only be stamped while Pending.");
        if (ExternalCheckoutId == externalCheckoutId) return Result.Success;
        ExternalCheckoutId = externalCheckoutId;
        return Result.Success;
    }

    public ErrorOr<Success> Expire(DateTime now)
    {
        if (Status == CheckoutSessionStatus.Expired) return Result.Success;
        if (Status != CheckoutSessionStatus.Pending)
            return Error.Conflict("CheckoutSession.NotPending", "Only a Pending Checkout Session can expire.");
        ExpectedPreviousStatus = Status;
        Status = CheckoutSessionStatus.Expired;
        ExpiredAt = now;
        AddDomainEvent(new CheckoutSessionExpiredDomainEvent(Id, TenantId, now));
        return Result.Success;
    }

    public ErrorOr<Success> CompleteFromMovement(Guid providerAccountId, Guid expectedProviderAccountId,
        Money movementAmount, Money lockedPrice, bool providerEvidenceAllowsLateCompletion, DateTime occurredAt)
    {
        if (providerAccountId != expectedProviderAccountId)
            return Error.Conflict("CheckoutSession.ProviderAccount", "Movement belongs to a different Provider Account.");
        if (movementAmount != lockedPrice)
            return Error.Conflict("CheckoutSession.AmountMismatch", "Movement does not exactly match the locked Price.");
        if (Status == CheckoutSessionStatus.Completed) return Result.Success;
        if (Status == CheckoutSessionStatus.Expired && !providerEvidenceAllowsLateCompletion)
            return Error.Conflict("CheckoutSession.Expired", "Expired session cannot complete without valid late-payment evidence.");
        if (Status is not (CheckoutSessionStatus.Pending or CheckoutSessionStatus.Expired))
            return Error.Conflict("CheckoutSession.InvalidStatus", "Checkout Session cannot complete from its current status.");

        ExpectedPreviousStatus = Status;
        Status = CheckoutSessionStatus.Completed;
        CompletedAt = occurredAt;
        AddDomainEvent(new CheckoutSessionCompletedDomainEvent(Id, TenantId, occurredAt));
        return Result.Success;
    }
}

public sealed record CheckoutSessionCreatedDomainEvent(Guid SessionId, Guid TenantId, Guid UserId,
    Guid ProductId, Guid PriceId, Guid RouteId, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}

public sealed record CheckoutSessionExpiredDomainEvent(Guid SessionId, Guid TenantId, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}

public sealed record CheckoutSessionCompletedDomainEvent(Guid SessionId, Guid TenantId, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}
