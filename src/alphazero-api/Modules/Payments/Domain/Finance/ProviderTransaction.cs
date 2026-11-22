using AlphaZero.Modules.Payments.Domain.Ledger;
using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Finance;

public enum ProviderTransactionType { Capture, Refund, Payout, DisputeWithdrawal, DisputeReinstatement, Transfer }
public enum ProviderTransactionDirection { Incoming, Outgoing }

public sealed record ProviderTransaction(
    Guid Id,
    Guid ProviderAccountId,
    Guid? WebhookEventId,
    string ExternalTransactionId,
    string? ExternalPaymentId,
    string? ExternalObjectId,
    ProviderTransactionType Type,
    ProviderTransactionDirection Direction,
    Money Amount,
    Money Fee,
    DateTime OccurredAt,
    DateTime RecordedAt)
{
    public static ErrorOr<ProviderTransaction> Record(Guid id, Guid providerAccountId, Guid? webhookEventId,
        string externalTransactionId, string? externalPaymentId, string? externalObjectId,
        ProviderTransactionType type, ProviderTransactionDirection direction, Money amount, Money fee,
        DateTime occurredAt, DateTime recordedAt)
    {
        if (id == Guid.Empty || providerAccountId == Guid.Empty || string.IsNullOrWhiteSpace(externalTransactionId)
            || externalTransactionId.Length > 255)
            return Error.Validation("ProviderTransaction.Identity", "A stable Provider Transaction identity is required.");
        if (amount.Amount <= 0 || fee.Amount < 0 || amount.Currency != fee.Currency)
            return Error.Validation("ProviderTransaction.Amount", "Movement amount and non-negative fee must use one currency.");
        return new ProviderTransaction(id, providerAccountId, webhookEventId, externalTransactionId,
            externalPaymentId, externalObjectId, type, direction, amount, fee, occurredAt, recordedAt);
    }
}
