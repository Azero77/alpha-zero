using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.Infrastructure.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.SoftDelete;
using Application;
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

        moduleServices.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();

        moduleServices.AddMediatR(opts => opts.RegisterServicesFromAssembly(typeof(ICoursesApplicationMarker).Assembly));
    }
}

