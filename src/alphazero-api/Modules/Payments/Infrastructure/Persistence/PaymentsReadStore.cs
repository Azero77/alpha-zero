using AlphaZero.Modules.Payments.Application.Queries;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence;

public sealed class PaymentsReadStore(AppDbContext context) : IPaymentsReadStore
{
    public async Task<PurchasePage> ListStudentPurchasesAsync(Guid tenantId, Guid userId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = context.Purchases.AsNoTracking()
            .Where(purchase => purchase.TenantId == tenantId && purchase.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(purchase => purchase.PaidAt).ThenBy(purchase => purchase.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(purchase => new PurchaseSummary(purchase.Id, purchase.ProductId, purchase.ItemTitle,
                purchase.SettlementAmount, purchase.SettlementCurrency, purchase.Status, purchase.PaidAt))
            .ToListAsync(cancellationToken);
        return new PurchasePage(items, total, page, pageSize);
    }

    public Task<PurchaseDetail?> GetStudentPurchaseAsync(Guid tenantId, Guid userId, Guid purchaseId,
        CancellationToken cancellationToken) =>
        context.Purchases.AsNoTracking()
            .Where(purchase => purchase.Id == purchaseId && purchase.TenantId == tenantId
                && purchase.UserId == userId)
            .Select(purchase => new PurchaseDetail(purchase.Id, purchase.ProductId, purchase.PriceId,
                purchase.CheckoutSessionId, purchase.ProviderAccountId, purchase.ProviderTransactionId,
                purchase.ItemTitle, purchase.PresentmentAmount, purchase.PresentmentCurrency,
                purchase.SettlementAmount, purchase.SettlementCurrency, purchase.FxRate, purchase.ProviderFee,
                purchase.CommissionShare, purchase.ExternalPaymentId, purchase.Status, purchase.PaidAt,
                purchase.RefundedAt))
            .SingleOrDefaultAsync(cancellationToken);
}
