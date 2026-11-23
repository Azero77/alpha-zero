using AlphaZero.Modules.Payments.Application.Ledger;
using AlphaZero.Modules.Payments.Application.Webhooks;
using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Checkout;
using AlphaZero.Modules.Payments.Domain.Finance;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Modules.Payments.Domain.Purchases;
using AlphaZero.Modules.Payments.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
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
    Task LockMovementIdentityAsync(Guid providerAccountId, string externalTransactionId, CancellationToken cancellationToken);
    Task AddMovementAsync(ProviderTransaction movement, CancellationToken cancellationToken);
    Task<Purchase?> FindPurchaseByMovementAsync(Guid movementId, CancellationToken cancellationToken);
    Task AddPurchaseAsync(Purchase purchase, CancellationToken cancellationToken);
    Task FlushPendingRowsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<LedgerAccountRole, Guid>> GetCaptureAccountsAsync(Guid tenantId, Guid providerAccountId,
        string currency, CancellationToken cancellationToken);
    Task<Guid> AddAuditAsync(Guid tenantId, Guid purchaseId, Guid ledgerEntryId, string action, DateTime occurredAt,
        CancellationToken cancellationToken);
    Task UpdateCheckoutSessionAsync(CheckoutSession session, CancellationToken cancellationToken);
}

public sealed record AcademyPaymentSettingsSnapshot(decimal CommissionShare);

public sealed record CapturePurchase(
    Guid CheckoutSessionId,
    Guid ProviderAccountId,
    string ExternalTransactionId,
    string? ExternalPaymentId,
    string? ExternalObjectId,
    decimal SettlementGross,
    string SettlementCurrency,
    decimal ProviderFee,
    decimal PresentmentAmount,
    string PresentmentCurrency,
    decimal? FxRate,
    DateTime OccurredAt,
    bool VerifiedLatePayment,
    Guid? WebhookEventId = null) : ICommand<CapturePurchaseResult>;

internal sealed record PersistPurchaseCapture(
    CapturePurchase Request,
    ProviderTransaction Movement,
    Money Gross,
    Money Fee,
    Money Presentment) : ICommand<CapturePurchaseResult>, ITransactionalCommand;

public sealed record CapturePurchaseResult(Purchase Purchase, bool IsDuplicate);

public sealed class CapturePurchaseHandler(ISender sender, IClock clock)
    : IRequestHandler<CapturePurchase, ErrorOr<CapturePurchaseResult>>
{
    public async Task<ErrorOr<CapturePurchaseResult>> Handle(CapturePurchase request,
        CancellationToken cancellationToken)
    {
        var gross = Money.Create(request.SettlementGross, request.SettlementCurrency);
        if (gross.IsError) return gross.Errors;
        var fee = Money.Create(request.ProviderFee, request.SettlementCurrency, allowZero: true);
        if (fee.IsError) return fee.Errors;
        var presentment = Money.Create(request.PresentmentAmount, request.PresentmentCurrency);
        if (presentment.IsError) return presentment.Errors;

        var movement = ProviderTransaction.Record(Guid.CreateVersion7(), request.ProviderAccountId,
            request.WebhookEventId,
            request.ExternalTransactionId, request.ExternalPaymentId, request.ExternalObjectId,
            ProviderTransactionType.Capture, ProviderTransactionDirection.Incoming, gross.Value, fee.Value,
            request.OccurredAt, clock.Now);
        if (movement.IsError) return movement.Errors;

        return await sender.Send(new PersistPurchaseCapture(request, movement.Value, gross.Value, fee.Value, presentment.Value),
            cancellationToken);
    }
}

