using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Providers;

public sealed class ProviderConfiguration : AggregateRoot
{
    private readonly List<ProviderCurrencyRoute> _routes = [];
    private ProviderConfiguration() { }

    private ProviderConfiguration(Guid id, ProviderName provider, ProviderEnvironment environment, string externalAccountId,
        ProviderAccountStatus status, string credentialSecretName, string webhookSecretName, DateTime createdAt, DateTime? updatedAt)
        : base(id)
    {
        Provider = provider;
        Environment = environment;
        ExternalAccountId = externalAccountId;
        Status = status;
        CredentialSecretName = credentialSecretName;
        WebhookSecretName = webhookSecretName;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public ProviderName Provider { get; private set; } = null!;
    public ProviderEnvironment Environment { get; private set; }
    public string ExternalAccountId { get; private set; } = null!;
    public ProviderAccountStatus Status { get; private set; }
    public string CredentialSecretName { get; private set; } = null!;
    public string WebhookSecretName { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public IReadOnlyCollection<ProviderCurrencyRoute> Routes => _routes.AsReadOnly();
    public ProviderAccountStatus? ExpectedPreviousStatus { get; private set; }

    public static ErrorOr<ProviderConfiguration> ConfigureAccount(Guid id, ProviderName provider,
        ProviderEnvironment environment, string externalAccountId, ProviderAccountStatus status,
        string credentialSecretName, string webhookSecretName, DateTime now)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(externalAccountId) || externalAccountId.Length > 255)
            return Error.Validation("ProviderAccount.Identity", "A Provider Account id and external identity are required.");
        if (string.IsNullOrWhiteSpace(credentialSecretName) || string.IsNullOrWhiteSpace(webhookSecretName))
            return Error.Validation("ProviderAccount.SecretReference", "Secret names are required; secret values are not accepted.");

        var account = new ProviderConfiguration(id, provider, environment, externalAccountId, status,
            credentialSecretName, webhookSecretName, now, null);
        account.AddDomainEvent(new ProviderAccountConfiguredDomainEvent(id, provider.Value, environment, now));
        return account;
    }

    public static ProviderConfiguration Reconstitute(Guid id, ProviderName provider, ProviderEnvironment environment,
        string externalAccountId, ProviderAccountStatus status, string credentialSecretName, string webhookSecretName,
        IEnumerable<ProviderCurrencyRoute> routes, DateTime createdAt, DateTime? updatedAt)
    {
        var account = new ProviderConfiguration(id, provider, environment, externalAccountId, status,
            credentialSecretName, webhookSecretName, createdAt, updatedAt);
        account._routes.AddRange(routes);
        return account;
    }

    public ErrorOr<Success> SetAccountStatus(ProviderAccountStatus status, DateTime now)
    {
        if (Status == status) return Result.Success;
        ExpectedPreviousStatus = Status;
        Status = status;
        UpdatedAt = now;
        AddDomainEvent(new ProviderAccountStatusChangedDomainEvent(Id, status, now));
        return Result.Success;
    }

    public ErrorOr<ProviderCurrencyRoute> ConfigureCurrencyRoute(Guid routeId, string integrationCurrency,
        string settlementCurrency, PresentmentMode presentmentMode, bool enabled, short shortHoldDays,
        decimal reserveRate, short disputeHoldDays, bool requiresDailyReconciliation, bool adapterSupportsRoute,
        DateTime now)
    {
        var route = ProviderCurrencyRoute.Create(routeId, Id, integrationCurrency, settlementCurrency, presentmentMode,
            enabled, shortHoldDays, reserveRate, disputeHoldDays, requiresDailyReconciliation, adapterSupportsRoute);
        if (route.IsError) return route.Errors;
        var existing = _routes.FindIndex(candidate => candidate.Id == routeId
            || candidate.IntegrationCurrency == integrationCurrency);
        if (existing >= 0) _routes[existing] = route.Value;
        else _routes.Add(route.Value);
        UpdatedAt = now;
        AddDomainEvent(new ProviderCurrencyRouteConfiguredDomainEvent(routeId, Id, integrationCurrency, settlementCurrency, now));
        return route.Value;
    }

    public ErrorOr<Success> DisableCurrencyRoute(Guid routeId, DateTime now)
    {
        var index = _routes.FindIndex(route => route.Id == routeId);
        if (index < 0) return Error.NotFound("ProviderCurrencyRoute.NotFound", "Provider Currency Route was not found.");
        if (!_routes[index].IsEnabled) return Result.Success;
        _routes[index] = _routes[index] with { IsEnabled = false };
        UpdatedAt = now;
        AddDomainEvent(new ProviderCurrencyRouteDisabledDomainEvent(routeId, Id, now));
        return Result.Success;
    }
}

public sealed record ProviderCurrencyRoute(Guid Id, Guid ProviderAccountId, string IntegrationCurrency,
    string SettlementCurrency, PresentmentMode PresentmentMode, bool IsEnabled, short ShortHoldDays,
    decimal ReserveRate, short DisputeHoldDays, bool RequiresDailyReconciliation)
{
    public static ErrorOr<ProviderCurrencyRoute> Create(Guid id, Guid providerAccountId, string integrationCurrency,
        string settlementCurrency, PresentmentMode presentmentMode, bool isEnabled, short shortHoldDays,
        decimal reserveRate, short disputeHoldDays, bool requiresDailyReconciliation, bool adapterSupportsRoute)
    {
        if (id == Guid.Empty || providerAccountId == Guid.Empty)
            return Error.Validation("ProviderCurrencyRoute.Identity", "Route and Provider Account ids are required.");
        if (integrationCurrency.Length != 3 || settlementCurrency.Length != 3
            || integrationCurrency != integrationCurrency.ToUpperInvariant()
            || settlementCurrency != settlementCurrency.ToUpperInvariant())
            return Error.Validation("ProviderCurrencyRoute.Currency", "Route currencies must be uppercase ISO codes.");
        if (integrationCurrency != settlementCurrency)
            return Error.Validation("ProviderCurrencyRoute.SettlementCurrency", "Integration and settlement currency must match.");
        if (shortHoldDays < 0 || disputeHoldDays < 0 || reserveRate is < 0 or > 1)
            return Error.Validation("ProviderCurrencyRoute.Risk", "Route risk values are outside the supported range.");
        if (isEnabled && !adapterSupportsRoute)
            return Error.Conflict("ProviderCurrencyRoute.Capability", "An enabled route must be supported by a registered adapter capability.");
        return new ProviderCurrencyRoute(id, providerAccountId, integrationCurrency, settlementCurrency,
            presentmentMode, isEnabled, shortHoldDays, reserveRate, disputeHoldDays, requiresDailyReconciliation);
    }
}

public sealed record ProviderAccountConfiguredDomainEvent(Guid ProviderAccountId, string ProviderName,
    ProviderEnvironment Environment, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}

public sealed record ProviderAccountStatusChangedDomainEvent(Guid ProviderAccountId, ProviderAccountStatus Status,
    DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}

public sealed record ProviderCurrencyRouteConfiguredDomainEvent(Guid RouteId, Guid ProviderAccountId,
    string IntegrationCurrency, string SettlementCurrency, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}

public sealed record ProviderCurrencyRouteDisabledDomainEvent(Guid RouteId, Guid ProviderAccountId,
    DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}
