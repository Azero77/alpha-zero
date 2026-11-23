using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Checkout;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Modules.Payments.Domain.Providers;
using AlphaZero.Modules.Payments.Domain.Settings;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Payments.Application.Checkout;

public interface ICheckoutRepository
{
    Task<Product?> GetProductAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken);
    Task<Price?> GetActivePriceAsync(Guid tenantId, Guid productId, Guid priceId, CancellationToken cancellationToken);
    Task<AcademyPaymentSettings?> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderConfiguration>> GetProviderConfigurationsAsync(CancellationToken cancellationToken);
    Task<CheckoutSession?> GetPendingSessionAsync(Guid userId, Guid productId, CancellationToken cancellationToken);
    Task AddSessionAsync(CheckoutSession session, CancellationToken cancellationToken);
    Task UpdateSessionAsync(CheckoutSession session, CancellationToken cancellationToken);
}

public interface ICourseAccessReader
{
    Task<ErrorOr<bool>> HasActiveEnrollmentAsync(Guid tenantId, Guid userId, string targetResourceArn,
        CancellationToken cancellationToken);
}

public interface ICheckoutProvider
{
    /// <summary>CheckoutSessionId is the deterministic provider idempotency root.</summary>
    Task<ErrorOr<string>> CreateHostedCheckoutAsync(Guid providerCurrencyRouteId, Guid checkoutSessionId,
        Money lockedPrice, CancellationToken cancellationToken);
}

public sealed record CreateCheckoutSession(Guid TenantId, Guid UserId, Guid ProductId, Guid PriceId,
    Guid ProviderCurrencyRouteId) : ICommand<CheckoutSession>;

public sealed record CheckoutEligibilityResult(IReadOnlyList<PaymentMethod> Methods, bool HasActiveEnrollment);

public sealed record GetAvailableCheckoutMethods(Guid TenantId, Guid UserId, Guid ProductId, Guid PriceId)
    : IQuery<CheckoutEligibilityResult>;

public sealed class GetAvailableCheckoutMethodsHandler(ICheckoutRepository repository, ICourseAccessReader courseAccess)
    : IRequestHandler<GetAvailableCheckoutMethods, ErrorOr<CheckoutEligibilityResult>>
{
    public async Task<ErrorOr<CheckoutEligibilityResult>> Handle(GetAvailableCheckoutMethods request,
        CancellationToken cancellationToken)
    {
        var product = await repository.GetProductAsync(request.TenantId, request.ProductId, cancellationToken);
        if (product is null) return Error.NotFound("Product.NotFound", "Product was not found.");
        var price = await repository.GetActivePriceAsync(request.TenantId, request.ProductId, request.PriceId,
            cancellationToken);
        if (price is null)
            return Error.Conflict("Checkout.PriceUnavailable", "An active compatible Price is required.");
        var enrollment = await courseAccess.HasActiveEnrollmentAsync(request.TenantId, request.UserId,
            product.TargetResourceArn, cancellationToken);
        if (enrollment.IsError) return enrollment.Errors;

        var methods = CheckoutEligibilityPolicy.Decide(product, price,
            await repository.GetSettingsAsync(request.TenantId, cancellationToken),
            await repository.GetProviderConfigurationsAsync(cancellationToken), enrollment.Value);
        return methods.IsError ? methods.Errors : new CheckoutEligibilityResult(methods.Value, enrollment.Value);
    }
}

/// <summary>Public provider orchestrator. The durable intent phase commits before provider I/O.</summary>
public sealed class CreateCheckoutSessionHandler(ISender sender, ICheckoutProvider provider)
    : IRequestHandler<CreateCheckoutSession, ErrorOr<CheckoutSession>>
{
    public async Task<ErrorOr<CheckoutSession>> Handle(CreateCheckoutSession request, CancellationToken cancellationToken)
    {
        var eligibility = await sender.Send(new GetAvailableCheckoutMethods(request.TenantId, request.UserId,
            request.ProductId, request.PriceId), cancellationToken);
        if (eligibility.IsError) return eligibility.Errors;
        if (!eligibility.Value.Methods.Any(method => method.ProviderCurrencyRouteId == request.ProviderCurrencyRouteId))
            return Error.Conflict("Checkout.RouteUnavailable", "Selected route is not an eligible Payment Method.");

        var intent = await sender.Send(new CreateCheckoutIntent(request.TenantId, request.UserId,
            request.ProductId, request.PriceId, request.ProviderCurrencyRouteId, eligibility.Value.Methods), cancellationToken);
        if (intent.IsError) return intent.Errors;

        var external = await provider.CreateHostedCheckoutAsync(intent.Value.Session.ProviderCurrencyRouteId,
            intent.Value.Session.Id, intent.Value.LockedPrice, cancellationToken);
        if (external.IsError) return external.Errors; // Ambiguous outcomes retain the committed Pending intent.

        var stamped = await sender.Send(new StampProviderCheckoutSession(intent.Value.Session.Id, external.Value), cancellationToken);
        if (stamped.IsError) return stamped.Errors;
        return stamped.Value;
    }
}

