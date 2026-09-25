using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Tenats;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Identity.UnitTests;

public class IAMPreprocessorInvestigationTests
{
    [Fact]
    public void Test_TenantProvider_During_Active_Request()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder().Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        services.AddSharedInfrastructure(config, env);

        var builder = new ContainerBuilder();
        builder.Populate(services);
        var root = builder.Build();

        // 1. Resolve tenantProvider from root (as FastEndpoints does for singleton preprocessors)
        var tenantProvider = root.Resolve<ITenantProvider>();

        // 2. Now an HTTP request arrives:
        var httpContextAccessor = root.Resolve<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        var tenantId = Guid.NewGuid();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tid", tenantId.ToString())
        }, "TestAuth"));
        httpContext.Request.Headers["X-TenantId"] = tenantId.ToString();

        httpContextAccessor.HttpContext = httpContext;

        var result = tenantProvider.GetTenant();
        Console.WriteLine($"tenantProvider.GetTenant() = {result}");
        Assert.Equal(tenantId, result);
    }
}
