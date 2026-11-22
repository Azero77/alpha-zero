using System.Text.Json;
using AlphaZero.Modules.Payments.Application.Catalog;
using AlphaZero.Modules.Payments.Application.Queries;
using AlphaZero.Modules.Payments.Application.Webhooks;
using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Providers;
using AlphaZero.Modules.Payments.Infrastructure;
using AlphaZero.Modules.Payments.Infrastructure.Persistence;
using AlphaZero.Modules.Payments.Infrastructure.Persistence.Migrations;
using ErrorOr;
using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Payments.Tests.Integration;

public sealed class PaymentsWorkflowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _postgres = BuildContainerIfNeeded();
    private readonly WorkflowSeed _seed = new();
    private string ConnectionString => Environment.GetEnvironmentVariable("PAYMENTS_TEST_CONNECTION_STRING")
                                       ?? _postgres!.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (_postgres is not null)
            await _postgres.StartAsync();
        else
        {
            await using var connection = await OpenAsync();
            await ExecuteAsync(connection, "DROP SCHEMA IF EXISTS \"Payments\" CASCADE;");
        }
        await PaymentsSqlMigrationRunner.ApplyAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Webhook_ingest_verifies_before_write_and_commits_one_durable_processing_message()
    {
        await SeedCaptureAsync(includeLedgerAccounts: true);
        await using var services = BuildServices();

        var invalid = await SendAsync(services, new ReceiveProviderWebhook("stripe",
            new Dictionary<string, string> { ["signature"] = "invalid" }, Payload("event-invalid")));
        invalid.IsError.Should().BeTrue();

        var first = await SendAsync(services, new ReceiveProviderWebhook("stripe",
            ValidHeaders(), Payload("event-1")));
        var duplicate = await SendAsync(services, new ReceiveProviderWebhook("stripe",
            ValidHeaders(), Payload("event-1")));

        first.IsError.Should().BeFalse();
        first.Value.IsNew.Should().BeTrue();
        duplicate.IsError.Should().BeFalse();
        duplicate.Value.Should().Be(new WebhookIngestResult(first.Value.EventId, false));

        await using var connection = await OpenAsync();
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"WebhookEvents\";"))
            .Should().Be(1);
        (await ScalarAsync<int>(connection,
            "SELECT \"DeliveryCount\" FROM \"Payments\".\"WebhookEvents\" WHERE \"Id\"=@id;",
            ("id", first.Value.EventId))).Should().Be(2);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"WebhookEventPayloads\";"))
            .Should().Be(1);
        (await OutboxCountAsync(connection, nameof(ProcessWebhookEventRequested))).Should().Be(1);
    }

    [Fact]
    public async Task Processing_after_restart_commits_capture_activation_and_replays_as_no_op()
    {
        await SeedCaptureAsync(includeLedgerAccounts: true);
        Guid eventId;
        await using (var ingestServices = BuildServices())
        {
            var ingested = await SendAsync(ingestServices, new ReceiveProviderWebhook("stripe", ValidHeaders(),
                Payload("event-1")));
            eventId = ingested.Value.EventId;
        }

        await using var processingServices = BuildServices();
        var processed = await SendAsync(processingServices, new ProcessWebhookEvent(eventId));
        var replay = await SendAsync(processingServices, new ProcessWebhookEvent(eventId));
        processed.IsError.Should().BeFalse();
        processed.Value.Outcome.Should().Be(WebhookProcessingOutcome.Processed);
        replay.IsError.Should().BeFalse();
        replay.Value.Outcome.Should().Be(WebhookProcessingOutcome.Processed);

        var secondDelivery = await SendAsync(processingServices, new ReceiveProviderWebhook("stripe", ValidHeaders(),
            Payload("event-2")));
        var duplicateMovement = await SendAsync(processingServices,
            new ProcessWebhookEvent(secondDelivery.Value.EventId));
        duplicateMovement.IsError.Should().BeFalse();
        duplicateMovement.Value.Outcome.Should().Be(WebhookProcessingOutcome.Duplicate);

        var purchases = await SendAsync(processingServices,
            new ListStudentPurchases(_seed.TenantId, _seed.UserId));
        purchases.Value.Items.Should().ContainSingle().Which.ItemTitle.Should().Be("Stored Course Title");

        await using var connection = await OpenAsync();
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"ProviderTransactions\";"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"Purchases\";"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"LedgerEntries\" WHERE \"Type\"='Capture';"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"LedgerLines\";"))
            .Should().Be(5);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"PaymentsAuditLogs\";"))
            .Should().Be(1);
        (await ScalarAsync<string>(connection,
            "SELECT \"Status\" FROM \"Payments\".\"CheckoutSessions\" WHERE \"Id\"=@id;",
            ("id", _seed.CheckoutSessionId))).Should().Be("Completed");
        (await ScalarAsync<string>(connection,
            "SELECT \"ProcessingOutcome\" FROM \"Payments\".\"WebhookEvents\" WHERE \"Id\"=@id;",
            ("id", secondDelivery.Value.EventId))).Should().Be("Duplicate");
        (await OutboxCountAsync(connection, nameof(AlphaZero.Modules.Payments.IntegrationEvents.PurchaseActivatedIntegrationEvent)))
            .Should().Be(1);

        var ledgerEntryId = await ScalarAsync<Guid>(connection,
            "SELECT \"Id\" FROM \"Payments\".\"LedgerEntries\" WHERE \"Type\"='Capture' LIMIT 1;");
        var activationBody = await ScalarAsync<string>(connection,
            "SELECT \"Body\" FROM \"Payments\".\"OutboxMessage\" WHERE \"MessageType\" LIKE @type LIMIT 1;",
            ("type", $"%{nameof(AlphaZero.Modules.Payments.IntegrationEvents.PurchaseActivatedIntegrationEvent)}%"));
        activationBody.Should().Contain(ledgerEntryId.ToString());
        activationBody.Should().Contain(_seed.PlanId.ToString());
    }

    [Fact]
    public async Task Replayed_movement_with_different_money_facts_is_rejected_without_second_purchase()
    {
        await SeedCaptureAsync(includeLedgerAccounts: true);
        await using var services = BuildServices();
        var first = await SendAsync(services, new ReceiveProviderWebhook("stripe", ValidHeaders(), Payload("event-1")));
        (await SendAsync(services, new ProcessWebhookEvent(first.Value.EventId))).IsError.Should().BeFalse();

        var conflicting = await SendAsync(services,
            new ReceiveProviderWebhook("stripe", ValidHeaders(), Payload("event-2", 101m)));
        var result = await SendAsync(services, new ProcessWebhookEvent(conflicting.Value.EventId));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Capture.MovementDisagrees");

        await using var connection = await OpenAsync();
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"Purchases\";"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"ProviderTransactions\";"))
            .Should().Be(1);
        (await ScalarAsync<string>(connection,
            "SELECT \"ProcessingOutcome\" FROM \"Payments\".\"WebhookEvents\" WHERE \"Id\"=@id;",
            ("id", conflicting.Value.EventId))).Should().Be("Failed");
    }

    [Fact]
    public async Task Concurrent_processing_of_one_delivery_creates_exactly_one_purchase()
    {
        await SeedCaptureAsync(includeLedgerAccounts: true);
        Guid eventId;
        await using (var ingestServices = BuildServices())
        {
            var ingested = await SendAsync(ingestServices, new ReceiveProviderWebhook("stripe", ValidHeaders(),
                Payload("event-1")));
            eventId = ingested.Value.EventId;
        }

        await using var processingServices = BuildServices();
        var attempts = await Task.WhenAll(
            SendAsync(processingServices, new ProcessWebhookEvent(eventId)),
            SendAsync(processingServices, new ProcessWebhookEvent(eventId)));

        attempts.Should().OnlyContain(result => !result.IsError);
        attempts.Select(result => result.Value.Outcome).Should()
            .BeEquivalentTo(new[] { WebhookProcessingOutcome.Processed, WebhookProcessingOutcome.Duplicate });

        await using var connection = await OpenAsync();
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"ProviderTransactions\";"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"Purchases\";"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"LedgerEntries\" WHERE \"Type\"='Capture';"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"PaymentsAuditLogs\";"))
            .Should().Be(1);
        (await OutboxCountAsync(connection, nameof(AlphaZero.Modules.Payments.IntegrationEvents.PurchaseActivatedIntegrationEvent)))
            .Should().Be(1);
    }

    [Fact]
    public async Task Capture_error_rolls_back_money_and_marks_webhook_failed()
    {
        await SeedCaptureAsync(includeLedgerAccounts: false);
        await using var services = BuildServices();
        var ingested = await SendAsync(services, new ReceiveProviderWebhook("stripe", ValidHeaders(),
            Payload("event-1")));

        var result = await SendAsync(services, new ProcessWebhookEvent(ingested.Value.EventId));
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(error => error.Code == "Capture.StructuralAccountsMissing");

        await using var connection = await OpenAsync();
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"ProviderTransactions\";"))
            .Should().Be(0);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"Purchases\";"))
            .Should().Be(0);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"LedgerEntries\";"))
            .Should().Be(0);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"PaymentsAuditLogs\";"))
            .Should().Be(0);
        (await ScalarAsync<string>(connection,
            "SELECT \"Status\" FROM \"Payments\".\"CheckoutSessions\" WHERE \"Id\"=@id;",
            ("id", _seed.CheckoutSessionId))).Should().Be("Pending");
        (await ScalarAsync<string>(connection,
            "SELECT \"ProcessingOutcome\" FROM \"Payments\".\"WebhookEvents\" WHERE \"Id\"=@id;",
            ("id", ingested.Value.EventId))).Should().Be("Failed");
        (await OutboxCountAsync(connection, nameof(AlphaZero.Modules.Payments.IntegrationEvents.PurchaseActivatedIntegrationEvent)))
            .Should().Be(0);
    }

    [Fact]
    public async Task Catalog_reconciliation_applies_only_newer_snapshots_and_emits_outbox_facts()
    {
        await using var services = BuildServices();
        var occurredAt = _seed.OccurredAt;
        CatalogProductSnapshot Snapshot(long version, SourceAvailability availability, string title) => new(
            _seed.TenantId, _seed.CourseId, _seed.PlanId, _seed.TargetResourceArn, title, availability,
            version, occurredAt.AddMinutes(version));

        (await SendAsync(services, new ReconcileProductCatalog([Snapshot(2, SourceAvailability.Available, "v2")])))
            .Value.Should().Be(1);
        (await SendAsync(services, new ReconcileProductCatalog([Snapshot(1, SourceAvailability.Unavailable, "stale")])))
            .Value.Should().Be(0);
        (await SendAsync(services, new ReconcileProductCatalog([Snapshot(2, SourceAvailability.Available, "replay")])))
            .Value.Should().Be(0);
        (await SendAsync(services, new ReconcileProductCatalog([Snapshot(3, SourceAvailability.Unavailable, "v3")])))
            .Value.Should().Be(1);

        await using var connection = await OpenAsync();
        (await ScalarAsync<long>(connection,
            "SELECT \"SourceVersion\" FROM \"Payments\".\"Products\" WHERE \"ItemId\"=@id;",
            ("id", _seed.PlanId))).Should().Be(3);
        (await ScalarAsync<string>(connection,
            "SELECT \"SourceAvailability\" FROM \"Payments\".\"Products\" WHERE \"ItemId\"=@id;",
            ("id", _seed.PlanId))).Should().Be("Unavailable");
        (await OutboxCountAsync(connection,
            nameof(AlphaZero.Modules.Payments.IntegrationEvents.CatalogProductMirrorAppliedIntegrationEvent)))
            .Should().Be(2);
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddMassTransit(configuration =>
        {
            configuration.AddEntityFrameworkOutbox<AppDbContext>(outbox =>
            {
                outbox.UsePostgres(false);
                outbox.UseBusOutbox();
                outbox.QueryDelay = TimeSpan.FromSeconds(5);
            });
            configuration.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });
        services.AddScoped<IProviderEventNormalizer>(_ => new FakeProviderEventNormalizer(_seed));
        services.AddPaymentsPrivateInfrastructure(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task<ErrorOr<T>> SendAsync<T>(ServiceProvider services, IRequest<ErrorOr<T>> request)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SeedCaptureAsync(bool includeLedgerAccounts)
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection, """
            INSERT INTO "Payments"."ProviderAccounts"
                ("Id","ProviderName","Environment","ExternalAccountId","Status","CredentialSecretName","WebhookSecretName","CreatedAt")
            VALUES (@provider,'stripe','Test','acct-workflow','Active','kv/stripe','kv/webhook',@now);
            INSERT INTO "Payments"."ProviderCurrencyRoutes"
                ("Id","ProviderAccountId","IntegrationCurrency","SettlementCurrency","PresentmentMode","IsEnabled","ShortHoldDays","ReserveRate","DisputeHoldDays","RequiresDailyReconciliation")
            VALUES (@route,@provider,'USD','USD','Exact',true,0,0,0,false);
            INSERT INTO "Payments"."Products"
                ("Id","TenantId","ItemType","ItemId","ParentContainerId","TargetResourceArn","Title","SourceAvailability","SourceVersion","CreatedAt")
            VALUES (@product,@tenant,'CoursePlan',@plan,@course,@arn,'Stored Course Title','Available',1,@now);
            INSERT INTO "Payments"."Prices"
                ("Id","TenantId","ProductId","Amount","Currency","IsActive","CreatedByUserId","CreatedAt")
            VALUES (@price,@tenant,@product,100.00,'USD',true,@actor,@now);
            INSERT INTO "Payments"."AcademyPaymentSettings"
                ("Id","TenantId","OnlineSellingEnabled","CommissionShare","CreatedAt")
            VALUES (@settings,@tenant,true,0.2500,@now);
            INSERT INTO "Payments"."CheckoutSessions"
                ("Id","TenantId","UserId","ProductId","PriceId","ProviderCurrencyRouteId","Status","CreatedAt")
            VALUES (@session,@tenant,@user,@product,@price,@route,'Pending',@now);
            """, ("provider", _seed.ProviderAccountId), ("route", _seed.RouteId),
            ("product", _seed.ProductId), ("tenant", _seed.TenantId), ("plan", _seed.PlanId),
            ("course", _seed.CourseId), ("arn", _seed.TargetResourceArn), ("price", _seed.PriceId),
            ("actor", Guid.NewGuid()), ("settings", Guid.NewGuid()), ("session", _seed.CheckoutSessionId),
            ("user", _seed.UserId), ("now", _seed.OccurredAt));

        if (!includeLedgerAccounts) return;
        await ExecuteAsync(connection, """
            INSERT INTO "Payments"."LedgerAccounts"
                ("Id","Name","Role","Type","Currency","TenantId","ProviderAccountId","IsActive","CreatedAt")
            VALUES
                (@asset,'Provider Clearing','ProviderAsset','Asset','USD',NULL,@provider,true,@now),
                (@revenue,'Platform Revenue','PlatformRevenue','Income','USD',NULL,@provider,true,@now),
                (@fees,'Provider Fees','ProviderFees','Expense','USD',NULL,@provider,true,@now),
                (@payable,'Academy Payable','AcademyPayable','Liability','USD',@tenant,@provider,true,@now);
            """, ("asset", Guid.NewGuid()), ("revenue", Guid.NewGuid()), ("fees", Guid.NewGuid()),
            ("payable", Guid.NewGuid()), ("tenant", _seed.TenantId), ("provider", _seed.ProviderAccountId),
            ("now", _seed.OccurredAt));
    }

    private byte[] Payload(string eventId, decimal amount = 100m) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        eventId,
        transactionId = "movement-1",
        amount
    });

    private static IReadOnlyDictionary<string, string> ValidHeaders() =>
        new Dictionary<string, string> { ["signature"] = "valid" };

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<long> OutboxCountAsync(NpgsqlConnection connection, string messageType) =>
        await ScalarAsync<long>(connection,
            "SELECT count(*) FROM \"Payments\".\"OutboxMessage\" WHERE \"MessageType\" LIKE @type;",
            ("type", $"%{messageType}%"));

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql,
        params (string Name, object Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return command;
    }

    private static PostgreSqlContainer? BuildContainerIfNeeded() =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYMENTS_TEST_CONNECTION_STRING"))
            ? new PostgreSqlBuilder()
                .WithImage("postgres:16.1-alpine3.19")
                .WithDatabase("payments_workflow_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build()
            : null;

    private sealed class WorkflowSeed
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CourseId { get; } = Guid.NewGuid();
        public Guid PlanId { get; } = Guid.NewGuid();
        public Guid ProductId { get; } = Guid.NewGuid();
        public Guid PriceId { get; } = Guid.NewGuid();
        public Guid ProviderAccountId { get; } = Guid.NewGuid();
        public Guid RouteId { get; } = Guid.NewGuid();
        public Guid CheckoutSessionId { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = TruncateToMicroseconds(DateTime.UtcNow);
        public string TargetResourceArn => $"az:course:{TenantId:D}:course/{CourseId:D}/plan/{PlanId:D}";

        private static DateTime TruncateToMicroseconds(DateTime value) =>
            value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private sealed class FakeProviderEventNormalizer(WorkflowSeed seed) : IProviderEventNormalizer
    {
        public string ProviderName => "stripe";

        public Task<ErrorOr<VerifiedProviderWebhook>> VerifyAsync(IReadOnlyDictionary<string, string> headers,
            ReadOnlyMemory<byte> rawBody, CancellationToken cancellationToken)
        {
            if (!headers.TryGetValue("signature", out var signature) || signature != "valid")
                return Task.FromResult<ErrorOr<VerifiedProviderWebhook>>(
                    Error.Unauthorized("Webhook.InvalidSignature", "Webhook signature is invalid."));
            using var document = JsonDocument.Parse(rawBody);
            var externalEventId = document.RootElement.GetProperty("eventId").GetString()!;
            return Task.FromResult<ErrorOr<VerifiedProviderWebhook>>(new VerifiedProviderWebhook(
                seed.ProviderAccountId, externalEventId, "capture.completed", "object-1",
                ProviderEnvironment.Test, false));
        }

        public Task<ErrorOr<NormalizedCapture?>> NormalizeCaptureAsync(StoredProviderWebhook webhook,
            CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(webhook.PayloadJson);
            var transactionId = document.RootElement.GetProperty("transactionId").GetString()!;
            var amount = document.RootElement.TryGetProperty("amount", out var amountElement)
                ? amountElement.GetDecimal()
                : 100m;
            NormalizedCapture capture = new(seed.CheckoutSessionId, seed.ProviderAccountId, transactionId,
                "payment-1", "object-1", amount, "USD", 1m, amount, "USD", null,
                seed.OccurredAt, false);
            return Task.FromResult(ErrorOrFactory.From<NormalizedCapture?>(capture));
        }
    }
}