public sealed record CheckoutIntent(CheckoutSession Session, Money LockedPrice);

/// <summary>Internal transactional phase; origins must call CreateCheckoutSession instead.</summary>
public sealed record CreateCheckoutIntent(Guid TenantId, Guid UserId, Guid ProductId, Guid PriceId,
    Guid ProviderCurrencyRouteId, IReadOnlyList<PaymentMethod> EligibleMethods)
    : ICommand<CheckoutIntent>, ITransactionalCommand;

public sealed class CreateCheckoutIntentHandler(ICheckoutRepository repository, IClock clock)
    : IRequestHandler<CreateCheckoutIntent, ErrorOr<CheckoutIntent>>
{
    public async Task<ErrorOr<CheckoutIntent>> Handle(CreateCheckoutIntent request, CancellationToken cancellationToken)
    {
        var price = await repository.GetActivePriceAsync(request.TenantId, request.ProductId, request.PriceId, cancellationToken);
        if (price is null) return Error.Conflict("Checkout.PriceUnavailable", "An active compatible Price is required.");
        if (!request.EligibleMethods.Any(method => method.ProviderCurrencyRouteId == request.ProviderCurrencyRouteId))
            return Error.Conflict("Checkout.RouteUnavailable", "Selected route is not an eligible Payment Method.");

        var pending = await repository.GetPendingSessionAsync(request.UserId, request.ProductId, cancellationToken);
        if (pending is not null)
        {
            if (pending.TenantId != request.TenantId || pending.PriceId != request.PriceId
                || pending.ProviderCurrencyRouteId != request.ProviderCurrencyRouteId)
                return Error.Conflict("Checkout.PendingIntent", "An existing Pending Checkout Session has locked a different Price or route.");
            return new CheckoutIntent(pending, price.Amount);
        }

        var created = CheckoutSession.Create(Guid.NewGuid(), request.TenantId, request.UserId, request.ProductId,
            request.PriceId, request.ProviderCurrencyRouteId, clock.Now);
        if (created.IsError) return created.Errors;
        await repository.AddSessionAsync(created.Value, cancellationToken);
        return new CheckoutIntent(created.Value, price.Amount);
    }
}

/// <summary>Internal idempotent result phase, committed independently after provider I/O.</summary>
public sealed record StampProviderCheckoutSession(Guid SessionId, string ExternalCheckoutId)
    : ICommand<CheckoutSession>, ITransactionalCommand;

public sealed class StampProviderCheckoutSessionHandler(ICheckoutSessionLifecycleRepository repository)
    : IRequestHandler<StampProviderCheckoutSession, ErrorOr<CheckoutSession>>
{
    public async Task<ErrorOr<CheckoutSession>> Handle(StampProviderCheckoutSession request, CancellationToken cancellationToken)
    {
        var session = await repository.GetSessionAsync(request.SessionId, cancellationToken);
        if (session is null) return Error.NotFound("CheckoutSession.NotFound", "Checkout Session was not found.");
        var stamped = session.StampProviderSession(request.ExternalCheckoutId);
        if (stamped.IsError) return stamped.Errors;
        await repository.UpdateAsync(session, cancellationToken);
        return session;
    }
}

public sealed record ExpireCheckoutSession(Guid SessionId) : ICommand<Success>, ITransactionalCommand;

public interface ICheckoutSessionLifecycleRepository
{
    Task<CheckoutSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    Task UpdateAsync(CheckoutSession session, CancellationToken cancellationToken);
}

public sealed class ExpireCheckoutSessionHandler(ICheckoutSessionLifecycleRepository repository, IClock clock)
    : IRequestHandler<ExpireCheckoutSession, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ExpireCheckoutSession request, CancellationToken cancellationToken)
    {
        var session = await repository.GetSessionAsync(request.SessionId, cancellationToken);
        if (session is null) return Error.NotFound("CheckoutSession.NotFound", "Checkout Session was not found.");
        var expired = session.Expire(clock.Now);
        if (expired.IsError) return expired.Errors;
        await repository.UpdateAsync(session, cancellationToken);
        return Result.Success;
    }
}
