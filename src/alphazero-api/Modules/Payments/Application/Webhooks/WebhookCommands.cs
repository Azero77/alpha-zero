using System.Text;
using System.Text.Json;
using AlphaZero.Modules.Payments.Application.Purchases;
using AlphaZero.Modules.Payments.Domain.Providers;
using AlphaZero.Shared.Application;
using ErrorOr;
using MassTransit;
using MediatR;

namespace AlphaZero.Modules.Payments.Application.Webhooks;

public enum WebhookProcessingOutcome { Processed, Duplicate, Failed, Skipped }

public sealed record VerifiedProviderWebhook(
    Guid? ProviderAccountId,
    string ExternalEventId,
    string EventType,
    string? ExternalObjectId,
    ProviderEnvironment Environment,
    bool? Livemode);

public sealed record StoredProviderWebhook(
    Guid EventId,
    string ProviderName,
    Guid? ProviderAccountId,
    string ExternalEventId,
    string EventType,
    string? ExternalObjectId,
    ProviderEnvironment Environment,
    bool? Livemode,
    int DeliveryCount,
    DateTime ReceivedAt,
    DateTime? ProcessedAt,
    WebhookProcessingOutcome? ProcessingOutcome,
    string PayloadJson);

public sealed record NormalizedCapture(
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
    bool VerifiedLatePayment);

public interface IProviderEventNormalizer
{
    string ProviderName { get; }
    Task<ErrorOr<VerifiedProviderWebhook>> VerifyAsync(IReadOnlyDictionary<string, string> headers,
        ReadOnlyMemory<byte> rawBody, CancellationToken cancellationToken);
    Task<ErrorOr<NormalizedCapture?>> NormalizeCaptureAsync(StoredProviderWebhook webhook,
        CancellationToken cancellationToken);
}

public interface IProviderEventNormalizerRegistry
{
    ErrorOr<IProviderEventNormalizer> Resolve(string providerName);
}

public sealed record WebhookIngestResult(Guid EventId, bool IsNew);

