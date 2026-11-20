using AlphaZero.Modules.Payments.Infrastructure.Behaviors;
using AlphaZero.Modules.Payments.Infrastructure.Persistance;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.SoftDelete;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DatabaseSettings dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);

        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString, h =>
            {
                h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                h.MigrationsHistoryTable("__PaymentsMigrationHistory", AppDbContext.Schema);
            });
        });

        return services;
    }

    public static IServiceCollection AddPaymentsPrivateInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();

        // Register MediatR handlers and validators from the Application assembly
        var applicationAssembly = typeof(Application.IPaymentsModule).Assembly;

        services.AddValidatorsFromAssembly(applicationAssembly);

        services.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(applicationAssembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(TransactionalBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });

        return services;
    }
}
