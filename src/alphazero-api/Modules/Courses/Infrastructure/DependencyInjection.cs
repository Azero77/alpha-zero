using AlphaZero.Modules.Courses.Application.Courses.Commands.AddAssessment;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.Infrastructure.Repositories;
using AlphaZero.Modules.Courses.Infrastructure.RequestResponseMessaging;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure;
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
        
    }

    public static void AddCoursesPrivateInfrastructure(this IServiceCollection moduleServices, IConfiguration configuration)
    {
        moduleServices.AddScoped<ICourseRepository, CourseRepository>();
        moduleServices.AddScoped<ISubjectRepository, SubjectRepository>();
        moduleServices.AddScoped<IEnrollementRepository, EnrollementRepository>();
        moduleServices.AddScoped<ICourseAnalyticsRepository, CourseAnalyticsRepository>();
        moduleServices.AddScoped<ISectionRepository, SectionRepository>();
        moduleServices.AddScoped<ICurriculumItemRepository, CurriculumItemRepository>();
        moduleServices.AddScoped<IAssessmentService, AssessmentService>();
        moduleServices.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();
        moduleServices.AddScoped<AlphaZero.Modules.Courses.Application.Queries.ICourseQueryService, AlphaZero.Modules.Courses.Infrastructure.Queries.CourseQueryService>();
        moduleServices.AddScoped<AlphaZero.Modules.Courses.Application.Queries.ISubjectQueryService, AlphaZero.Modules.Courses.Infrastructure.Queries.SubjectQueryService>();
        moduleServices.AddScoped<AlphaZero.Modules.Courses.Application.Queries.IEnrollmentQueryService, AlphaZero.Modules.Courses.Infrastructure.Queries.EnrollmentQueryService>();
        moduleServices.AddScoped<AlphaZero.Modules.Courses.Application.Queries.ICourseAnalyticsQueryService, AlphaZero.Modules.Courses.Infrastructure.Queries.CourseAnalyticsQueryService>();

        moduleServices.AddValidatorsFromAssembly(typeof(ICoursesApplicationMarker).Assembly);

        moduleServices.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(typeof(ICoursesApplicationMarker).Assembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });
    }
}

