using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure;
using Application;
using Aspire.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddCoursesGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Add global infrastructure needed specifically for Courses module here
    }

    public static void AddCoursesPrivateInfrastructure(this IServiceCollection moduleServices, IConfiguration configuration)
    {
        DatabaseSettings dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);
        moduleServices.AddMediatR(opts => opts.RegisterServicesFromAssembly(typeof(ICoursesApplicationMarker).Assembly));

       /* moduleServices.AddDbContext<AppDbContext>(opts =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString);
        });*/
    }
}
