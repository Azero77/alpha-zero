namespace AlphaZero.Modules.Payments.Application.Ledger;

using AlphaZero.Modules.Payments.Domain.Ledger;

public interface ILedgerWriter
{
    Task<Guid> PostAsync(LedgerPosting posting, CancellationToken cancellationToken);
}
