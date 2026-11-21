using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using AlphaZero.Modules.Payments.Infrastructure.Persistence.Migrations;
using Xunit;

namespace Payments.Tests.Integration;

public sealed class PaymentsSchemaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _postgres = BuildContainerIfNeeded();
    private string ConnectionString => Environment.GetEnvironmentVariable("PAYMENTS_TEST_CONNECTION_STRING")
                                      ?? _postgres!.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.StartAsync();
        }
        else
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("DROP SCHEMA IF EXISTS \"Payments\" CASCADE;", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    private static PostgreSqlContainer? BuildContainerIfNeeded()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYMENTS_TEST_CONNECTION_STRING")))
        {
            return null;
        }

        return new PostgreSqlBuilder()
            .WithImage("postgres:16.1-alpine3.19")
            .WithDatabase("payments_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    [Fact]
    public async Task Initial_migration_is_tracked_repeatable_and_contains_required_schema()
    {
        await PaymentsSqlMigrationRunner.ApplyAsync(ConnectionString);
        await PaymentsSqlMigrationRunner.ApplyAsync(ConnectionString);

        await using var connection = await OpenAsync();
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'Payments' AND table_type = 'BASE TABLE';"))
            .Should().Be(29);
        (await ScalarAsync<string[]>(connection, "SELECT array_agg(\"MigrationId\" ORDER BY \"MigrationId\") FROM \"Payments\".\"__PaymentsMigrationHistory\";"))
            .Should().Equal(
                PaymentsSqlMigrationRunner.InitialMigrationId,
                "20261121181625_AddPaymentsOutboxTables");
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"Currencies\";"))
            .Should().Be(9);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"PaymentProviders\" WHERE NOT \"IsEnabled\";"))
            .Should().Be(2);
        (await ScalarAsync<bool>(connection, "SELECT has_table_privilege('payments_runtime', '\"Payments\".\"Prices\"', 'INSERT');"))
            .Should().BeTrue();
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'Payments' AND table_name IN ('InboxState','OutboxState','OutboxMessage');"))
            .Should().Be(3);
        (await ScalarAsync<bool>(connection, "SELECT has_table_privilege('payments_runtime', '\"Payments\".\"InboxState\"', 'SELECT,INSERT,UPDATE,DELETE');"))
            .Should().BeTrue();
        (await ScalarAsync<bool>(connection, "SELECT has_sequence_privilege('payments_runtime', '\"Payments\".\"InboxState_Id_seq\"', 'USAGE');"))
            .Should().BeTrue();
        (await ScalarAsync<bool>(connection, "SELECT to_regclass('\"Payments\".\"UQ_CheckoutSessions_User_Product_Pending\"') IS NOT NULL;"))
            .Should().BeTrue();
        (await ScalarAsync<string>(connection, "SELECT pg_get_expr(indpred, indrelid) FROM pg_index WHERE indexrelid = '\"Payments\".\"UQ_CheckoutSessions_User_Product_Pending\"'::regclass;"))
            .Should().Contain("Pending");
    }

    [Fact]
    public async Task Prices_enforce_currency_scale_reference_and_single_active_offer()
    {
        await PaymentsSqlMigrationRunner.ApplyAsync(ConnectionString);
        await using var connection = await OpenAsync();
        var tenant = Guid.NewGuid();
        var product = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"Products\" (\"Id\",\"TenantId\",\"ItemId\",\"ParentContainerId\",\"TargetResourceArn\",\"Title\",\"SourceVersion\",\"CreatedAt\") VALUES (@id,@tenant,@item,@parent,'arn:test','Product',1,@now);",
            ("id", product), ("tenant", tenant), ("item", Guid.NewGuid()), ("parent", Guid.NewGuid()), ("now", now));

        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"Prices\" (\"Id\",\"TenantId\",\"ProductId\",\"Amount\",\"Currency\",\"IsActive\",\"CreatedByUserId\",\"CreatedAt\") VALUES (@id,@tenant,@product,12.34,'USD',true,@user,@now);",
            ("id", Guid.NewGuid()), ("tenant", tenant), ("product", product), ("user", Guid.NewGuid()), ("now", now));
        await ThrowsPostgresAsync(connection,
            "INSERT INTO \"Payments\".\"Prices\" (\"Id\",\"TenantId\",\"ProductId\",\"Amount\",\"Currency\",\"IsActive\",\"CreatedByUserId\",\"CreatedAt\") VALUES (@id,@tenant,@product,5.00,'USD',true,@user,@now);",
            ("id", Guid.NewGuid()), ("tenant", tenant), ("product", product), ("user", Guid.NewGuid()), ("now", now));
        await ThrowsPostgresAsync(connection,
            "INSERT INTO \"Payments\".\"Prices\" (\"Id\",\"TenantId\",\"ProductId\",\"Amount\",\"Currency\",\"IsActive\",\"CreatedByUserId\",\"CreatedAt\") VALUES (@id,@tenant,@product,12.3456,'USD',true,@user,@now);",
            ("id", Guid.NewGuid()), ("tenant", tenant), ("product", product), ("user", Guid.NewGuid()), ("now", now));
        await ThrowsPostgresAsync(connection,
            "INSERT INTO \"Payments\".\"Prices\" (\"Id\",\"TenantId\",\"ProductId\",\"Amount\",\"Currency\",\"IsActive\",\"CreatedByUserId\",\"CreatedAt\") VALUES (@id,@tenant,@product,1.00,'ZZZ',true,@user,@now);",
            ("id", Guid.NewGuid()), ("tenant", tenant), ("product", product), ("user", Guid.NewGuid()), ("now", now));

        (await ScalarAsync<long>(connection, "SELECT count(*) FROM \"Payments\".\"Prices\";"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Checkout_session_requires_active_price_matching_tenant_and_enabled_currency_route()
    {
        await PaymentsSqlMigrationRunner.ApplyAsync(ConnectionString);
        await using var connection = await OpenAsync();
        var now = DateTimeOffset.UtcNow;
        var tenant = Guid.NewGuid();
        var product = Guid.NewGuid();
        var price = Guid.NewGuid();
        var providerAccount = Guid.NewGuid();
        var enabledRoute = Guid.NewGuid();
        var disabledRoute = Guid.NewGuid();

        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"ProviderAccounts\" (\"Id\",\"ProviderName\",\"Environment\",\"ExternalAccountId\",\"Status\",\"CredentialSecretName\",\"WebhookSecretName\",\"CreatedAt\") VALUES (@id,'stripe','Test','checkout-test','Active','kv/stripe','kv/hook',@now);",
            ("id", providerAccount), ("now", now));
        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"ProviderCurrencyRoutes\" (\"Id\",\"ProviderAccountId\",\"IntegrationCurrency\",\"SettlementCurrency\",\"PresentmentMode\",\"IsEnabled\",\"ShortHoldDays\",\"ReserveRate\",\"DisputeHoldDays\",\"RequiresDailyReconciliation\") VALUES (@enabled,@provider,'USD','USD','Exact',true,0,0,0,false),(@disabled,@provider,'SYP','SYP','Exact',false,0,0,0,false);",
            ("enabled", enabledRoute), ("disabled", disabledRoute), ("provider", providerAccount));
        var historicalMovementId = Guid.NewGuid();
        await ExecuteAsync(connection, "SET ROLE payments_runtime;");
        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"ProviderTransactions\" (\"Id\",\"ProviderAccountId\",\"ExternalTransactionId\",\"Type\",\"Direction\",\"Amount\",\"Currency\",\"OccurredAt\") VALUES (@id,@provider,'historical-disabled-route','Capture','Incoming',1.00,'SYP',@now);",
            ("id", historicalMovementId), ("provider", providerAccount), ("now", now));
        await ExecuteAsync(connection, "RESET ROLE;");

        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"Products\" (\"Id\",\"TenantId\",\"ItemId\",\"ParentContainerId\",\"TargetResourceArn\",\"Title\",\"SourceVersion\",\"CreatedAt\") VALUES (@id,@tenant,@item,@parent,'arn:checkout','Checkout Product',1,@now);",
            ("id", product), ("tenant", tenant), ("item", Guid.NewGuid()), ("parent", Guid.NewGuid()), ("now", now));
        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"Prices\" (\"Id\",\"TenantId\",\"ProductId\",\"Amount\",\"Currency\",\"IsActive\",\"CreatedByUserId\",\"CreatedAt\") VALUES (@id,@tenant,@product,9.99,'USD',true,@user,@now);",
            ("id", price), ("tenant", tenant), ("product", product), ("user", Guid.NewGuid()), ("now", now));

        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"CheckoutSessions\" (\"Id\",\"TenantId\",\"UserId\",\"ProductId\",\"PriceId\",\"ProviderCurrencyRouteId\",\"Status\",\"CreatedAt\") VALUES (@id,@tenant,@user,@product,@price,@route,'Pending',@now);",
            ("id", Guid.NewGuid()), ("tenant", tenant), ("user", Guid.NewGuid()), ("product", product), ("price", price), ("route", enabledRoute), ("now", now));

        await ThrowsPostgresAsync(connection,
            "INSERT INTO \"Payments\".\"CheckoutSessions\" (\"Id\",\"TenantId\",\"UserId\",\"ProductId\",\"PriceId\",\"ProviderCurrencyRouteId\",\"Status\",\"CreatedAt\") VALUES (@id,@tenant,@user,@product,@price,@route,'Pending',@now);",
            "23514", ("id", Guid.NewGuid()), ("tenant", tenant), ("user", Guid.NewGuid()), ("product", product), ("price", price), ("route", disabledRoute), ("now", now));
        await ThrowsPostgresAsync(connection,
            "INSERT INTO \"Payments\".\"CheckoutSessions\" (\"Id\",\"TenantId\",\"UserId\",\"ProductId\",\"PriceId\",\"ProviderCurrencyRouteId\",\"Status\",\"CreatedAt\") VALUES (@id,@tenant,@user,@product,@price,@route,'Pending',@now);",
            "23514", ("id", Guid.NewGuid()), ("tenant", Guid.NewGuid()), ("user", Guid.NewGuid()), ("product", product), ("price", price), ("route", enabledRoute), ("now", now));

        await ExecuteAsync(connection,
            "UPDATE \"Payments\".\"Prices\" SET \"IsActive\"=false, \"ArchivedAt\"=@now WHERE \"Id\"=@id;",
            ("id", price), ("now", now));
        await ThrowsPostgresAsync(connection,
            "INSERT INTO \"Payments\".\"CheckoutSessions\" (\"Id\",\"TenantId\",\"UserId\",\"ProductId\",\"PriceId\",\"ProviderCurrencyRouteId\",\"Status\",\"CreatedAt\") VALUES (@id,@tenant,@user,@product,@price,@route,'Pending',@now);",
            "23514", ("id", Guid.NewGuid()), ("tenant", tenant), ("user", Guid.NewGuid()), ("product", product), ("price", price), ("route", enabledRoute), ("now", now));
    }

    [Fact]
    public async Task Ledger_posting_function_writes_balanced_entry_and_rejects_sub_minor_unit_lines()
    {
        await PaymentsSqlMigrationRunner.ApplyAsync(ConnectionString);
        await using var connection = await OpenAsync();
        var now = DateTimeOffset.UtcNow;
        now = now.AddTicks(-(now.Ticks % 10));
        var providerAccount = Guid.NewGuid();
        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"ProviderAccounts\" (\"Id\",\"ProviderName\",\"Environment\",\"ExternalAccountId\",\"Status\",\"CredentialSecretName\",\"WebhookSecretName\",\"CreatedAt\") VALUES (@id,'stripe','Test','acct_test','Active','kv/stripe','kv/stripe-hook',@now);",
            ("id", providerAccount), ("now", now));
        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"ProviderCurrencyRoutes\" (\"Id\",\"ProviderAccountId\",\"IntegrationCurrency\",\"SettlementCurrency\",\"PresentmentMode\",\"IsEnabled\",\"ShortHoldDays\",\"ReserveRate\",\"DisputeHoldDays\",\"RequiresDailyReconciliation\") VALUES (@id,@provider,'USD','USD','Adaptive',true,14,0.1,120,false);",
            ("id", Guid.NewGuid()), ("provider", providerAccount));
        var providerAsset = Guid.NewGuid();
        var bank = Guid.NewGuid();
        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"LedgerAccounts\" (\"Id\",\"Name\",\"Role\",\"Type\",\"Currency\",\"ProviderAccountId\",\"IsActive\",\"CreatedAt\") VALUES (@asset,'Provider Clearing','ProviderAsset','Asset','USD',@provider,true,@now), (@bank,'Bank Account','Bank','Asset','USD',NULL,true,@now);",
            ("asset", providerAsset), ("bank", bank), ("provider", providerAccount), ("now", now));
        var transactionId = Guid.NewGuid();
        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"ProviderTransactions\" (\"Id\",\"ProviderAccountId\",\"ExternalTransactionId\",\"Type\",\"Direction\",\"Amount\",\"Currency\",\"OccurredAt\") VALUES (@id,@provider,'payout-1','Payout','Outgoing',20.00,'USD',@now);",
            ("id", transactionId), ("provider", providerAccount), ("now", now));

        var entryId = Guid.NewGuid();
        var effectiveAt = now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
        var posting = $$"""
            {"entry":{"id":"{{entryId}}","type":"ProviderPayout","providerAccountId":"{{providerAccount}}","providerTransactionId":"{{transactionId}}","effectiveAt":"{{effectiveAt}}"},"lines":[{"id":"{{Guid.NewGuid()}}","accountId":"{{bank}}","direction":"Debit","amount":20.00,"currency":"USD"},{"id":"{{Guid.NewGuid()}}","accountId":"{{providerAsset}}","direction":"Credit","amount":20.00,"currency":"USD"}]}
            """;
        await ExecuteAsync(connection, "SET ROLE payments_runtime;");
        await ExecuteAsync(connection, "SELECT \"Payments\".post_ledger_entry(@posting::jsonb);", ("posting", posting));
        await ExecuteAsync(connection, "RESET ROLE;");
        (await ScalarAsync<decimal>(connection,
            "SELECT sum(\"Amount\") FILTER (WHERE \"Direction\"='Debit') FROM \"Payments\".\"LedgerLines\" WHERE \"EntryId\"=@id;", ("id", entryId)))
            .Should().Be(20m);

        var badMovementId = Guid.NewGuid();
        await ExecuteAsync(connection,
            "INSERT INTO \"Payments\".\"ProviderTransactions\" (\"Id\",\"ProviderAccountId\",\"ExternalTransactionId\",\"Type\",\"Direction\",\"Amount\",\"Currency\",\"OccurredAt\") VALUES (@id,@provider,'payout-2','Payout','Outgoing',20.00,'USD',@now);",
            ("id", badMovementId), ("provider", providerAccount), ("now", now));
        var badPosting = $$"""
            {"entry":{"id":"{{Guid.NewGuid()}}","type":"ProviderPayout","providerAccountId":"{{providerAccount}}","providerTransactionId":"{{badMovementId}}","effectiveAt":"{{effectiveAt}}"},"lines":[{"id":"{{Guid.NewGuid()}}","accountId":"{{bank}}","direction":"Debit","amount":20.001,"currency":"USD"},{"id":"{{Guid.NewGuid()}}","accountId":"{{providerAsset}}","direction":"Credit","amount":20.001,"currency":"USD"}]}
            """;
        await ExecuteAsync(connection, "SET ROLE payments_runtime;");
        await ThrowsPostgresAsync(connection, "SELECT \"Payments\".post_ledger_entry(@posting::jsonb);", "23514", ("posting", badPosting));
        await ExecuteAsync(connection, "RESET ROLE;");
    }

    [Fact]
    public async Task Runtime_role_cannot_insert_ledger_directly_and_public_cannot_post()
    {
        await PaymentsSqlMigrationRunner.ApplyAsync(ConnectionString);
        await using var connection = await OpenAsync();
        (await ScalarAsync<bool>(connection, "SELECT has_table_privilege('payments_runtime', '\"Payments\".\"LedgerEntries\"', 'INSERT');"))
            .Should().BeFalse();
        (await ScalarAsync<bool>(connection, "SELECT has_function_privilege('payments_runtime', '\"Payments\".post_ledger_entry(jsonb)', 'EXECUTE');"))
            .Should().BeTrue();
        (await ScalarAsync<bool>(connection, "SELECT has_function_privilege('public', '\"Payments\".post_ledger_entry(jsonb)', 'EXECUTE');"))
            .Should().BeFalse();

        await ExecuteAsync(connection, "SET ROLE payments_runtime;");
        await ThrowsPostgresAsync(connection,
            "INSERT INTO \"Payments\".\"LedgerEntries\" (\"Id\",\"Type\") VALUES (gen_random_uuid(),'ManualAdjustment');",
            "42501");
        await ExecuteAsync(connection, "RESET ROLE;");
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ThrowsPostgresAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        await FluentActions.Invoking(() => command.ExecuteNonQueryAsync())
            .Should().ThrowAsync<PostgresException>();
    }

    private static async Task ThrowsPostgresAsync(
        NpgsqlConnection connection,
        string sql,
        string expectedSqlState,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        var exception = await FluentActions.Invoking(() => command.ExecuteNonQueryAsync())
            .Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(expectedSqlState);
    }

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        return command;
    }
}
