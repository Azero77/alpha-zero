using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Checkout;
using AlphaZero.Modules.Payments.Domain.Finance;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Modules.Payments.Domain.Purchases;
using AlphaZero.Modules.Payments.IntegrationEvents;
using AlphaZero.Shared.Application;
using ErrorOr;
using MassTransit;
using MediatR;

namespace AlphaZero.Modules.Payments.Application.Purchases;

public interface IPurchaseCaptureRepository
{
    Task<CheckoutSession?> GetCheckoutSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<Price?> GetPriceAsync(Guid priceId, CancellationToken cancellationToken);
    Task<Guid?> GetProviderAccountForRouteAsync(Guid routeId, CancellationToken cancellationToken);
    Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken);
    Task<AcademyPaymentSettingsSnapshot?> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<ProviderTransaction?> FindMovementAsync(Guid providerAccountId, string externalTransactionId, CancellationToken cancellationToken);
    Task AddMovementAsync(ProviderTransaction movement, CancellationToken cancellationToken);
    Task<Purchase?> FindPurchaseByMovementAsync(Guid movementId, CancellationToken cancellationToken);
    Task AddPurchaseAsync(Purchase purchase, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<LedgerAccountRole, Guid>> GetCaptureAccountsAsync(Guid tenantId, Guid providerAccountId,
        string currency, CancellationToken cancellationToken);
    Task<Guid> PostLedgerEntryAsync(LedgerPosting posting, CancellationToken cancellationToken);
    Task<Guid> AddAuditAsync(Guid purchaseId, Guid ledgerEntryId, string action, DateTime occurredAt, CancellationToken cancellationToken);
    Task UpdateCheckoutSessionAsync(CheckoutSession session, CancellationToken cancellationToken);
}

public sealed record AcademyPaymentSettingsSnapshot(decimal CommissionShare);

public interface IPurchaseCaptureUnitOfWork
{
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}

public sealed record CapturePurchase(Guid CheckoutSessionId, Guid ProviderAccountId, string ExternalTransactionId,
    string? ExternalPaymentId, string? ExternalObjectId, decimal SettlementGross, string SettlementCurrency,
    decimal ProviderFee, decimal PresentmentAmount, string PresentmentCurrency, decimal? FxRate,
    string ItemTitle, DateTime OccurredAt, bool VerifiedLatePayment) : ICommand<Purchase>;

