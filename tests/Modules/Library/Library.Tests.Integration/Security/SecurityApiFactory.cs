using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using ErrorOr;
using Library.Tests.Integration.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Library.Tests.Integration.Security;

public class FakePersonaPolicyEvaluator(IHttpContextAccessor httpContextAccessor) : IPolicyEvaluatorService
{
    public Task<ErrorOr<Success>> Authorize(AuthorizationContext context)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var persona = httpContext?.Request.Headers["X-Persona"].ToString();

        var allowedPermissions = persona switch
        {
            "Worker" => Array.Empty<string>(),
            "Accountant" => new[] { "library:Audit" },
            "Manager" => new[] { "library:SellCodes", "library:Audit" },
            "Admin" => new[] { "library:GenerateCodes", "library:SellCodes", "library:Audit" },
            _ => new[] { "library:GenerateCodes", "library:SellCodes", "library:Audit", "courses:Enroll", "library:AttachCourses" }
        };

        if (context.RequiredPermission != null && !allowedPermissions.Contains(context.RequiredPermission))
        {
            return Task.FromResult<ErrorOr<Success>>(
                Error.Forbidden("Access.Denied", $"Persona '{persona}' lacks permission '{context.RequiredPermission}'"));
        }

        // Library scoping for Manager persona:
        // A Manager is scoped to their own library (passed via X-Scoped-Library-Id).
        // If the request targets a specific library (e.g. az:library:{tenant}:library/{id}),
        // and it doesn't match the Manager's scoped library, deny with 403 Forbidden.
        if (persona == "Manager" && httpContext?.Request.Headers.TryGetValue("X-Scoped-Library-Id", out var scopedLib) == true)
        {
            if (Guid.TryParse(scopedLib, out var scopedLibId))
            {
                var targetArn = ResourceArn.Create(context.ResourceType, context.TenantId?.ToString() ?? ResourceArn.GlobalTenant, context.ResourcePath);
                if (!targetArn.IsError)
                {
                    var targetLibId = targetArn.Value.ExtractResourceId();
                    if (targetLibId.HasValue && targetLibId.Value != scopedLibId)
                    {
                        return Task.FromResult<ErrorOr<Success>>(
                            Error.Forbidden("Access.Denied", "Manager is not authorized for this library."));
                    }
                }
            }
        }

        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}

public class SecurityApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Testing:BypassIAM", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPolicyEvaluatorService>();
            services.AddScoped<IPolicyEvaluatorService, FakePersonaPolicyEvaluator>();
        });
    }
}

[CollectionDefinition("LibrarySecurityIntegration")]
public class SecurityIntegrationCollection : ICollectionFixture<SecurityApiFactory> { }