internal sealed class PersistPurchaseCaptureHandler(
    IPurchaseCaptureRepository repository,
    ILedgerWriter ledgerWriter,
    IWebhookStore webhooks,
    IPublishEndpoint publisher,
    IClock clock)
    : IRequestHandler<PersistPurchaseCapture, ErrorOr<CapturePurchaseResult>>
{
    public async Task<ErrorOr<CapturePurchaseResult>> Handle(PersistPurchaseCapture request,
        CancellationToken cancellationToken)
    {
        if (request.Request.WebhookEventId is { } webhookEventId)
        {
            var webhook = await webhooks.LockForProcessingAsync(webhookEventId, cancellationToken);
            if (webhook is null) return Error.NotFound("Webhook.NotFound", "Webhook event was not found.");
            if (webhook.ProcessingOutcome is WebhookProcessingOutcome.Processed
                or WebhookProcessingOutcome.Duplicate or WebhookProcessingOutcome.Skipped)
            {
                var terminalMovement = await repository.FindMovementAsync(request.Request.ProviderAccountId,
                    request.Request.ExternalTransactionId, cancellationToken);
                var terminalPurchase = terminalMovement is null ? null
                    : await repository.FindPurchaseByMovementAsync(terminalMovement.Id, cancellationToken);
                return terminalPurchase is null
                    ? Error.Conflict("Webhook.AlreadyProcessed", "Webhook event is already terminal.")
                    : new CapturePurchaseResult(terminalPurchase, true);
            }
        }

        await repository.LockMovementIdentityAsync(request.Request.ProviderAccountId, request.Request.ExternalTransactionId, cancellationToken);
        var existingMovement = await repository.FindMovementAsync(request.Request.ProviderAccountId, request.Request.ExternalTransactionId, cancellationToken);
        if (existingMovement is not null)
        {
            if (!MovementMatches(existingMovement, request.Movement))
                return Error.Conflict("Capture.MovementDisagrees",
                    "A Provider Transaction with this identity has different immutable money facts.");
            var existingPurchase = await repository.FindPurchaseByMovementAsync(existingMovement.Id, cancellationToken);
            if (existingPurchase is not null)
            {
                if (request.Request.WebhookEventId is { } duplicateEventId)
                    await webhooks.MarkOutcomeAsync(duplicateEventId, WebhookProcessingOutcome.Duplicate,
                        clock.Now, cancellationToken);
                return new CapturePurchaseResult(existingPurchase, true);
            }
            return Error.Conflict("Capture.MovementAlreadyObserved", "Provider movement is already recorded without a Purchase; review is required.");
        }
        var session = await repository.GetCheckoutSessionAsync(request.Request.CheckoutSessionId, cancellationToken);
        if (session is null) return Error.NotFound("CheckoutSession.NotFound", "Checkout Session was not found.");
        if (session.Status == CheckoutSessionStatus.Completed)
            return Error.Conflict("Capture.SessionAlreadyCompleted",
                "A different movement cannot fund an already completed Checkout Session.");

        var expectedProviderAccountId = await repository.GetProviderAccountForRouteAsync(session.ProviderCurrencyRouteId, cancellationToken);
        var price = await repository.GetPriceAsync(session.PriceId, cancellationToken);
        var product = await repository.GetProductAsync(session.ProductId, cancellationToken);
        var settings = await repository.GetSettingsAsync(session.TenantId, cancellationToken);
        if (price is null || product is null || settings is null)
            return Error.Conflict("Capture.SnapshotMissing", "Locked Price, Product, and Academy settings must exist for capture.");

        var complete = session.CompleteFromMovement(request.Request.ProviderAccountId, expectedProviderAccountId ?? Guid.Empty,
            request.Gross, price.Amount, request.Request.VerifiedLatePayment, request.Request.OccurredAt);
        if (complete.IsError) return complete.Errors;
        var purchase = Purchase.CreateFromCapture(Guid.NewGuid(), session.TenantId, session.UserId,
            session.ProductId, session.PriceId, session.Id, request.Request.ProviderAccountId, request.Movement,
            product.Title, request.Presentment, price.Amount, request.Request.FxRate, settings.CommissionShare,
            request.Request.ExternalPaymentId ?? request.Request.ExternalTransactionId, request.Request.OccurredAt);
        if (purchase.IsError) return purchase.Errors;

        await repository.AddMovementAsync(request.Movement, cancellationToken);
        await repository.AddPurchaseAsync(purchase.Value, cancellationToken);
        // The hardened SQL function validates typed FKs immediately; expose these rows inside
        // the still-open transaction before posting, without committing any partial state.
        await repository.FlushPendingRowsAsync(cancellationToken);
        var accounts = await repository.GetCaptureAccountsAsync(session.TenantId, request.Request.ProviderAccountId,
            request.Gross.Currency, cancellationToken);
        if (new[] { LedgerAccountRole.ProviderAsset, LedgerAccountRole.AcademyPayable,
                LedgerAccountRole.PlatformRevenue, LedgerAccountRole.ProviderFees }
            .Any(role => !accounts.ContainsKey(role)))
            return Error.Conflict("Capture.StructuralAccountsMissing", "Required structural Ledger accounts are not provisioned.");
        var posting = LedgerPostingFactory.Capture(Guid.CreateVersion7(), session.TenantId, request.Request.ProviderAccountId,
            request.Movement.Id, purchase.Value.Id, request.Gross, request.Fee, settings.CommissionShare,
            accounts[LedgerAccountRole.ProviderAsset], accounts[LedgerAccountRole.AcademyPayable],
            accounts[LedgerAccountRole.PlatformRevenue], accounts[LedgerAccountRole.ProviderFees], request.Request.OccurredAt);
        if (posting.IsError) return posting.Errors;

        var ledgerEntryId = await ledgerWriter.PostAsync(posting.Value, cancellationToken);
        await repository.AddAuditAsync(purchase.Value.TenantId, purchase.Value.Id, ledgerEntryId,
            "PurchaseCaptured", request.Request.OccurredAt, cancellationToken);
        await repository.UpdateCheckoutSessionAsync(session, cancellationToken);

        var metadata = new PurchaseActivationMetadata(product.ItemId, product.ItemType, purchase.Value.ItemTitle);
        await publisher.Publish(new PurchaseActivatedIntegrationEvent(
            Guid.NewGuid(),
            purchase.Value.TenantId,
            purchase.Value.Id,
            purchase.Value.UserId,
            product.TargetResourceArn,
            metadata,
            ledgerEntryId), cancellationToken);

        if (request.Request.WebhookEventId is { } processedEventId)
            await webhooks.MarkOutcomeAsync(processedEventId, WebhookProcessingOutcome.Processed,
                clock.Now, cancellationToken);
        return new CapturePurchaseResult(purchase.Value, false);
    }

    private static bool MovementMatches(ProviderTransaction existing, ProviderTransaction incoming) =>
        existing.ProviderAccountId == incoming.ProviderAccountId
        && existing.ExternalTransactionId == incoming.ExternalTransactionId
        && existing.ExternalPaymentId == incoming.ExternalPaymentId
        && existing.ExternalObjectId == incoming.ExternalObjectId
        && existing.Type == incoming.Type
        && existing.Direction == incoming.Direction
        && existing.Amount == incoming.Amount
        && existing.Fee == incoming.Fee
        && Math.Abs((existing.OccurredAt - incoming.OccurredAt).Ticks) < TimeSpan.TicksPerMicrosecond;
}
