using Microsoft.Extensions.Configuration;

namespace AlphaZero.Shared.Infrastructure;

public  class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";
    public string ConnectionString { get; set; } = string.Empty;

    public static DatabaseSettings GetDatabaseSettings(IConfiguration configuration)
    {

        DatabaseSettings dbSettings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>()
            ?? throw new ArgumentException("Database settings are not configured");

        var conn = configuration.GetConnectionString("alphazerodb");
        if (!string.IsNullOrEmpty(conn))
        {
            dbSettings.ConnectionString = conn;
        }

        return dbSettings;
    }
}
