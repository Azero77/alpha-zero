using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Providers;
using AlphaZero.Modules.Payments.Domain.Settings;
using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Checkout;

public sealed record PaymentMethod(Guid ProviderCurrencyRouteId, string ProviderName, string IntegrationCurrency,
    string SettlementCurrency, PresentmentMode PresentmentMode);

public static class CheckoutEligibilityPolicy
{
    public static ErrorOr<IReadOnlyList<PaymentMethod>> Decide(Product product, Price price,
        AcademyPaymentSettings? settings, IEnumerable<ProviderConfiguration> configurations, bool hasActiveEnrollment)
    {
        if (product.SourceAvailability != SourceAvailability.Available)
            return Error.Conflict("Checkout.ProductUnavailable", "The source Course Plan is unavailable.");
        if (!price.IsActive || price.ProductId != product.Id || price.TenantId != product.TenantId)
            return Error.Conflict("Checkout.PriceUnavailable", "An active compatible Price is required.");
        if (settings is null || !settings.OnlineSellingEnabled)
            return Error.Conflict("Checkout.AcademyDisabled", "Online selling is not enabled for this Academy.");
        if (hasActiveEnrollment)
            return Error.Conflict("Checkout.AlreadyEnrolled", "Student already has active access to this Product.");

        var methods = configurations
            .Where(account => account.Status == ProviderAccountStatus.Active)
            .SelectMany(account => account.Routes
                .Where(route => route.IsEnabled && route.IntegrationCurrency == price.Amount.Currency)
                .Select(route => new PaymentMethod(route.Id, account.Provider.Value, route.IntegrationCurrency,
                    route.SettlementCurrency, route.PresentmentMode)))
            .ToArray();
        return methods.Length == 0
            ? Error.Conflict("Checkout.NoCompatibleRoute", "No active Provider Currency Route accepts this Price currency.")
            : methods;
    }
}
