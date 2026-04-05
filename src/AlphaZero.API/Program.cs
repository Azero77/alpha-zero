using AlphaZero.API.Shared;
using AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Tenats;
using Amazon.Extensions.NETCore.Setup;
using Aspire.Shared;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FastEndpoints;
using FastEndpoints.Swagger;
using MassTransit;
using Microsoft.OpenApi;
using System.Reflection;

namespace AlphaZero.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = CreateWebApplicationBuilder(args);
        var app = builder.Build();

        // Initialize and Run
        var moduleInstances = app.Services.GetServices<IModule>().ToList();
        var moduleTypes = moduleInstances.Select(m => m.GetType()).ToList();

        InitializeModules(app, moduleInstances);
        app.MapDefaultEndpoints();
        app.UseFastEndpoints(c =>
        {
            c.Errors.UseProblemDetails();
        })
            .UseSwaggerGen();
        MapModulesEndpoint(app, moduleTypes);

        if (app.Environment.IsDevelopment())
        {
            app.UseCors(b => b.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();

        // Run migrations only when NOT in design-time (EF tools)
        // EF tools don't call Main if they find CreateBuilder, but we ensure safety here too.
        await app.RunMigrations(moduleInstances);

        await app.RunAsync();
    }

    // EF Core tools (.NET 6+) look for a method that returns WebApplicationBuilder or IHost
    public static WebApplicationBuilder CreateWebApplicationBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        LoadModuleAssemblies();

        builder.AddServiceDefaults();
        builder.Services.AddAuthorization();
        
        builder.Services.AddSharedInfrastructure(builder.Configuration, builder.Environment);
        builder.Services.AddDatabaseSettings(builder.Configuration);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddCors();

        builder.Services.AddFastEndpoints(o =>
        {
            o.SourceGeneratorDiscoveredTypes = new List<Type>(); // Disable SG to allow manual assembly scanning
            o.Assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName!.StartsWith("AlphaZero"))
                .ToList();
        }).SwaggerDocument();

        var moduleInstances = RegisterModules(builder);

        ConfigureMassTransit(builder, moduleInstances);
        ConfigureAutofac(builder, moduleInstances);

        return builder;
    }

    private static void LoadModuleAssemblies()
    {
        string[] assembliesPath = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll");
        foreach (var path in assembliesPath)
        {
            var assemblyName = AssemblyName.GetAssemblyName(path);
            if (assemblyName.FullName.StartsWith("AlphaZero"))
            {
                Assembly.Load(assemblyName);
            }
        }
    }

    private static List<IModule> RegisterModules(WebApplicationBuilder builder)
    {
        var moduleTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(c => c.GetTypes().Where(t => t.IsClass && !t.IsAbstract && typeof(AppModule).IsAssignableFrom(t)))
            .ToList();

        List<IModule> moduleInstances = new();
        foreach (var type in moduleTypes)
        {
            var instance = (IModule)Activator.CreateInstance(type)!;
            instance.Configuration = builder.Configuration;
            instance.RegisterGlobal(builder.Services);
            moduleInstances.Add(instance);
        }
        return moduleInstances;
    }

    private static void ConfigureMassTransit(WebApplicationBuilder builder, List<IModule> moduleInstances)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName!.StartsWith("AlphaZero"))
            .ToArray();

        builder.Services.AddMassTransit<IModuleBus>(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddConsumers(filter => !filter.Name.Contains("sqs", StringComparison.InvariantCultureIgnoreCase), assemblies);
            foreach (var module in moduleInstances)
            {
                module.ConfigureModuleBus(x);
            }
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
            
            x.ConfigureHealthCheckOptions(options =>
            {
                options.Name = "module-bus";
            });
        });

        builder.Services.AddMassTransit<IExternalBus>(x =>
        {
            x.AddConsumers(filter => filter.Name.Contains("sqs", StringComparison.InvariantCultureIgnoreCase), assemblies);
            x.UsingAmazonSqs((context, cfg) =>
            {
                try {
                    string region = context.GetRequiredService<IConfiguration>().GetAWSOptions().Region.SystemName;
                    cfg.Host(region, h => { });
                } catch { /* Suppress design-time failures */ }
                cfg.ConfigureEndpoints(context);
            });

            x.ConfigureHealthCheckOptions(options =>
            {
                options.Name = "external-bus";
            });
        });
    }

    private static void ConfigureAutofac(WebApplicationBuilder builder, List<IModule> moduleInstances)
    {
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            foreach (var moduleInstance in moduleInstances)
            {
                containerBuilder.RegisterModule((Autofac.Module)moduleInstance);
                containerBuilder.RegisterInstance(moduleInstance).AsSelf().As<IModule>().SingleInstance();
            }
        });
    }

    private static void MapModulesEndpoint(IEndpointRouteBuilder app, List<Type> modules)
    {
        foreach (var module in modules)
        {
            var endpointTypes = module.Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && t.IsClass && typeof(Shared.IEndpoint).IsAssignableFrom(t))
                .ToList();

            var endpoints = endpointTypes.Select(s => (Shared.IEndpoint)Activator.CreateInstance(s)!);
            foreach (var endpoint in endpoints)
            {
                endpoint.MapEndpoint(app);
            }
        }
    }

    private static void InitializeModules(WebApplication app, IEnumerable<IModule> modules)
    {
        var root = app.Services.GetAutofacRoot();
        foreach (var module in modules)
        {
            module.Initialize(root);
        }
    }
}
