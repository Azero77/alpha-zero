using AlphaZero.Modules.Payments.Application.Catalog;
using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence;

public sealed class CatalogPriceRepository(AppDbContext context) : IPriceRepository
{
    public async Task<Price?> FindForArchiveAsync(Guid priceId, CancellationToken cancellationToken)
    {
        var row = await context.Prices.SingleOrDefaultAsync(price => price.Id == priceId, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public Task<bool> ProductExistsAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken) =>
        context.Products.AnyAsync(product => product.Id == productId && product.TenantId == tenantId, cancellationToken);

    public Task<bool> HasActivePriceAsync(Guid productId, string currency, CancellationToken cancellationToken) =>
        context.Prices.AnyAsync(price => price.ProductId == productId && price.Currency == currency && price.IsActive, cancellationToken);

    public Task<bool> HasActiveCheckoutAsync(Guid priceId, CancellationToken cancellationToken) =>
        context.CheckoutSessions.AnyAsync(session => session.PriceId == priceId && session.Status == "Pending", cancellationToken);

    public Task<bool> HasPaidPurchaseAsync(Guid priceId, CancellationToken cancellationToken) =>
        context.Purchases.AnyAsync(purchase => purchase.PriceId == priceId, cancellationToken);

    public async Task AddAsync(Price price, CancellationToken cancellationToken)
    {
        await context.Prices.AddAsync(new PriceDataModel
        {
            Id = price.Id,
            TenantId = price.TenantId,
            ProductId = price.ProductId,
            Amount = price.Amount.Amount,
            Currency = price.Amount.Currency,
            IsActive = price.IsActive,
            ArchivedAt = price.ArchivedAt,
            CreatedByUserId = price.CreatedByUserId,
            CreatedAt = price.CreatedAt
        }, cancellationToken);
    }

    public Task UpdateAsync(Price price, CancellationToken cancellationToken)
    {
        var row = context.Prices.Local.SingleOrDefault(candidate => candidate.Id == price.Id);
        if (row is null)
        {
            row = new PriceDataModel { Id = price.Id };
            context.Prices.Attach(row);
        }
        row.IsActive = price.IsActive;
        row.ArchivedAt = price.ArchivedAt;
        context.Entry(row).Property(candidate => candidate.IsActive).IsModified = true;
        context.Entry(row).Property(candidate => candidate.ArchivedAt).IsModified = true;
        return Task.CompletedTask;
    }

    private static Price ToDomain(PriceDataModel row) => Price.Reconstitute(row.Id, row.TenantId, row.ProductId,
        Money.Create(row.Amount, row.Currency).Value, row.CreatedByUserId, row.IsActive, row.CreatedAt, row.ArchivedAt);
}
