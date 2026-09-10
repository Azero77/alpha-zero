using AlphaZero.Modules.Documents.Application;
using AlphaZero.Modules.Documents.Application.Services;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Modules.Documents.Infrastructure.Persistance;
using AlphaZero.Modules.Documents.Infrastructure.Repositories;
using AlphaZero.Modules.Documents.Infrastructure.Services;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.Documents.Infrastructure;

public static class DependencyInjection
{
    public static void AddDocumentsGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);

        services.AddScoped<IDocumentStorageService, S3DocumentStorageService>();

        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString, h =>
            {
                h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                h.MigrationsHistoryTable("__DocumentsMigrationHistory", AppDbContext.Schema);
            });

            opts.AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());
        });
    }

    public static void AddDocumentsPrivateInfrastructure(this IServiceCollection moduleServices, IConfiguration configuration)
    {
        moduleServices.AddFluentValidation(typeof(IDocumentsApplicationMarker));

        moduleServices.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(typeof(IDocumentsApplicationMarker).Assembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });

        moduleServices.AddScoped<IDocumentRepository, DocumentRepository>();
        moduleServices.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();
    }
}