public interface IWebhookStore
{
    Task<WebhookIngestResult> IngestAsync(Guid eventId, string providerName, VerifiedProviderWebhook verified,
        string payloadJson, DateTime receivedAt, CancellationToken cancellationToken);
    Task<StoredProviderWebhook?> GetAsync(Guid eventId, CancellationToken cancellationToken);
    Task<StoredProviderWebhook?> LockForProcessingAsync(Guid eventId, CancellationToken cancellationToken);
    Task MarkOutcomeAsync(Guid eventId, WebhookProcessingOutcome outcome, DateTime processedAt,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> FindRedriveCandidatesAsync(DateTime staleBefore, int limit,
        CancellationToken cancellationToken);
}

public sealed record ReceiveProviderWebhook(string ProviderName, IReadOnlyDictionary<string, string> Headers,
    byte[] RawBody) : ICommand<WebhookIngestResult>;

/// <summary>Signature verification runs before the transactional ingest phase and before any write.</summary>
public sealed class ReceiveProviderWebhookHandler(IProviderEventNormalizerRegistry normalizers, ISender sender)
    : IRequestHandler<ReceiveProviderWebhook, ErrorOr<WebhookIngestResult>>
{
    public async Task<ErrorOr<WebhookIngestResult>> Handle(ReceiveProviderWebhook request,
        CancellationToken cancellationToken)
    {
        var normalizer = normalizers.Resolve(request.ProviderName);
        if (normalizer.IsError) return normalizer.Errors;

        var verified = await normalizer.Value.VerifyAsync(request.Headers, request.RawBody, cancellationToken);
        if (verified.IsError) return verified.Errors;
        if (!IsValid(verified.Value))
            return Error.Validation("Webhook.Envelope", "Verified webhook identity fields are invalid.");

        string payloadJson;
        try
        {
            using var _ = JsonDocument.Parse(request.RawBody);
            payloadJson = Encoding.UTF8.GetString(request.RawBody);
        }
        catch (JsonException)
        {
            return Error.Validation("Webhook.Payload", "Webhook payload must be valid UTF-8 JSON.");
        }

        return await sender.Send(new IngestVerifiedProviderWebhook(Guid.CreateVersion7(), request.ProviderName,
            verified.Value, payloadJson, DateTime.UtcNow), cancellationToken);
    }

    private static bool IsValid(VerifiedProviderWebhook webhook) =>
        webhook.ProviderAccountId is { } accountId && accountId != Guid.Empty
        && !string.IsNullOrWhiteSpace(webhook.ExternalEventId) && webhook.ExternalEventId.Length <= 255
        && !string.IsNullOrWhiteSpace(webhook.EventType) && webhook.EventType.Length <= 64
        && (webhook.ExternalObjectId is null || webhook.ExternalObjectId.Length <= 255);
}

public sealed record IngestVerifiedProviderWebhook(Guid EventId, string ProviderName,
    VerifiedProviderWebhook Verified, string PayloadJson, DateTime ReceivedAt)
    : ICommand<WebhookIngestResult>, ITransactionalCommand;

public sealed class IngestVerifiedProviderWebhookHandler(IWebhookStore store, IPublishEndpoint publisher)
    : IRequestHandler<IngestVerifiedProviderWebhook, ErrorOr<WebhookIngestResult>>
{
    public async Task<ErrorOr<WebhookIngestResult>> Handle(IngestVerifiedProviderWebhook request,
        CancellationToken cancellationToken)
    {
        var result = await store.IngestAsync(request.EventId, request.ProviderName, request.Verified,
            request.PayloadJson, request.ReceivedAt, cancellationToken);
        if (result.IsNew)
            await publisher.Publish(new ProcessWebhookEventRequested(result.EventId), cancellationToken);
        return result;
    }
}

/// <summary>Internal durable handoff written through the Payments EF outbox.</summary>
public sealed record ProcessWebhookEventRequested(Guid EventId);

public sealed record WebhookProcessingResult(WebhookProcessingOutcome Outcome, Guid? PurchaseId = null);

public sealed record ProcessWebhookEvent(Guid EventId) : ICommand<WebhookProcessingResult>;

public sealed class ProcessWebhookEventHandler(IWebhookStore store, IProviderEventNormalizerRegistry normalizers,
    ISender sender) : IRequestHandler<ProcessWebhookEvent, ErrorOr<WebhookProcessingResult>>
{
    public async Task<ErrorOr<WebhookProcessingResult>> Handle(ProcessWebhookEvent request,
        CancellationToken cancellationToken)
    {
        var webhook = await store.GetAsync(request.EventId, cancellationToken);
        if (webhook is null) return Error.NotFound("Webhook.NotFound", "Webhook event was not found.");
        if (webhook.ProcessingOutcome is WebhookProcessingOutcome.Processed
            or WebhookProcessingOutcome.Duplicate or WebhookProcessingOutcome.Skipped)
            return new WebhookProcessingResult(webhook.ProcessingOutcome.Value);

        var normalizer = normalizers.Resolve(webhook.ProviderName);
        if (normalizer.IsError) return await FailAsync(request.EventId, normalizer.Errors, cancellationToken);
        var normalized = await normalizer.Value.NormalizeCaptureAsync(webhook, cancellationToken);
        if (normalized.IsError) return await FailAsync(request.EventId, normalized.Errors, cancellationToken);
        if (normalized.Value is null)
        {
            await sender.Send(new MarkWebhookOutcome(request.EventId, WebhookProcessingOutcome.Skipped),
                cancellationToken);
            return new WebhookProcessingResult(WebhookProcessingOutcome.Skipped);
        }
        if (webhook.ProviderAccountId is { } accountId && accountId != normalized.Value.ProviderAccountId)
            return await FailAsync(request.EventId,
                [Error.Conflict("Webhook.ProviderAccount", "Normalized movement does not match the webhook Provider Account.")],
                cancellationToken);

        var capture = normalized.Value;
        var result = await sender.Send(new CapturePurchase(capture.CheckoutSessionId, capture.ProviderAccountId,
            capture.ExternalTransactionId, capture.ExternalPaymentId, capture.ExternalObjectId,
            capture.SettlementGross, capture.SettlementCurrency, capture.ProviderFee, capture.PresentmentAmount,
            capture.PresentmentCurrency, capture.FxRate, capture.OccurredAt, capture.VerifiedLatePayment,
            request.EventId), cancellationToken);
        if (result.IsError)
        {
            if (result.FirstError.Code == "Webhook.AlreadyProcessed")
                return new WebhookProcessingResult(WebhookProcessingOutcome.Duplicate);
            return await FailAsync(request.EventId, result.Errors, cancellationToken);
        }
        return new WebhookProcessingResult(result.Value.IsDuplicate
            ? WebhookProcessingOutcome.Duplicate
            : WebhookProcessingOutcome.Processed, result.Value.Purchase.Id);
    }

    private async Task<ErrorOr<WebhookProcessingResult>> FailAsync(Guid eventId, List<Error> errors,
        CancellationToken cancellationToken)
    {
        await sender.Send(new MarkWebhookOutcome(eventId, WebhookProcessingOutcome.Failed), cancellationToken);
        return errors;
    }
}

public sealed record MarkWebhookOutcome(Guid EventId, WebhookProcessingOutcome Outcome)
    : ICommand<Success>, ITransactionalCommand;

public sealed class MarkWebhookOutcomeHandler(IWebhookStore store)
    : IRequestHandler<MarkWebhookOutcome, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(MarkWebhookOutcome request, CancellationToken cancellationToken)
    {
        if (await store.LockForProcessingAsync(request.EventId, cancellationToken) is null)
            return Error.NotFound("Webhook.NotFound", "Webhook event was not found.");
        await store.MarkOutcomeAsync(request.EventId, request.Outcome, DateTime.UtcNow, cancellationToken);
        return Result.Success;
    }
}

