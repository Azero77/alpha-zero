using AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;
using AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Services;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Authorization;
using AlphaZero.Modules.Courses.Infrastructure.Expiry;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.Infrastructure.Repositories;
using AlphaZero.Modules.Courses.Infrastructure.RequestResponseMessaging;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.SoftDelete;
using Application;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddCoursesGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DatabaseSettings dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);

        services.AddDbContext<AppDbContext>((sp,opts) =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString, h =>
            {
                h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                h.MigrationsHistoryTable("__CoursesMigrationHistory", AppDbContext.Schema);
            });
            opts.AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());
        });

        // Enrollment expiry scan (issue #34): in-process hosted service (non-HTTP trigger). The
        // scan work lives in the module's own container (IEnrollmentExpiryService registered in
        // AddCoursesPrivateInfrastructure so its IUnitOfWork is unambiguously the Courses one);
        // the worker only bridges host lifecycle → module scope per pass.
        services.AddHostedService<EnrollmentExpiryWorker>();
    }

    public static void AddCoursesPrivateInfrastructure(this IServiceCollection moduleServices, IConfiguration configuration)
    {
        moduleServices.AddScoped<ICourseRepository, CourseRepository>();
        moduleServices.AddScoped<ICourseResourceQueryService, CourseResourceQueryService>();
        moduleServices.AddScoped<ICourseOverviewRepository, CourseOverviewRepository>();
        moduleServices.AddScoped<IRepository<CourseAsset>, BaseRepository<AppDbContext, CourseAsset>>();
        moduleServices.AddScoped<ISubjectRepository, SubjectRepository>();
        moduleServices.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        moduleServices.AddScoped<ICourseAnalyticsRepository, CourseAnalyticsRepository>();
        moduleServices.AddScoped<IAssessmentService, AssessmentService>();
        moduleServices.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();
        moduleServices.AddTransient<ICourseQueryBuilder, CourseQueryBuilder>();

        // Domain Services
        moduleServices.AddScoped<AlphaZero.Modules.Courses.Domain.Services.CourseReadinessService>();
        moduleServices.AddScoped<AlphaZero.Modules.Courses.Domain.Services.CoursePublishingService>();

        // Asset Readiness Strategies
        moduleServices.AddScoped<ICourseAssetReadinessStrategy, VideoAssetReadinessStrategy>();
        moduleServices.AddScoped<ICourseAssetReadinessStrategy, DocumentAssetReadinessStrategy>();
        moduleServices.AddScoped<ICourseAssetReadinessStrategy, AssessmentAssetReadinessStrategy>();

        // Course Metadata Sync Handlers
        moduleServices.AddScoped<ICourseMetadataSyncCommandHandler, VideoCourseMetadataSyncCommandHandler>();
        moduleServices.AddScoped<ICourseMetadataSyncCommandHandler, DocumentCourseMetadataSyncCommandHandler>();
        moduleServices.AddScoped<ICourseMetadataSyncCommandHandler, AssessmentCourseMetadataSyncCommandHandler>();

        moduleServices.AddScoped<AlphaZero.Modules.Courses.Application.Queries.ICourseQueryService, AlphaZero.Modules.Courses.Infrastructure.Queries.CourseQueryService>();
        moduleServices.AddScoped<AlphaZero.Modules.Courses.Application.Queries.ISubjectQueryService, AlphaZero.Modules.Courses.Infrastructure.Queries.SubjectQueryService>();
        moduleServices.AddScoped<AlphaZero.Modules.Courses.Application.Queries.IEnrollmentQueryService, AlphaZero.Modules.Courses.Infrastructure.Queries.EnrollmentQueryService>();
        moduleServices.AddScoped<AlphaZero.Modules.Courses.Application.Queries.ICourseAnalyticsQueryService, AlphaZero.Modules.Courses.Infrastructure.Queries.CourseAnalyticsQueryService>();

        // Enrollment expiry scan (issue #34): resolved in the module's own container so the
        // scan's IUnitOfWork is unambiguously the Courses one (the host root's IUnitOfWork
        // belongs to whichever module registered it globally first). The hosted worker resolves
        // this service through ICoursesModule.CreateScope().
        moduleServices.AddScoped<IEnrollmentExpiryService, EnrollmentExpiryService>();

        moduleServices.AddValidatorsFromAssembly(typeof(ICoursesApplicationMarker).Assembly);
        moduleServices.AddPreRequestHandlersFromAssembly(typeof(ICoursesApplicationMarker).Assembly);

        moduleServices.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(typeof(ICoursesApplicationMarker).Assembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(PreRequestBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });
    }
}

