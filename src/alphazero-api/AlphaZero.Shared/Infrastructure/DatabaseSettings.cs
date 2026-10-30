using Microsoft.Extensions.Configuration;

namespace AlphaZero.Shared.Infrastructure;

public  class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";
    public string ConnectionString { get; set; } = string.Empty;

    public static DatabaseSettings GetDatabaseSettings(IConfiguration configuration)
    {
        
        DatabaseSettings dbSettings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>() ?? 
            new();

        var conn = configuration.GetConnectionString("alphazerodb");
        if (!string.IsNullOrEmpty(conn))
        {
            dbSettings.ConnectionString = conn;
        }

        // Belt-and-braces for the cache eviction pipeline: ambient System.Transactions transactions
        // have no EF commit hook to flush eviction at, so the write path must never enlist in one.
        // The UnitOfWork interceptor also fails fast if an ambient transaction is ever encountered.
        if (!string.IsNullOrEmpty(dbSettings.ConnectionString)
            && !dbSettings.ConnectionString.Contains("Enlist=", StringComparison.OrdinalIgnoreCase))
        {
            dbSettings.ConnectionString = dbSettings.ConnectionString.TrimEnd(';') + ";Enlist=false";
        }

        return dbSettings;
    }
}
