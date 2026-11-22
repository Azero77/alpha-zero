using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Ledger;

public enum LedgerEntryType { Capture, RefundReversal, Settlement, ProviderPayout, DisputeLoss, DisputeWon, UnappliedReceipt, ApplyUnappliedFunds, ReturnUnappliedFunds, ManualAdjustment }
public enum LedgerDirection { Debit, Credit }
public enum LedgerAccountRole { ProviderAsset, PlatformRevenue, ProviderFees, AcademyPayable, Bank, Cash, UnappliedFunds }

public sealed record LedgerLine(Guid Id, Guid AccountId, LedgerAccountRole AccountRole, LedgerDirection Direction, Money Amount, Guid? TenantId = null)
{
    public static ErrorOr<LedgerLine> Create(Guid id, Guid accountId, LedgerAccountRole role,
        LedgerDirection direction, Money amount, Guid? tenantId = null) =>
        id == Guid.Empty || accountId == Guid.Empty
            ? Error.Validation("LedgerLine.Identity", "Ledger Line and Account ids are required.")
            : new LedgerLine(id, accountId, role, direction, amount, tenantId);
}

public sealed class LedgerPosting
{
    private LedgerPosting(Guid id, LedgerEntryType type, Guid? tenantId, Guid? providerAccountId,
        Guid? providerTransactionId, Guid? purchaseId, Guid? refundId, Guid? settlementId,
        Guid? disputeId, Guid? paymentReviewCaseId, string? externalObjectId, DateTime? effectiveAt,
        IReadOnlyList<LedgerLine> lines)
    {
        Id = id;
        Type = type;
        TenantId = tenantId;
        ProviderAccountId = providerAccountId;
        ProviderTransactionId = providerTransactionId;
        PurchaseId = purchaseId;
        RefundId = refundId;
        SettlementId = settlementId;
        DisputeId = disputeId;
        PaymentReviewCaseId = paymentReviewCaseId;
        ExternalObjectId = externalObjectId;
        EffectiveAt = effectiveAt;
        Lines = lines;
    }

    public Guid Id { get; }
    public LedgerEntryType Type { get; }
    public Guid? TenantId { get; }
    public Guid? ProviderAccountId { get; }
    public Guid? ProviderTransactionId { get; }
    public Guid? PurchaseId { get; }
    public Guid? RefundId { get; }
    public Guid? SettlementId { get; }
    public Guid? DisputeId { get; }
    public Guid? PaymentReviewCaseId { get; }
    public string? ExternalObjectId { get; }
    public DateTime? EffectiveAt { get; }
    public IReadOnlyList<LedgerLine> Lines { get; }
    public string Currency => Lines[0].Amount.Currency;

    public static ErrorOr<LedgerPosting> Create(Guid id, LedgerEntryType type, Guid? tenantId,
        Guid? providerAccountId, Guid? providerTransactionId, Guid? purchaseId, Guid? refundId,
        Guid? settlementId, Guid? disputeId, Guid? paymentReviewCaseId, string? externalObjectId,
        DateTime? effectiveAt, IEnumerable<LedgerLine> lines)
    {
        var materialized = lines.ToArray();
        if (id == Guid.Empty || materialized.Length < 2 || materialized.Any(line => line.Amount.Amount <= 0))
            return Error.Validation("LedgerPosting.Lines", "A posting needs an identity and at least two positive lines.");
        if (materialized.Select(line => line.Amount.Currency).Distinct(StringComparer.Ordinal).Count() != 1)
            return Error.Validation("LedgerPosting.Currency", "All Ledger lines must use one currency.");
        var debits = materialized.Where(line => line.Direction == LedgerDirection.Debit).Sum(line => line.Amount.Amount);
        var credits = materialized.Where(line => line.Direction == LedgerDirection.Credit).Sum(line => line.Amount.Amount);
        if (debits != credits)
            return Error.Validation("LedgerPosting.Unbalanced", "Ledger debits and credits must balance exactly.");
        if (materialized.Any(line => line.TenantId is not null && line.TenantId != tenantId))
            return Error.Validation("LedgerPosting.Tenant", "Academy Ledger accounts must match the entry tenant.");
        if (!ReferencesAreExact(type, providerAccountId, providerTransactionId, purchaseId, refundId,
                settlementId, disputeId, paymentReviewCaseId))
            return Error.Validation("LedgerPosting.ReferenceSet", "Ledger typed references must exactly match the entry type.");
        return new LedgerPosting(id, type, tenantId, providerAccountId, providerTransactionId, purchaseId,
            refundId, settlementId, disputeId, paymentReviewCaseId, externalObjectId, effectiveAt, materialized);
    }

