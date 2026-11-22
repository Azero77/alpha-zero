using AlphaZero.Modules.Payments.Domain.Providers;
using AlphaZero.Modules.Payments.Domain.Settings;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Payments.Application.Settings;

public interface IPaymentConfigurationRepository
{
    Task<AcademyPaymentSettings?> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken);
    Task AddSettingsAsync(AcademyPaymentSettings settings, CancellationToken cancellationToken);
    Task UpdateSettingsAsync(AcademyPaymentSettings settings, CancellationToken cancellationToken);
    Task<bool> ProviderExistsAsync(ProviderName provider, CancellationToken cancellationToken);
    Task<bool> HasStructuralLedgerAccountsAsync(Guid providerAccountId, CancellationToken cancellationToken);
    Task<ProviderConfiguration?> GetProviderConfigurationAsync(Guid providerAccountId, CancellationToken cancellationToken);
    Task AddProviderConfigurationAsync(ProviderConfiguration configuration, CancellationToken cancellationToken);
    Task UpdateProviderConfigurationAsync(ProviderConfiguration configuration, CancellationToken cancellationToken);
}

public sealed record UpdateAcademyPaymentSettings(Guid TenantId, bool OnlineSellingEnabled, decimal CommissionShare,
    short? RefundWindowDays, Guid ActorUserId) : ICommand<AcademyPaymentSettings>, ITransactionalCommand;

public sealed class UpdateAcademyPaymentSettingsHandler(IPaymentConfigurationRepository repository)
    : IRequestHandler<UpdateAcademyPaymentSettings, ErrorOr<AcademyPaymentSettings>>
{
    public async Task<ErrorOr<AcademyPaymentSettings>> Handle(UpdateAcademyPaymentSettings request, CancellationToken cancellationToken)
    {
        var settings = await repository.GetSettingsAsync(request.TenantId, cancellationToken);
        if (settings is null)
        {
            var created = AcademyPaymentSettings.CreatePolicy(Guid.NewGuid(), request.TenantId,
                request.OnlineSellingEnabled, request.CommissionShare, request.RefundWindowDays,
                request.ActorUserId, DateTime.UtcNow);
            if (created.IsError) return created.Errors;
            await repository.AddSettingsAsync(created.Value, cancellationToken);
            return created.Value;
        }
        var update = settings.UpdatePolicy(request.OnlineSellingEnabled, request.CommissionShare,
            request.RefundWindowDays, request.ActorUserId, DateTime.UtcNow);
        if (update.IsError) return update.Errors;
        await repository.UpdateSettingsAsync(settings, cancellationToken);
        return settings;
    }
}

public sealed record ConfigureProviderAccount(Guid Id, string ProviderName, ProviderEnvironment Environment,
    string ExternalAccountId, ProviderAccountStatus Status, string CredentialSecretName, string WebhookSecretName)
    : ICommand<ProviderConfiguration>, ITransactionalCommand;

public sealed class ConfigureProviderAccountHandler(IPaymentConfigurationRepository repository)
    : IRequestHandler<ConfigureProviderAccount, ErrorOr<ProviderConfiguration>>
{
    public async Task<ErrorOr<ProviderConfiguration>> Handle(ConfigureProviderAccount request, CancellationToken cancellationToken)
    {
        var provider = ProviderName.Create(request.ProviderName);
        if (provider.IsError) return provider.Errors;
        if (!await repository.ProviderExistsAsync(provider.Value, cancellationToken))
            return Error.Conflict("Provider.Disabled", "Provider is not registered and enabled in Payments.");
        var account = ProviderConfiguration.ConfigureAccount(request.Id, provider.Value, request.Environment,
            request.ExternalAccountId, request.Status, request.CredentialSecretName, request.WebhookSecretName, DateTime.UtcNow);
        if (account.IsError) return account.Errors;
        if (!await repository.HasStructuralLedgerAccountsAsync(request.Id, cancellationToken))
            return Error.Conflict("ProviderAccount.LedgerAccounts", "Structural Ledger accounts must be provisioned before configuring this account.");
        await repository.AddProviderConfigurationAsync(account.Value, cancellationToken);
        return account.Value;
    }
}

public sealed record ConfigureProviderCurrencyRoute(Guid ProviderAccountId, Guid RouteId, string IntegrationCurrency,
    string SettlementCurrency, PresentmentMode PresentmentMode, bool Enabled, short ShortHoldDays,
    decimal ReserveRate, short DisputeHoldDays, bool RequiresDailyReconciliation, bool AdapterSupportsRoute)
    : ICommand<ProviderCurrencyRoute>, ITransactionalCommand;

public sealed class ConfigureProviderCurrencyRouteHandler(IPaymentConfigurationRepository repository)
    : IRequestHandler<ConfigureProviderCurrencyRoute, ErrorOr<ProviderCurrencyRoute>>
{
    public async Task<ErrorOr<ProviderCurrencyRoute>> Handle(ConfigureProviderCurrencyRoute request, CancellationToken cancellationToken)
    {
        var configuration = await repository.GetProviderConfigurationAsync(request.ProviderAccountId, cancellationToken);
        if (configuration is null) return Error.NotFound("ProviderAccount.NotFound", "Provider Account was not found.");
        var route = configuration.ConfigureCurrencyRoute(request.RouteId, request.IntegrationCurrency,
            request.SettlementCurrency, request.PresentmentMode, request.Enabled, request.ShortHoldDays,
            request.ReserveRate, request.DisputeHoldDays, request.RequiresDailyReconciliation,
            request.AdapterSupportsRoute, DateTime.UtcNow);
        if (route.IsError) return route.Errors;
        await repository.UpdateProviderConfigurationAsync(configuration, cancellationToken);
        return route.Value;
    }
}
