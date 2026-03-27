using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Shared.Infrastructure;

public static class MigrationRunner
{
    //every module has a dbcontext of AppDbContext 
    public static async Task RunMigrations(this WebApplication app, List<IModule> modules)
    {
        if (!app.Environment.IsDevelopment())
            return;

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MigrationRunner");

        logger.LogInformation("Starting automated database migrations for {ModuleCount} modules...", modules.Count);

        foreach (var module in modules)
        {
            var moduleName = module.GetType().Name;
            
            try
            {
                if (module is AppModule appModule)
                {
                    logger.LogInformation("Applying migrations for module: {ModuleName}...", moduleName);
                    await appModule.RunMigrations();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while applying migrations for module: {ModuleName}.", moduleName);
                throw; // Stop startup if migrations fail in development
            }
        }

        logger.LogInformation("Finished automated database migrations.");
    }
}
