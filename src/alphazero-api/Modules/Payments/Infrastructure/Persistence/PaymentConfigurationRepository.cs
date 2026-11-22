using AlphaZero.Modules.Payments.Application.Settings;
using AlphaZero.Modules.Payments.Domain.Providers;
using AlphaZero.Modules.Payments.Domain.Settings;
using AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence;

public sealed class PaymentConfigurationRepository(AppDbContext context) : IPaymentConfigurationRepository
{
    public async Task<AcademyPaymentSettings?> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var row = await context.AcademyPaymentSettings.SingleOrDefaultAsync(settings => settings.TenantId == tenantId, cancellationToken);
        return row is null ? null : AcademyPaymentSettings.Reconstitute(row.Id, row.TenantId, row.OnlineSellingEnabled,
            row.CommissionShare, row.RefundWindowDays, row.UpdatedByUserId, row.CreatedAt, row.UpdatedAt);
    }

    public async Task AddSettingsAsync(AcademyPaymentSettings settings, CancellationToken cancellationToken) =>
        await context.AcademyPaymentSettings.AddAsync(new AcademyPaymentSettingDataModel
        {
            Id = settings.Id, TenantId = settings.TenantId, OnlineSellingEnabled = settings.OnlineSellingEnabled,
            CommissionShare = settings.CommissionShare, RefundWindowDays = settings.RefundWindowDays,
            UpdatedByUserId = settings.UpdatedByUserId, CreatedAt = settings.CreatedAt, UpdatedAt = settings.UpdatedAt
        }, cancellationToken);

    public Task UpdateSettingsAsync(AcademyPaymentSettings settings, CancellationToken cancellationToken)
    {
        var row = context.AcademyPaymentSettings.Local.SingleOrDefault(candidate => candidate.Id == settings.Id);
        if (row is null)
        {
            row = new AcademyPaymentSettingDataModel { Id = settings.Id };
            context.AcademyPaymentSettings.Attach(row);
        }
        row.OnlineSellingEnabled = settings.OnlineSellingEnabled;
        row.CommissionShare = settings.CommissionShare;
        row.RefundWindowDays = settings.RefundWindowDays;
        row.UpdatedByUserId = settings.UpdatedByUserId;
        row.UpdatedAt = settings.UpdatedAt;
        context.Entry(row).Property(candidate => candidate.OnlineSellingEnabled).IsModified = true;
        context.Entry(row).Property(candidate => candidate.CommissionShare).IsModified = true;
        context.Entry(row).Property(candidate => candidate.RefundWindowDays).IsModified = true;
        context.Entry(row).Property(candidate => candidate.UpdatedByUserId).IsModified = true;
        context.Entry(row).Property(candidate => candidate.UpdatedAt).IsModified = true;
        return Task.CompletedTask;
    }

    public Task<bool> ProviderExistsAsync(ProviderName provider, CancellationToken cancellationToken) =>
        context.PaymentProviders.AnyAsync(candidate => candidate.Name == provider.Value && candidate.IsEnabled, cancellationToken);

    public Task<bool> HasStructuralLedgerAccountsAsync(Guid providerAccountId, CancellationToken cancellationToken) =>
        context.LedgerAccounts.AnyAsync(account => account.ProviderAccountId == providerAccountId
            && account.Role == "ProviderAsset", cancellationToken);

    public async Task<ProviderConfiguration?> GetProviderConfigurationAsync(Guid providerAccountId, CancellationToken cancellationToken)
    {
        var row = await context.ProviderAccounts.Include(account => account.ProviderCurrencyRoutes)
            .SingleOrDefaultAsync(account => account.Id == providerAccountId, cancellationToken);
        if (row is null) return null;
        var provider = ProviderName.Create(row.ProviderName).Value;
        var routes = row.ProviderCurrencyRoutes.Select(route => ProviderCurrencyRoute.Create(route.Id, route.ProviderAccountId,
            route.IntegrationCurrency, route.SettlementCurrency, Enum.Parse<PresentmentMode>(route.PresentmentMode),
            route.IsEnabled, route.ShortHoldDays, route.ReserveRate, route.DisputeHoldDays,
            route.RequiresDailyReconciliation, adapterSupportsRoute: true).Value);
        return ProviderConfiguration.Reconstitute(row.Id, provider, Enum.Parse<ProviderEnvironment>(row.Environment),
            row.ExternalAccountId, Enum.Parse<ProviderAccountStatus>(row.Status), row.CredentialSecretName,
            row.WebhookSecretName, routes, row.CreatedAt, row.UpdatedAt);
    }

    public async Task AddProviderConfigurationAsync(ProviderConfiguration configuration, CancellationToken cancellationToken) =>
        await context.ProviderAccounts.AddAsync(new ProviderAccountDataModel
        {
            Id = configuration.Id, ProviderName = configuration.Provider.Value, Environment = configuration.Environment.ToString(),
            ExternalAccountId = configuration.ExternalAccountId, Status = configuration.Status.ToString(),
            CredentialSecretName = configuration.CredentialSecretName, WebhookSecretName = configuration.WebhookSecretName,
            CreatedAt = configuration.CreatedAt, UpdatedAt = configuration.UpdatedAt
        }, cancellationToken);

    public Task UpdateProviderConfigurationAsync(ProviderConfiguration configuration, CancellationToken cancellationToken)
    {
        var account = context.ProviderAccounts.Local.SingleOrDefault(candidate => candidate.Id == configuration.Id);
        if (account is null)
        {
            account = new ProviderAccountDataModel { Id = configuration.Id };
            context.ProviderAccounts.Attach(account);
        }
        account.Status = configuration.Status.ToString();
        account.UpdatedAt = configuration.UpdatedAt;
        context.Entry(account).Property(candidate => candidate.Status).IsModified = true;
        context.Entry(account).Property(candidate => candidate.UpdatedAt).IsModified = true;
        foreach (var route in configuration.Routes)
        {
            var routeRow = context.ProviderCurrencyRoutes.Local.SingleOrDefault(candidate => candidate.Id == route.Id);
            if (routeRow is null)
            {
                routeRow = new ProviderCurrencyRouteDataModel
                {
                    Id = route.Id, ProviderAccountId = configuration.Id, IntegrationCurrency = route.IntegrationCurrency,
                    SettlementCurrency = route.SettlementCurrency, PresentmentMode = route.PresentmentMode.ToString(),
                    IsEnabled = route.IsEnabled, ShortHoldDays = route.ShortHoldDays, ReserveRate = route.ReserveRate,
                    DisputeHoldDays = route.DisputeHoldDays, RequiresDailyReconciliation = route.RequiresDailyReconciliation
                };
                context.ProviderCurrencyRoutes.Add(routeRow);
            }
            else
            {
                routeRow.IsEnabled = route.IsEnabled;
                context.Entry(routeRow).Property(candidate => candidate.IsEnabled).IsModified = true;
            }
        }
        return Task.CompletedTask;
    }
}