    private static bool ReferencesAreExact(LedgerEntryType type, Guid? providerAccountId, Guid? providerTransactionId,
        Guid? purchaseId, Guid? refundId, Guid? settlementId, Guid? disputeId, Guid? paymentReviewCaseId)
    {
        var expected = type switch
        {
            LedgerEntryType.Capture => (true, true, true, false, false, false, false),
            LedgerEntryType.RefundReversal => (true, true, true, true, false, false, false),
            LedgerEntryType.Settlement => (true, false, false, false, true, false, true),
            LedgerEntryType.ProviderPayout => (true, true, false, false, false, false, false),
            LedgerEntryType.DisputeLoss or LedgerEntryType.DisputeWon => (true, true, true, false, false, true, false),
            LedgerEntryType.UnappliedReceipt => (true, true, false, false, false, false, false),
            LedgerEntryType.ApplyUnappliedFunds => (true, true, true, false, false, false, true),
            LedgerEntryType.ReturnUnappliedFunds => (true, true, false, false, false, false, true),
            LedgerEntryType.ManualAdjustment => (false, false, false, false, false, false, true),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        var actual = (providerAccountId.HasValue, providerTransactionId.HasValue, purchaseId.HasValue,
            refundId.HasValue, settlementId.HasValue, disputeId.HasValue, paymentReviewCaseId.HasValue);
        return expected == actual;
    }
}

public static class LedgerPostingFactory
{
    public static ErrorOr<LedgerPosting> Capture(Guid entryId, Guid tenantId, Guid providerAccountId,
        Guid providerTransactionId, Guid purchaseId, Money gross, Money providerFee, decimal commissionShare,
        Guid providerAssetAccountId, Guid payableAccountId, Guid revenueAccountId, Guid feeAccountId,
        DateTime effectiveAt)
    {
        if (gross.Currency != providerFee.Currency || commissionShare is < 0 or > 1)
            return Error.Validation("LedgerPosting.CaptureInputs", "Capture gross, fee and Commission Share are inconsistent.");
        var commission = Money.Round(gross.Amount * commissionShare, gross.Currency);
        var academy = gross.Amount - commission;
        var lines = new List<LedgerLine>
        {
            new(Guid.NewGuid(), providerAssetAccountId, LedgerAccountRole.ProviderAsset, LedgerDirection.Debit, Money.Create(gross.Amount, gross.Currency).Value)
        };
        if (academy > 0)
            lines.Add(new LedgerLine(Guid.NewGuid(), payableAccountId, LedgerAccountRole.AcademyPayable,
                LedgerDirection.Credit, Money.Create(academy, gross.Currency).Value, tenantId));
        if (commission > 0)
            lines.Add(new LedgerLine(Guid.NewGuid(), revenueAccountId, LedgerAccountRole.PlatformRevenue,
                LedgerDirection.Credit, Money.Create(commission, gross.Currency).Value));
        if (providerFee.Amount > 0)
        {
            lines.Add(new LedgerLine(Guid.NewGuid(), feeAccountId, LedgerAccountRole.ProviderFees, LedgerDirection.Debit, providerFee));
            lines.Add(new LedgerLine(Guid.NewGuid(), providerAssetAccountId, LedgerAccountRole.ProviderAsset, LedgerDirection.Credit, providerFee));
        }
        return LedgerPosting.Create(entryId, LedgerEntryType.Capture, tenantId, providerAccountId,
            providerTransactionId, purchaseId, null, null, null, null, null, effectiveAt, lines);
    }
}
