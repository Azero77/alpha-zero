
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Presentation;
using System.Reflection;

namespace AlphaZero.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        List<Type> modules = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && (a.FullName?.StartsWith("AlphaZero") ?? false))
            .SelectMany(c => c.GetTypes().Where(t => t.IsClass && !t.IsAbstract && typeof(AppModule).IsAssignableFrom(t)))
            .ToList();
        ConfigureModules(builder,modules);
        var app = builder.Build();

        app.MapDefaultEndpoints();
        InitializeModules(app,modules);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.Run();
    }

    private static void InitializeModules(WebApplication app,IEnumerable<Type> modules)
    {
        var root = app.Services.GetAutofacRoot();
        foreach (var module in modules)
        {
            var executingModule = (IModule)root.Resolve(module);
            executingModule.Initialize(root);
        }
    }

    private static void ConfigureModules(WebApplicationBuilder builder, IEnumerable<Type> modules)
    {
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            foreach (var module in modules)
            {
                containerBuilder.RegisterType(module).AsSelf().SingleInstance();
            }
        });
    }
}
