using System.Data;
using AlphaZero.Modules.Payments.Application.Webhooks;
using AlphaZero.Modules.Payments.Domain.Providers;
using AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence;

public sealed class WebhookStore(AppDbContext context) : IWebhookStore
{
    public async Task<WebhookIngestResult> IngestAsync(Guid eventId, string providerName,
        VerifiedProviderWebhook verified, string payloadJson, DateTime receivedAt,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction?)context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            INSERT INTO "Payments"."WebhookEvents"
                ("Id", "ProviderName", "ProviderAccountId", "ExternalEventId", "EventType",
                 "ExternalObjectId", "SignatureVerified", "Environment", "Livemode", "DeliveryCount", "ReceivedAt")
            VALUES
                (@id, @providerName, @providerAccountId, @externalEventId, @eventType,
                 @externalObjectId, true, @environment, @livemode, 1, @receivedAt)
            ON CONFLICT ("ProviderName", "ExternalEventId") DO UPDATE
                SET "DeliveryCount" = "WebhookEvents"."DeliveryCount" + 1
            RETURNING "Id", (xmax = 0) AS "Inserted";
            """;
        command.Parameters.AddWithValue("id", eventId);
        command.Parameters.AddWithValue("providerName", providerName);
        command.Parameters.Add("providerAccountId", NpgsqlDbType.Uuid).Value =
            (object?)verified.ProviderAccountId ?? DBNull.Value;
        command.Parameters.AddWithValue("externalEventId", verified.ExternalEventId);
        command.Parameters.AddWithValue("eventType", verified.EventType);
        command.Parameters.Add("externalObjectId", NpgsqlDbType.Varchar).Value =
            (object?)verified.ExternalObjectId ?? DBNull.Value;
        command.Parameters.AddWithValue("environment", verified.Environment.ToString());
        command.Parameters.Add("livemode", NpgsqlDbType.Boolean).Value = (object?)verified.Livemode ?? DBNull.Value;
        command.Parameters.AddWithValue("receivedAt", receivedAt);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var persistedEventId = reader.GetGuid(0);
        var inserted = reader.GetBoolean(1);
        await reader.CloseAsync();

        if (inserted)
            await context.WebhookEventPayloads.AddAsync(new WebhookEventPayloadDataModel
            {
                EventId = persistedEventId,
                Payload = payloadJson,
                ArchiveState = "Hot"
            }, cancellationToken);

        return new WebhookIngestResult(persistedEventId, inserted);
    }

    public async Task<StoredProviderWebhook?> GetAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var row = await context.WebhookEvents.AsNoTracking()
            .SingleOrDefaultAsync(webhook => webhook.Id == eventId, cancellationToken);
        if (row is null) return null;
        var payload = await context.WebhookEventPayloads.AsNoTracking()
            .SingleAsync(candidate => candidate.EventId == eventId, cancellationToken);
        return ToStored(row, payload.Payload ?? throw new InvalidOperationException(
            $"Webhook event {eventId} has no hot payload and no archive reader is available in Slice 1."));
    }

    public async Task<StoredProviderWebhook?> LockForProcessingAsync(Guid eventId,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Webhook processing lock requires an ambient transaction.");
        var row = await context.WebhookEvents
            .FromSqlInterpolated($"SELECT * FROM \"Payments\".\"WebhookEvents\" WHERE \"Id\" = {eventId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var payload = await context.WebhookEventPayloads.AsNoTracking()
            .SingleAsync(candidate => candidate.EventId == eventId, cancellationToken);
        return ToStored(row, payload.Payload ?? "null");
    }

    public Task MarkOutcomeAsync(Guid eventId, WebhookProcessingOutcome outcome, DateTime processedAt,
        CancellationToken cancellationToken)
    {
        var row = context.WebhookEvents.Local.Single(webhook => webhook.Id == eventId);
        row.ProcessingOutcome = outcome.ToString();
        row.ProcessedAt = processedAt;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Guid>> FindRedriveCandidatesAsync(DateTime staleBefore, int limit,
        CancellationToken cancellationToken) =>
        await context.WebhookEvents.AsNoTracking()
            .Where(webhook => webhook.ProcessingOutcome == null && webhook.ReceivedAt < staleBefore
                || webhook.ProcessingOutcome == nameof(WebhookProcessingOutcome.Failed)
                && webhook.ProcessedAt < staleBefore)
            .OrderBy(webhook => webhook.ReceivedAt)
            .Select(webhook => webhook.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    private static StoredProviderWebhook ToStored(WebhookEventDataModel row, string payload) => new(
        row.Id, row.ProviderName, row.ProviderAccountId, row.ExternalEventId, row.EventType,
        row.ExternalObjectId, Enum.Parse<ProviderEnvironment>(row.Environment), row.Livemode,
        row.DeliveryCount, row.ReceivedAt, row.ProcessedAt,
        row.ProcessingOutcome is null ? null : Enum.Parse<WebhookProcessingOutcome>(row.ProcessingOutcome), payload);
}
