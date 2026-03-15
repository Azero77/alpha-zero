
using AlphaZero.API.Shared;
using Amazon.Extensions.NETCore.Setup;
using Aspire.Shared;
using Autofac;
using Autofac.Extensions.DependencyInjection;
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
        ConfigureAWSResources(builder);
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        string[] assembliesPath = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll");
        foreach (var path in assembliesPath)
        {
            var assemblyName = AssemblyName.GetAssemblyName(path);
            if (assemblyName.FullName.StartsWith("AlphaZero"))
            {
                Assembly.Load(assemblyName);
            }
        }

        List<Type> modules = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(c => c.GetTypes().Where(t => t.IsClass && !t.IsAbstract && typeof(AppModule).IsAssignableFrom(t)))
            .ToList();
        ConfigureModules(builder, modules);
        var app = builder.Build();

        InitializeModules(app, modules);
        app.MapDefaultEndpoints();
        MapModulesEndpoint(app,modules);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {

            app.UseSwagger();
            app.UseSwaggerUI();
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

    private static void ConfigureAWSResources(WebApplicationBuilder builder)
    {
        AWSResources awsResources = new();
        AWSOptions options = builder.Configuration.GetAWSOptions();
        builder.Configuration.Bind(AWSResources.Section, awsResources);
        builder.Services.AddSingleton<AWSResources>(awsResources);
        builder.Services.AddDefaultAWSOptions(options);
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
