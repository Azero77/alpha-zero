using System.Text.Json;
using AlphaZero.Modules.Payments.Application.Purchases;
using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Checkout;
using AlphaZero.Modules.Payments.Domain.Finance;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Modules.Payments.Domain.Purchases;
using AlphaZero.Modules.Payments.Domain.Settings;
using AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;
using AlphaZero.Shared.Application;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using CheckoutRow = AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities.CheckoutSessionDataModel;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence;

public sealed class PurchaseCaptureRepository(AppDbContext context) : IPurchaseCaptureRepository
{
    public async Task<CheckoutSession?> GetCheckoutSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var row = await context.CheckoutSessions.FromSqlInterpolated($@"SELECT * FROM ""Payments"".""CheckoutSessions"" WHERE ""Id"" = {sessionId} FOR NO KEY UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : CheckoutSession.Reconstitute(row.Id, row.TenantId, row.UserId, row.ProductId,
            row.PriceId, row.ProviderCurrencyRouteId, Enum.Parse<CheckoutSessionStatus>(row.Status),
            row.ExternalCheckoutId, row.CreatedAt, row.CompletedAt, row.ExpiredAt);
    }

    public Task<Guid?> GetProviderAccountForRouteAsync(Guid routeId, CancellationToken cancellationToken) =>
        context.ProviderCurrencyRoutes.AsNoTracking().Where(route => route.Id == routeId)
            .Select(route => (Guid?)route.ProviderAccountId).SingleOrDefaultAsync(cancellationToken);

    public async Task<Price?> GetPriceAsync(Guid priceId, CancellationToken cancellationToken)
    {
        var row = await context.Prices.AsNoTracking().SingleOrDefaultAsync(price => price.Id == priceId, cancellationToken);
        return row is null ? null : Price.Reconstitute(row.Id, row.TenantId, row.ProductId,
            Money.Create(row.Amount, row.Currency).Value, row.CreatedByUserId, row.IsActive, row.CreatedAt, row.ArchivedAt);
    }

    public async Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var row = await context.Products.AsNoTracking().SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
        return row is null ? null : Product.Reconstitute(row.Id, row.TenantId, row.ItemType, row.ItemId,
            row.ParentContainerId, row.TargetResourceArn, row.Title, Enum.Parse<SourceAvailability>(row.SourceAvailability),
            row.SourceVersion, row.CreatedAt, row.UpdatedAt);
    }

    public async Task<AcademyPaymentSettingsSnapshot?> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var commission = await context.AcademyPaymentSettings.AsNoTracking().Where(settings => settings.TenantId == tenantId)
            .Select(settings => (decimal?)settings.CommissionShare).SingleOrDefaultAsync(cancellationToken);
        return commission is null ? null : new AcademyPaymentSettingsSnapshot(commission.Value);
    }

    public async Task<ProviderTransaction?> FindMovementAsync(Guid providerAccountId, string externalTransactionId, CancellationToken cancellationToken)
    {
        var row = await context.ProviderTransactions.AsNoTracking().SingleOrDefaultAsync(movement =>
            movement.ProviderAccountId == providerAccountId && movement.ExternalTransactionId == externalTransactionId, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public async Task LockMovementIdentityAsync(Guid providerAccountId, string externalTransactionId,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Provider movement identity lock requires an ambient transaction.");
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)context.Database.CurrentTransaction.GetDbTransaction();
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@identity, 0));";
        command.Parameters.AddWithValue("identity", $"{providerAccountId:D}:{externalTransactionId}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddMovementAsync(ProviderTransaction movement, CancellationToken cancellationToken) =>
        await context.ProviderTransactions.AddAsync(new ProviderTransactionDataModel
        {
            Id = movement.Id, ProviderAccountId = movement.ProviderAccountId, WebhookEventId = movement.WebhookEventId,
            ExternalTransactionId = movement.ExternalTransactionId, ExternalPaymentId = movement.ExternalPaymentId,
            ExternalObjectId = movement.ExternalObjectId, Type = movement.Type.ToString(), Direction = movement.Direction.ToString(),
            Amount = movement.Amount.Amount, Currency = movement.Amount.Currency, FeeAmount = movement.Fee.Amount,
            OccurredAt = movement.OccurredAt, RecordedAt = movement.RecordedAt
        }, cancellationToken);

    public async Task<Purchase?> FindPurchaseByMovementAsync(Guid movementId, CancellationToken cancellationToken)
    {
        var row = await context.Purchases.AsNoTracking().SingleOrDefaultAsync(purchase => purchase.ProviderTransactionId == movementId, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public async Task AddPurchaseAsync(Purchase purchase, CancellationToken cancellationToken) =>
        await context.Purchases.AddAsync(new PurchaseDataModel
        {
            Id = purchase.Id, TenantId = purchase.TenantId, UserId = purchase.UserId, ProductId = purchase.ProductId,
            PriceId = purchase.PriceId, CheckoutSessionId = purchase.CheckoutSessionId,
            ProviderAccountId = purchase.ProviderAccountId, ProviderTransactionId = purchase.ProviderTransactionId,
            ItemTitle = purchase.ItemTitle, PresentmentAmount = purchase.PresentmentAmount.Amount,
            PresentmentCurrency = purchase.PresentmentAmount.Currency, SettlementAmount = purchase.SettlementAmount.Amount,
            SettlementCurrency = purchase.SettlementAmount.Currency, FxRate = purchase.FxRate,
            ProviderFee = purchase.ProviderFee.Amount, CommissionShare = purchase.CommissionShare,
            ExternalPaymentId = purchase.ExternalPaymentId, Status = purchase.Status.ToString(),
            PaidAt = purchase.PaidAt, RefundedAt = purchase.RefundedAt
        }, cancellationToken);

    public Task FlushPendingRowsAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<LedgerAccountRole, Guid>> GetCaptureAccountsAsync(Guid tenantId,
        Guid providerAccountId, string currency, CancellationToken cancellationToken)
    {
        var rows = await context.LedgerAccounts.AsNoTracking().Where(account => account.Currency == currency
            && account.IsActive && (account.ProviderAccountId == providerAccountId || account.ProviderAccountId == null)
            && (account.TenantId == tenantId || account.TenantId == null))
            .ToListAsync(cancellationToken);
        var roleMap = new Dictionary<string, LedgerAccountRole>(StringComparer.Ordinal)
        {
            ["ProviderAsset"] = LedgerAccountRole.ProviderAsset,
            ["AcademyPayable"] = LedgerAccountRole.AcademyPayable,
            ["PlatformRevenue"] = LedgerAccountRole.PlatformRevenue,
            ["ProviderFees"] = LedgerAccountRole.ProviderFees
        };
        return rows.Where(row => roleMap.ContainsKey(row.Role)).GroupBy(row => roleMap[row.Role])
            .ToDictionary(group => group.Key, group => group.Single().Id);
    }

    public async Task<Guid> AddAuditAsync(Guid tenantId, Guid purchaseId, Guid ledgerEntryId, string action,
        DateTime occurredAt, CancellationToken cancellationToken)
    {
        var auditId = Guid.NewGuid();
        await context.PaymentsAuditLogs.AddAsync(new PaymentsAuditLogDataModel
        {
            Id = auditId, EventId = Guid.NewGuid(), TenantId = tenantId, AggregateId = purchaseId,
            Action = action, Payload = JsonSerializer.Serialize(new { ledgerEntryId }), OccurredAt = occurredAt
        }, cancellationToken);
        return auditId;
    }

    public Task UpdateCheckoutSessionAsync(CheckoutSession session, CancellationToken cancellationToken)
    {
        var row = context.CheckoutSessions.Local.SingleOrDefault(candidate => candidate.Id == session.Id);
        if (row is null)
        {
            row = new CheckoutRow { Id = session.Id };
            context.CheckoutSessions.Attach(row);
        }
        row.Status = session.Status.ToString();
        row.CompletedAt = session.CompletedAt;
        row.ExpiredAt = session.ExpiredAt;
        context.Entry(row).Property(candidate => candidate.Status).IsModified = true;
        context.Entry(row).Property(candidate => candidate.CompletedAt).IsModified = true;
        context.Entry(row).Property(candidate => candidate.ExpiredAt).IsModified = true;
        return Task.CompletedTask;
    }

    private static ProviderTransaction ToDomain(ProviderTransactionDataModel row) => ProviderTransaction.Record(row.Id,
        row.ProviderAccountId, row.WebhookEventId, row.ExternalTransactionId, row.ExternalPaymentId, row.ExternalObjectId,
        Enum.Parse<ProviderTransactionType>(row.Type), Enum.Parse<ProviderTransactionDirection>(row.Direction),
        Money.Create(row.Amount, row.Currency).Value, Money.Create(row.FeeAmount, row.Currency, allowZero: true).Value,
        row.OccurredAt, row.RecordedAt).Value;

    private static Purchase ToDomain(PurchaseDataModel row) => Purchase.Reconstitute(row.Id, row.TenantId, row.UserId,
        row.ProductId, row.PriceId, row.CheckoutSessionId, row.ProviderAccountId, row.ProviderTransactionId, row.ItemTitle,
        Money.Create(row.PresentmentAmount, row.PresentmentCurrency).Value,
        Money.Create(row.SettlementAmount, row.SettlementCurrency).Value, row.FxRate,
        Money.Create(row.ProviderFee, row.SettlementCurrency, allowZero: true).Value, row.CommissionShare,
        row.ExternalPaymentId, Enum.Parse<PurchaseStatus>(row.Status), row.PaidAt, row.RefundedAt);
}
