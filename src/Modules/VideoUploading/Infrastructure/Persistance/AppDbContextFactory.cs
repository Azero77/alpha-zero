using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // 1. Build configuration (similar to how the API does it)
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../../AlphaZero.API");
        
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(dbSettings.ConnectionString, h =>
        {
            h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            h.MigrationsHistoryTable("__VideoUploadingMigrationHistory", AppDbContext.Schema);
        });

        // Use a fake tenant provider for design time
        return new AppDbContext(optionsBuilder.Options, new FakeTenantProvider());
    }
}
