using AlphaZero.API.Shared;
using AlphaZero.Shared.Application;
using Amazon.Extensions.NETCore.Setup;
using Aspire.Shared;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using MassTransit;
using System.Reflection;

namespace AlphaZero.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();
        builder.Services.AddAuthorization();
        var awsResources = ConfigureAWSResources(builder);
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddCors();
        builder.Services.AddScoped<IModuleBus, ModuleBus>();

        string[] assembliesPath = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll");
        foreach (var path in assembliesPath)
        {
            var assemblyName = AssemblyName.GetAssemblyName(path);
            if (assemblyName.FullName.StartsWith("AlphaZero"))
            {
                Assembly.Load(assemblyName);
            }
        }

        // Configure MassTransit with in-memory bus and automatic consumer discovery
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName!.StartsWith("AlphaZero"))
            .ToArray();


        //Configure In-Memory Messagin
        builder.Services.AddMediator(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddConsumers(filter => !filter.Name.Contains("sqs", StringComparison.InvariantCultureIgnoreCase), assemblies);
            x.AddSagas(assemblies);
        });

        //Configure SQS messaging
        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumers(filter => filter.Name.Contains("sqs", StringComparison.InvariantCultureIgnoreCase),assemblies);
            x.UsingAmazonSqs((context, cfg) =>
            {
                string region = context.GetRequiredService<IConfiguration>().GetAWSOptions()
                .Region.SystemName;
                cfg.Host(region, h => { });
                cfg.ConfigureEndpoints(context);
            });

        });


        List<Type> moduleTypes = AppDomain.CurrentDomain.GetAssemblies()
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

        ConfigureModules(builder, moduleInstances);
        var app = builder.Build();

        InitializeModules(app, moduleInstances);
        app.MapDefaultEndpoints();
        MapModulesEndpoint(app, moduleTypes);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {

            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseCors(builder =>
            {
                builder.AllowAnyHeader()
                .AllowAnyOrigin()
                .AllowAnyMethod();
            });
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.Run();
    }

    private static void MapModulesEndpoint(IEndpointRouteBuilder app,List<Type> modules)
    {
        foreach (var module in modules)
        {
            var endpointTypes = module.Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && t.IsClass && typeof(IEndpoint).IsAssignableFrom(t))
                .ToList();

            var endpoints = endpointTypes.Select(s =>
            {
                return (IEndpoint) Activator.CreateInstance(s)!;
            });
            foreach (var endpoint in endpoints)
            {
                endpoint.MapEndpoint(app);
            }
        }
    }

    private static AWSResources ConfigureAWSResources(WebApplicationBuilder builder)
    {
        AWSResources awsResources = new();
        AWSOptions options = builder.Configuration.GetAWSOptions();
        builder.Configuration.Bind(AWSResources.Section, awsResources);
        builder.Services.AddSingleton<AWSResources>(awsResources);
        builder.Services.AddDefaultAWSOptions(options);

        return awsResources;
    }

    private static void InitializeModules(WebApplication app, IEnumerable<IModule> modules)
    {
        var root = app.Services.GetAutofacRoot();
        foreach (var module in modules)
        {
            module.Initialize(root);
        }
    }

    private static void ConfigureModules(WebApplicationBuilder builder, IEnumerable<IModule> modules)
    {
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            foreach (var moduleInstance in modules)
            {
                // Register as a native Autofac module to run Load() on the root container
                containerBuilder.RegisterModule((Autofac.Module)moduleInstance);
                
                // Register the instance so it can be resolved during InitializeModules
                containerBuilder.RegisterInstance(moduleInstance).AsSelf().As<IModule>().SingleInstance();
            }
        });
    }
}
