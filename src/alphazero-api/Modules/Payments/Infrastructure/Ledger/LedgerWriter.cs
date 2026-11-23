namespace AlphaZero.Modules.Payments.Infrastructure.Ledger;

using System.Text.Json;
using AlphaZero.Modules.Payments.Application.Ledger;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Modules.Payments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class LedgerWriter(AppDbContext context) : ILedgerWriter
{
    public async Task<Guid> PostAsync(LedgerPosting posting, CancellationToken cancellationToken)
    {
        var description = posting.Description ?? posting.Type switch
        {
            LedgerEntryType.Capture => "Purchase capture",
            LedgerEntryType.RefundReversal => "Refund reversal",
            LedgerEntryType.Settlement => "Settlement",
            LedgerEntryType.ProviderPayout => "Provider payout",
            LedgerEntryType.DisputeLoss => "Dispute loss",
            LedgerEntryType.DisputeWon => "Dispute won",
            LedgerEntryType.UnappliedReceipt => "Unapplied receipt",
            LedgerEntryType.ApplyUnappliedFunds => "Apply unapplied funds",
            LedgerEntryType.ReturnUnappliedFunds => "Return unapplied funds",
            LedgerEntryType.ManualAdjustment => "Manual adjustment",
            _ => $"{posting.Type} ledger entry"
        };

        var payload = JsonSerializer.Serialize(new
        {
            entry = new
            {
                id = posting.Id,
                effectiveAt = posting.EffectiveAt,
                type = posting.Type.ToString(),
                tenantId = posting.TenantId,
                providerAccountId = posting.ProviderAccountId,
                providerTransactionId = posting.ProviderTransactionId,
                purchaseId = posting.PurchaseId,
                refundId = posting.RefundId,
                settlementId = posting.SettlementId,
                disputeId = posting.DisputeId,
                paymentReviewCaseId = posting.PaymentReviewCaseId,
                externalObjectId = posting.ExternalObjectId,
                description
            },
            lines = posting.Lines.Select(line => new
            {
                id = line.Id,
                accountId = line.AccountId,
                direction = line.Direction.ToString(),
                amount = line.Amount.Amount,
                currency = line.Amount.Currency,
                tenantId = line.TenantId
            })
        });

        await context.Database.ExecuteSqlInterpolatedAsync(
            $@"SELECT ""Payments"".post_ledger_entry({payload}::jsonb)", cancellationToken);

        return posting.Id;
    }
}
