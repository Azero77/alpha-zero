using AlphaZero.Modules.Assessments.Application;
using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Application.Resolvers;
using AlphaZero.Modules.Assessments.Application.Services;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments.Servies;
using AlphaZero.Modules.Assessments.Infrastructure.Persistance;
using AlphaZero.Modules.Assessments.Infrastructure.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.SoftDelete;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.Assessments.Infrastructure;

public static class DependencyInjection
{
    public static void AddAssessmentsGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DatabaseSettings dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);

        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString, h =>
            {
                h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                h.MigrationsHistoryTable("__AssessmentsMigrationHistory", AppDbContext.Schema);
            });
            opts.AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());
        });
    }

    public static void AddAssessmentsPrivateInfrastructure(this IServiceCollection moduleServices, IConfiguration configuration)
    {
        moduleServices.AddScoped<IAssessmentRepository, AssessmentRepository>();
        moduleServices.AddScoped<ISubmissionRepository, SubmissionRepository>();

        moduleServices.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();

        moduleServices.AddValidatorsFromAssembly(typeof(IAssessmentsApplicationMarker).Assembly);

        moduleServices.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(typeof(IAssessmentsApplicationMarker).Assembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });
        
        // Register validators and factory
        moduleServices.AddScoped<IAssestmentValidtorFactory, AssestmentValidtorFactory>();
        moduleServices.AddScoped<McqAssessmentValidator>();
        moduleServices.AddScoped<HandwrittenAssessmentValidator>();
        moduleServices.AddScoped<HybridAssessmentValidator>();

        // Register Grading services
        moduleServices.AddScoped<AssessmentGradingService>();
        moduleServices.AddScoped<IQuestionResolver, McqQuestionResolver>();
    }
}
