using System.Reflection;
using AlphaZero.Modules.Payments.Infrastructure.Persistence;
using AlphaZero.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Migrations;

/// <summary>Applies the SQL baseline, then all generated EF Core Payments migrations.</summary>
public static class PaymentsSqlMigrationRunner
{
    public const string InitialMigrationId = "20261120000000_InitialPayments";
    private const string ResourceSuffix = ".20261120000000_InitialPayments.sql";

    public static async Task ApplyConfiguredAsync(
        IConfiguration configuration,
        bool allowRuntimeConnectionFallback,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration["Payments:MigrationConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (!allowRuntimeConnectionFallback)
            {
                throw new InvalidOperationException(
                    "Payments:MigrationConnectionString must be configured for the deployment migration command. " +
                    "The migration principal must hold CREATEROLE and own the Payments schema or be a database administrator.");
            }

            connectionString = DatabaseSettings.GetDatabaseSettings(configuration).ConnectionString;
        }

        await ApplyAsync(connectionString, cancellationToken);
    }

    public static async Task ApplyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = new NpgsqlCommand(
                         "SELECT pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended('Payments.SqlMigrations', 0));",
                         connection,
                         transaction))
        {
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var historyExists = await TableExistsAsync(connection, transaction, cancellationToken);
        if (historyExists)
        {
            await using var roleCommand = new NpgsqlCommand(
                "SELECT pg_catalog.pg_has_role(current_user, 'payments_migration', 'MEMBER');",
                connection,
                transaction);
            if ((bool)(await roleCommand.ExecuteScalarAsync(cancellationToken) ?? false))
            {
                await using var setRoleCommand = new NpgsqlCommand("SET LOCAL ROLE payments_migration;", connection, transaction);
                await setRoleCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        if (!historyExists || !await IsAppliedAsync(connection, transaction, cancellationToken))
        {
            var migrationSql = await ReadMigrationSqlAsync(cancellationToken);
            await using var migrationCommand = new NpgsqlCommand(migrationSql, connection, transaction)
            {
                CommandTimeout = 0
            };
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await ApplyEfMigrationsAsync(connectionString, cancellationToken);
    }

    private static async Task ApplyEfMigrationsAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var roleCommand = new NpgsqlCommand(
                         "SELECT pg_catalog.to_regrole('payments_migration') IS NOT NULL " +
                         "AND pg_catalog.pg_has_role(current_user, 'payments_migration', 'MEMBER');",
                         connection))
        {
            if ((bool)(await roleCommand.ExecuteScalarAsync(cancellationToken) ?? false))
            {
                await using var setRoleCommand = new NpgsqlCommand("SET ROLE payments_migration;", connection);
                await setRoleCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, postgres =>
            {
                postgres.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                postgres.MigrationsHistoryTable("__PaymentsMigrationHistory", AppDbContext.Schema);
            })
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_catalog.to_regclass('\"Payments\".\"__PaymentsMigrationHistory\"') IS NOT NULL;",
            connection,
            transaction);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> IsAppliedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM \"Payments\".\"__PaymentsMigrationHistory\" WHERE \"MigrationId\" = @migrationId);",
            connection,
            transaction);
        command.Parameters.AddWithValue("migrationId", InitialMigrationId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<string> ReadMigrationSqlAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(PaymentsSqlMigrationRunner).Assembly;
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded Payments SQL migration {ResourceSuffix} was not found.");
        }

        await using var stream = assembly.GetManifestResourceStream(resourceName)
                                 ?? throw new InvalidOperationException($"Could not open embedded migration {resourceName}.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
