using AlphaZero.Modules.Payments.Domain.Finance;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Purchases;

public enum PurchaseStatus { Paid, PartiallyRefunded, Refunded }

public sealed class Purchase : AggregateRoot
{
    private Purchase() { }
    private Purchase(Guid id, Guid tenantId, Guid userId, Guid productId, Guid priceId, Guid checkoutSessionId,
        Guid providerAccountId, Guid providerTransactionId, string itemTitle, Money presentmentAmount,
        Money settlementAmount, decimal? fxRate, Money providerFee, decimal commissionShare,
        string externalPaymentId, PurchaseStatus status, DateTime paidAt, DateTime? refundedAt) : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        ProductId = productId;
        PriceId = priceId;
        CheckoutSessionId = checkoutSessionId;
        ProviderAccountId = providerAccountId;
        ProviderTransactionId = providerTransactionId;
        ItemTitle = itemTitle;
        PresentmentAmount = presentmentAmount;
        SettlementAmount = settlementAmount;
        FxRate = fxRate;
        ProviderFee = providerFee;
        CommissionShare = commissionShare;
        ExternalPaymentId = externalPaymentId;
        Status = status;
        PaidAt = paidAt;
        RefundedAt = refundedAt;
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid PriceId { get; private set; }
    public Guid CheckoutSessionId { get; private set; }
    public Guid ProviderAccountId { get; private set; }
    public Guid ProviderTransactionId { get; private set; }
    public string ItemTitle { get; private set; } = null!;
    public Money PresentmentAmount { get; private set; } = null!;
    public Money SettlementAmount { get; private set; } = null!;
    public decimal? FxRate { get; private set; }
    public Money ProviderFee { get; private set; } = null!;
    public decimal CommissionShare { get; private set; }
    public string ExternalPaymentId { get; private set; } = null!;
    public PurchaseStatus Status { get; private set; }
    public DateTime PaidAt { get; private set; }
    public DateTime? RefundedAt { get; private set; }
    public PurchaseStatus? ExpectedPreviousStatus { get; private set; }

    public static ErrorOr<Purchase> CreateFromCapture(Guid id, Guid tenantId, Guid userId, Guid productId,
        Guid priceId, Guid checkoutSessionId, Guid providerAccountId, ProviderTransaction movement,
        string itemTitle, Money presentmentAmount, Money lockedPrice, decimal? fxRate, decimal commissionShare,
        string externalPaymentId, DateTime paidAt)
    {
        if (new[] { id, tenantId, userId, productId, priceId, checkoutSessionId, providerAccountId }.Any(x => x == Guid.Empty))
            return Error.Validation("Purchase.Identity", "Purchase identity and ownership fields are required.");
        if (movement.ProviderAccountId != providerAccountId || movement.Type != ProviderTransactionType.Capture
            || movement.Direction != ProviderTransactionDirection.Incoming)
            return Error.Conflict("Purchase.Movement", "Purchase must be funded by an incoming capture from its Provider Account.");
        if (movement.Amount.Currency != lockedPrice.Currency || movement.Amount.Amount != lockedPrice.Amount
            || movement.Amount.Currency != presentmentAmount.Currency && (fxRate is null or <= 0)
            || movement.Amount.Currency == presentmentAmount.Currency && fxRate is not null)
            return Error.Conflict("Purchase.CaptureMismatch", "Provider settlement movement does not match the locked Price and presentment facts.");
        if (movement.Fee.Currency != movement.Amount.Currency)
            return Error.Validation("Purchase.FeeCurrency", "Provider fee must use the settlement currency.");
        if (commissionShare is < 0 or > 1)
            return Error.Validation("Purchase.CommissionShare", "Commission Share must be between zero and one.");
        if (string.IsNullOrWhiteSpace(itemTitle) || itemTitle.Length > 255 || string.IsNullOrWhiteSpace(externalPaymentId)
            || externalPaymentId.Length > 255)
            return Error.Validation("Purchase.Snapshot", "Capture snapshot fields are required.");

        var purchase = new Purchase(id, tenantId, userId, productId, priceId, checkoutSessionId, providerAccountId,
            movement.Id, itemTitle, presentmentAmount, movement.Amount, fxRate, movement.Fee,
            commissionShare, externalPaymentId, PurchaseStatus.Paid, paidAt, null);
        purchase.AddDomainEvent(new PurchasePaidDomainEvent(id, tenantId, userId, productId, paidAt));
        return purchase;
    }

    public static Purchase Reconstitute(Guid id, Guid tenantId, Guid userId, Guid productId, Guid priceId,
        Guid checkoutSessionId, Guid providerAccountId, Guid providerTransactionId, string itemTitle,
        Money presentmentAmount, Money settlementAmount, decimal? fxRate, Money providerFee,
        decimal commissionShare, string externalPaymentId, PurchaseStatus status, DateTime paidAt, DateTime? refundedAt) =>
        new(id, tenantId, userId, productId, priceId, checkoutSessionId, providerAccountId, providerTransactionId,
            itemTitle, presentmentAmount, settlementAmount, fxRate, providerFee, commissionShare, externalPaymentId,
            status, paidAt, refundedAt);

    public ErrorOr<Success> RecordConfirmedRefund(Money confirmedRefund, decimal confirmedReversalTotal, DateTime occurredAt)
    {
        if (confirmedRefund.Currency != SettlementAmount.Currency || confirmedRefund.Amount <= 0
            || confirmedReversalTotal <= 0 || confirmedReversalTotal > SettlementAmount.Amount)
            return Error.Validation("Purchase.Refund", "Confirmed reversal must be positive, in settlement currency, and not exceed gross.");
        if (Status == PurchaseStatus.Refunded) return Result.Success;
        var next = confirmedReversalTotal >= SettlementAmount.Amount
            ? PurchaseStatus.Refunded
            : PurchaseStatus.PartiallyRefunded;
        if (next == Status) return Result.Success;
        ExpectedPreviousStatus = Status;
        Status = next;
        if (next == PurchaseStatus.Refunded) RefundedAt = occurredAt;
        AddDomainEvent(next == PurchaseStatus.Refunded
            ? new PurchaseFullyRefundedDomainEvent(Id, TenantId, occurredAt)
            : new PurchasePartiallyRefundedDomainEvent(Id, TenantId, occurredAt));
        return Result.Success;
    }

    public ErrorOr<Success> RecordLostDispute(Money disputedAmount, decimal confirmedReversalTotal, DateTime occurredAt) =>
        RecordConfirmedRefund(disputedAmount, confirmedReversalTotal, occurredAt);
}

public sealed record PurchasePaidDomainEvent(Guid PurchaseId, Guid TenantId, Guid UserId,
    Guid ProductId, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}

public sealed record PurchasePartiallyRefundedDomainEvent(Guid PurchaseId, Guid TenantId, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}

public sealed record PurchaseFullyRefundedDomainEvent(Guid PurchaseId, Guid TenantId, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}