public sealed record RedriveWebhookEvent(Guid EventId) : ICommand<bool>, ITransactionalCommand;

public sealed class RedriveWebhookEventHandler(IWebhookStore store, IPublishEndpoint publisher)
    : IRequestHandler<RedriveWebhookEvent, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(RedriveWebhookEvent request, CancellationToken cancellationToken)
    {
        var webhook = await store.LockForProcessingAsync(request.EventId, cancellationToken);
        if (webhook is null) return Error.NotFound("Webhook.NotFound", "Webhook event was not found.");
        if (webhook.ProcessingOutcome is WebhookProcessingOutcome.Processed
            or WebhookProcessingOutcome.Duplicate or WebhookProcessingOutcome.Skipped)
            return false;
        await publisher.Publish(new ProcessWebhookEventRequested(request.EventId), cancellationToken);
        return true;
    }
}

public sealed record RedriveStaleWebhookEvents(DateTime StaleBefore, int Limit = 100)
    : ICommand<int>, ITransactionalCommand;

public sealed class RedriveStaleWebhookEventsHandler(IWebhookStore store, IPublishEndpoint publisher)
    : IRequestHandler<RedriveStaleWebhookEvents, ErrorOr<int>>
{
    public async Task<ErrorOr<int>> Handle(RedriveStaleWebhookEvents request, CancellationToken cancellationToken)
    {
        if (request.Limit is < 1 or > 500)
            return Error.Validation("Webhook.RedriveLimit", "Redrive limit must be from 1 to 500.");
        var eventIds = await store.FindRedriveCandidatesAsync(request.StaleBefore, request.Limit,
            cancellationToken);
        foreach (var eventId in eventIds)
            await publisher.Publish(new ProcessWebhookEventRequested(eventId), cancellationToken);
        return eventIds.Count;
    }
}
