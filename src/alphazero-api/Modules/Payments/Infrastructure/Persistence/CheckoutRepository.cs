using AlphaZero.Modules.Payments.Application.Checkout;
using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Checkout;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Modules.Payments.Domain.Providers;
using AlphaZero.Modules.Payments.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using CheckoutRow = AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities.CheckoutSessionDataModel;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence;

public sealed class CheckoutRepository(AppDbContext context) : ICheckoutRepository, ICheckoutSessionLifecycleRepository
{
    public async Task<Product?> GetProductAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken)
    {
        var row = await context.Products.AsNoTracking().SingleOrDefaultAsync(product => product.Id == productId
            && product.TenantId == tenantId, cancellationToken);
        return row is null ? null : Product.Reconstitute(row.Id, row.TenantId, row.ItemType, row.ItemId,
            row.ParentContainerId, row.TargetResourceArn, row.Title, Enum.Parse<SourceAvailability>(row.SourceAvailability),
            row.SourceVersion, row.CreatedAt, row.UpdatedAt);
    }

    public async Task<Price?> GetActivePriceAsync(Guid tenantId, Guid productId, Guid priceId, CancellationToken cancellationToken)
    {
        var row = await context.Prices.AsNoTracking().SingleOrDefaultAsync(price => price.Id == priceId
            && price.TenantId == tenantId && price.ProductId == productId && price.IsActive, cancellationToken);
        return row is null ? null : Price.Reconstitute(row.Id, row.TenantId, row.ProductId,
            Money.Create(row.Amount, row.Currency).Value, row.CreatedByUserId, row.IsActive, row.CreatedAt, row.ArchivedAt);
    }

    public async Task<AcademyPaymentSettings?> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var row = await context.AcademyPaymentSettings.AsNoTracking().SingleOrDefaultAsync(settings => settings.TenantId == tenantId, cancellationToken);
        return row is null ? null : AcademyPaymentSettings.Reconstitute(row.Id, row.TenantId, row.OnlineSellingEnabled,
            row.CommissionShare, row.RefundWindowDays, row.UpdatedByUserId, row.CreatedAt, row.UpdatedAt);
    }

    public async Task<IReadOnlyList<ProviderConfiguration>> GetProviderConfigurationsAsync(CancellationToken cancellationToken)
    {
        var rows = await context.ProviderAccounts.AsNoTracking().Include(account => account.ProviderCurrencyRoutes)
            .ToListAsync(cancellationToken);
        return rows.Select(row =>
        {
            var provider = ProviderName.Create(row.ProviderName).Value;
            var routes = row.ProviderCurrencyRoutes.Select(route => ProviderCurrencyRoute.Create(route.Id, route.ProviderAccountId,
                route.IntegrationCurrency, route.SettlementCurrency, Enum.Parse<PresentmentMode>(route.PresentmentMode),
                route.IsEnabled, route.ShortHoldDays, route.ReserveRate, route.DisputeHoldDays,
                route.RequiresDailyReconciliation, adapterSupportsRoute: true).Value);
            return ProviderConfiguration.Reconstitute(row.Id, provider, Enum.Parse<ProviderEnvironment>(row.Environment),
                row.ExternalAccountId, Enum.Parse<ProviderAccountStatus>(row.Status), row.CredentialSecretName,
                row.WebhookSecretName, routes, row.CreatedAt, row.UpdatedAt);
        }).ToArray();
    }

    public async Task<CheckoutSession?> GetPendingSessionAsync(Guid userId, Guid productId, CancellationToken cancellationToken)
    {
        var row = await context.CheckoutSessions.SingleOrDefaultAsync(session => session.UserId == userId
            && session.ProductId == productId && session.Status == "Pending", cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public async Task AddSessionAsync(CheckoutSession session, CancellationToken cancellationToken) =>
        await context.CheckoutSessions.AddAsync(ToRow(session), cancellationToken);

    public Task UpdateSessionAsync(CheckoutSession session, CancellationToken cancellationToken)
    {
        var row = context.CheckoutSessions.Local.SingleOrDefault(candidate => candidate.Id == session.Id);
        if (row is null)
        {
            row = new CheckoutRow { Id = session.Id };
            context.CheckoutSessions.Attach(row);
        }
        row.ExternalCheckoutId = session.ExternalCheckoutId;
        row.Status = session.Status.ToString();
        row.CompletedAt = session.CompletedAt;
        row.ExpiredAt = session.ExpiredAt;
        context.Entry(row).Property(candidate => candidate.ExternalCheckoutId).IsModified = true;
        context.Entry(row).Property(candidate => candidate.Status).IsModified = true;
        context.Entry(row).Property(candidate => candidate.CompletedAt).IsModified = true;
        context.Entry(row).Property(candidate => candidate.ExpiredAt).IsModified = true;
        return Task.CompletedTask;
    }

    public async Task<CheckoutSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var row = await context.CheckoutSessions.SingleOrDefaultAsync(session => session.Id == sessionId, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public Task UpdateAsync(CheckoutSession session, CancellationToken cancellationToken) => UpdateSessionAsync(session, cancellationToken);

    private static CheckoutSession ToDomain(CheckoutRow row) => CheckoutSession.Reconstitute(row.Id, row.TenantId, row.UserId,
        row.ProductId, row.PriceId, row.ProviderCurrencyRouteId, Enum.Parse<CheckoutSessionStatus>(row.Status),
        row.ExternalCheckoutId, row.CreatedAt, row.CompletedAt, row.ExpiredAt);

    private static CheckoutRow ToRow(CheckoutSession session) => new()
    {
        Id = session.Id, TenantId = session.TenantId, UserId = session.UserId, ProductId = session.ProductId,
        PriceId = session.PriceId, ProviderCurrencyRouteId = session.ProviderCurrencyRouteId,
        ExternalCheckoutId = session.ExternalCheckoutId, Status = session.Status.ToString(),
        CreatedAt = session.CreatedAt, CompletedAt = session.CompletedAt, ExpiredAt = session.ExpiredAt
    };
}
