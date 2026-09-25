using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Tenats;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Identity.UnitTests;

public class TestCommand : IRequest<Guid?> { }

public class TestCommandHandler : IRequestHandler<TestCommand, Guid?>
{
    private readonly ITenantProvider _tenantProvider;
    public TestCommandHandler(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public Task<Guid?> Handle(TestCommand request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[TestCommandHandler] TenantProvider type: {_tenantProvider?.GetType().FullName}");
        if (_tenantProvider is HttpTenantProvider httpProvider)
        {
            var field = typeof(HttpTenantProvider).GetField("_httpContextAccessor", BindingFlags.NonPublic | BindingFlags.Instance);
            var accessor = field?.GetValue(httpProvider) as IHttpContextAccessor;
            Console.WriteLine($"[TestCommandHandler] _httpContextAccessor is null? {accessor == null}");
            if (accessor != null)
            {
                Console.WriteLine($"[TestCommandHandler] accessor.HttpContext is null? {accessor.HttpContext == null}");
                if (accessor.HttpContext != null)
                {
                    Console.WriteLine($"[TestCommandHandler] Claims count: {accessor.HttpContext.User?.Claims?.Count()}");
                    foreach (var c in accessor.HttpContext.User.Claims)
                    {
                        Console.WriteLine($"[TestCommandHandler] Claim: {c.Type} = {c.Value}");
                    }
                    Console.WriteLine($"[TestCommandHandler] Header X-TenantId: {accessor.HttpContext.Request.Headers["X-TenantId"]}");
                }
            }
        }
        return Task.FromResult(_tenantProvider?.GetTenant());
    }
}

public class TestModule : AppModule
{
    public override void RegisterGlobal(IServiceCollection services)
    {
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        moduleServices.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(TestCommandHandler).Assembly));
    }
}

public class ActiveHttpRequestInvestigationTests
{
    [Fact]
    public async Task Test_Module_Send_During_Active_Http_Request()
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

        var module = new TestModule();
        module.Initialize(root);

        // Now simulate an active HTTP request:
        var httpContextAccessor = root.Resolve<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        var tenantId = Guid.NewGuid();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("TenantId", tenantId.ToString())
        }, "TestAuth"));
        httpContext.Request.Headers["X-TenantId"] = tenantId.ToString();

        httpContextAccessor.HttpContext = httpContext;

        Console.WriteLine($"[Main] Before Send: httpContextAccessor.HttpContext is null? {httpContextAccessor.HttpContext == null}");

        // Inside the active HTTP request, call module.Send:
        var resultTenantId = await module.Send(new TestCommand());

        Console.WriteLine($"[Main] Result TenantId: {resultTenantId}");
        Assert.Equal(tenantId, resultTenantId);
    }
}