/// <summary>Normalized movement capture is atomic across movement, Purchase, Ledger and outbox.</summary>
public sealed class CapturePurchaseHandler(IPurchaseCaptureRepository repository,
    IPurchaseCaptureUnitOfWork unitOfWork, IPublishEndpoint publisher)
    : IRequestHandler<CapturePurchase, ErrorOr<Purchase>>
{
    public async Task<ErrorOr<Purchase>> Handle(CapturePurchase request, CancellationToken cancellationToken)
    {
        var gross = Money.Create(request.SettlementGross, request.SettlementCurrency);
        if (gross.IsError) return gross.Errors;
        var fee = Money.Create(request.ProviderFee, request.SettlementCurrency, allowZero: true);
        if (fee.IsError) return fee.Errors;
        var presentment = Money.Create(request.PresentmentAmount, request.PresentmentCurrency);
        if (presentment.IsError) return presentment.Errors;

        var movement = ProviderTransaction.Record(Guid.NewGuid(), request.ProviderAccountId, null,
            request.ExternalTransactionId, request.ExternalPaymentId, request.ExternalObjectId,
            ProviderTransactionType.Capture, ProviderTransactionDirection.Incoming, gross.Value, fee.Value,
            request.OccurredAt, DateTime.UtcNow);
        if (movement.IsError) return movement.Errors;

        return await unitOfWork.InTransactionAsync<ErrorOr<Purchase>>(async token =>
        {
            var session = await repository.GetCheckoutSessionAsync(request.CheckoutSessionId, token);
            if (session is null) return Error.NotFound("CheckoutSession.NotFound", "Checkout Session was not found.");
            var existingMovement = await repository.FindMovementAsync(request.ProviderAccountId, request.ExternalTransactionId, token);
            if (existingMovement is not null)
            {
                var existingPurchase = await repository.FindPurchaseByMovementAsync(existingMovement.Id, token);
                if (existingPurchase is not null) return existingPurchase;
                return Error.Conflict("Capture.MovementAlreadyObserved", "Provider movement is already recorded without a Purchase; review is required.");
            }
            var expectedProviderAccountId = await repository.GetProviderAccountForRouteAsync(session.ProviderCurrencyRouteId, token);
            if (expectedProviderAccountId is null || expectedProviderAccountId.Value != request.ProviderAccountId)
                return Error.Conflict("Capture.ProviderAccount", "Movement Provider Account does not match the locked route.");
            var price = await repository.GetPriceAsync(session.PriceId, token);
            var product = await repository.GetProductAsync(session.ProductId, token);
            var settings = await repository.GetSettingsAsync(session.TenantId, token);
            if (price is null || product is null || settings is null)
                return Error.Conflict("Capture.SnapshotMissing", "Locked Price, Product, and Academy settings must exist for capture.");

            var complete = session.CompleteFromMovement(request.ProviderAccountId, expectedProviderAccountId.Value,
                gross.Value, price.Amount, request.VerifiedLatePayment, request.OccurredAt);
            if (complete.IsError) return complete.Errors;
            var purchase = Purchase.CreateFromCapture(Guid.NewGuid(), session.TenantId, session.UserId,
                session.ProductId, session.PriceId, session.Id, request.ProviderAccountId, movement.Value,
                request.ItemTitle, presentment.Value, price.Amount, request.FxRate, settings.CommissionShare,
                request.ExternalPaymentId ?? request.ExternalTransactionId, request.OccurredAt);
            if (purchase.IsError) return purchase.Errors;

            await repository.AddMovementAsync(movement.Value, token);
            await repository.AddPurchaseAsync(purchase.Value, token);
            var accounts = await repository.GetCaptureAccountsAsync(session.TenantId, request.ProviderAccountId,
                gross.Value.Currency, token);
            if (new[] { LedgerAccountRole.ProviderAsset, LedgerAccountRole.AcademyPayable,
                    LedgerAccountRole.PlatformRevenue, LedgerAccountRole.ProviderFees }
                .Any(role => !accounts.ContainsKey(role)))
                return Error.Conflict("Capture.StructuralAccountsMissing", "Required structural Ledger accounts are not provisioned.");
            var posting = LedgerPostingFactory.Capture(Guid.NewGuid(), session.TenantId, request.ProviderAccountId,
                movement.Value.Id, purchase.Value.Id, gross.Value, fee.Value, settings.CommissionShare,
                accounts[LedgerAccountRole.ProviderAsset], accounts[LedgerAccountRole.AcademyPayable],
                accounts[LedgerAccountRole.PlatformRevenue], accounts[LedgerAccountRole.ProviderFees], request.OccurredAt);
            if (posting.IsError) return posting.Errors;
            var ledgerEntryId = await repository.PostLedgerEntryAsync(posting.Value, token);
            var auditId = await repository.AddAuditAsync(purchase.Value.Id, ledgerEntryId, "PurchaseCaptured", request.OccurredAt, token);
            await repository.UpdateCheckoutSessionAsync(session, token);
            await publisher.Publish(new PurchaseActivatedIntegrationEvent(Guid.NewGuid(), purchase.Value.TenantId,
                purchase.Value.Id, purchase.Value.UserId, product.TargetResourceArn,
                $$"""{"itemTitle":{{System.Text.Json.JsonSerializer.Serialize(purchase.Value.ItemTitle)}}}""",
                auditId), token);
            return purchase.Value;
        }, cancellationToken);
    }
}
