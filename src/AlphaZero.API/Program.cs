using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Swagger;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using NSwag;
using NSwag.AspNetCore;
using System.Reflection;
using System.Security.Claims;

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
            if (app.Environment.IsDevelopment())
            {
                c.Endpoints.Configurator = ep =>
                {
                    ep.PreProcessor<IAMDevPreprocessor>(Order.Before);
                };
            }
            else
            {
                c.Endpoints.Configurator = ep =>
                {
                    ep.PreProcessor<IAMPreprocessor>(Order.Before);
                };
            }
        })
            .UseSwaggerGen();

        app.UseSwaggerUi(c =>
        {
            c.OAuth2Client = new OAuth2ClientSettings()
            {
                ClientId = builder.Configuration["Keycloak:ClientId"] ?? "alpha-zero-client",
                UsePkceWithAuthorizationCodeGrant = true,
            };

        });
        MapModulesEndpoint(app, moduleTypes);

        if (app.Environment.IsDevelopment())
        {
            app.UseCors(b => b.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        // Run migrations only when NOT in design-time (EF tools)
        // EF tools don't call Main if they find CreateBuilder, but we ensure safety here too.
        await app.RunMigrations(moduleInstances);

        if (app.Environment.IsDevelopment())
        {
            var identityModule = moduleInstances.OfType<AlphaZero.Modules.Identity.Presentation.IdentityModule>().FirstOrDefault();
            if (identityModule is not null)
            {
                using var scope = identityModule.CreateScope();
                var identityContext = scope.Resolve<AlphaZero.Modules.Identity.Infrastructure.Persistance.AppDbContext>();
                await AlphaZero.Modules.Identity.Infrastructure.Persistance.Seeding.IdentitySeedReader.SeedAsync(identityContext);
            }
        }

        app.MapGet("users/me", (ClaimsPrincipal principal) =>
        {
            return principal.Claims.ToDictionary(c => c.Type, c => c.Value);
        }).RequireAuthorization();

        await app.RunAsync();
    }

    public static WebApplicationBuilder CreateWebApplicationBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        LoadModuleAssemblies();

        builder.AddServiceDefaults();
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Authentication:Authority"] ?? "http://localhost:8080/realms/alpha-zero";
            options.Audience = builder.Configuration["Authentication:Audience"] ?? "account";
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Authentication:Authority"] ?? "http://localhost:8080/realms/alpha-zero",
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Authentication:Audience"] ?? "account"
            };
        });
        
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
        }).SwaggerDocument( o =>
        {
            o.DocumentSettings = s =>
            {
                s.Title = "Alpha Zero";
                
                s.AddAuth("oauth2", new()
                {
                    Type = OpenApiSecuritySchemeType.OAuth2,
                    Flows = new()
                    {
                        AuthorizationCode = new()
                        {
                            AuthorizationUrl = builder.Configuration["Keycloak:AuthorizationUrl"]!,
                            TokenUrl = builder.Configuration["Keycloak:TokenUrl"]!,
                            Scopes = new Dictionary<string, string>
                            {
                                ["openid"] = "openid",
                                ["profile"] = "profile",
                                ["email"] = "email"
                            }
                        }
                    },
                    
                });
            };
        });

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

        builder.Services.AddDbContext<JobServiceSagaDbContext>((sp, opts) =>
        {
            DatabaseSettings dbSettings = DatabaseSettings.GetDatabaseSettings(builder.Configuration);

            opts.UseNpgsql(dbSettings.ConnectionString, h =>
            {
                h.MigrationsAssembly(typeof(DatabaseSettings).Assembly.FullName);//shared assembly name
                h.MigrationsHistoryTable("__JobServiceSagaMigrationHistory", "Jobs");
            });
        });

        builder.Services.AddMassTransit<IModuleBus>(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddJobSagaStateMachines(options =>
            {
                options.FinalizeCompleted = true;


            }).EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                r.ExistingDbContext<JobServiceSagaDbContext>();
                r.UsePostgres();
            });
            x.AddConsumers(filter => !filter.Name.Contains("sqs", StringComparison.InvariantCultureIgnoreCase), assemblies);
            foreach (var module in moduleInstances)
            {
                module.ConfigureModuleBus(x);
            }
            x.AddDelayedMessageScheduler();
            x.UsingInMemory((context, cfg) =>
            {
                cfg.UseDelayedMessageScheduler();
                cfg.ConfigureEndpoints(context);
            });
            /*x.AddEntityFrameworkOutbox<OrchestrationDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });*/


            x.ConfigureHealthCheckOptions(options =>
            {
                options.Name = "module-bus";
            });
        });

        builder.Services.AddMassTransit<IExternalBus>(x =>
        {
            x.AddConsumers(filter => filter.Name.Contains("sqs", StringComparison.InvariantCultureIgnoreCase), assemblies);
            
            var region = builder.Configuration.GetAWSOptions().Region?.SystemName;
            
            if (string.IsNullOrEmpty(region))
            {
                x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
            }
            else
            {
                x.UsingAmazonSqs((context, cfg) =>
                {
                    cfg.Host(region, h => { });
                    cfg.ConfigureEndpoints(context);
                });
            }

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
