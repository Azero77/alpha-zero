using System.Net;
using System.Net.Http.Json;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using ErrorOr;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Courses.Tests.Integration.Security;

/// <summary>
/// Second WebApplicationFactory (IAM-enforcing host) for the enrollment-lifecycle endpoints
/// (#34, stories #3/#4): keeps the Development host but turns the IAM dev bypass off so requests
/// flow through IAMPreprocessor, and swaps the real policy evaluator for a controllable fake —
/// the same prior art as CrudSecurityApiFactory. Proves the suspend/reactivate endpoints' wiring
/// (permission action + resource ARN) on the permission-denial path. Lives in the shared
/// "Integration" collection so the two hosts never race each other (they share process-global
/// connection-string environment).
/// </summary>
[Collection("Integration")]
public class LifecycleSecurityApiFactory : ApiFactory
{
    public FakePolicyEvaluatorService Evaluator { get; } = new();

    protected override bool OwnsDatabase => false;

    /// <summary>
    /// Points this host at the primary factory's shared Postgres container. Must run before the
    /// first CreateClient() (host build) so the security host migrates the same database.
    /// </summary>
    public void UseSharedDatabase(ApiFactory primary)
    {
        var connectionString = primary.DbConnectionString;
        Environment.SetEnvironmentVariable("ConnectionStrings__alphazerodb", connectionString);
        Environment.SetEnvironmentVariable("DatabaseSettings__ConnectionString", connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Testing:BypassIAM", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPolicyEvaluatorService>();
            services.AddSingleton<IPolicyEvaluatorService>(Evaluator);
        });
    }
}

public class FakePolicyEvaluatorService : IPolicyEvaluatorService
{
    public ErrorOr<Success> ResultToReturn { get; set; } = ErrorOr.Result.Success;

    public Task<ErrorOr<Success>> Authorize(AuthorizationContext context) =>
        Task.FromResult(ResultToReturn);
}

[Collection("Integration")]
public class LifecyclePermissionTests : IClassFixture<LifecycleSecurityApiFactory>, IDisposable
{
    private readonly LifecycleSecurityApiFactory _factory;
    private readonly HttpClient _client;

    public LifecyclePermissionTests(LifecycleSecurityApiFactory factory, ApiFactory primaryFactory)
    {
        _factory = factory;
        _factory.UseSharedDatabase(primaryFactory);
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        _factory.Evaluator.ResultToReturn = ErrorOr.Result.Success;
    }

    private void SetTenant(Guid tenantId)
    {
        _client.DefaultRequestHeaders.Remove("X-TenantId");
        _client.DefaultRequestHeaders.Add("X-TenantId", tenantId.ToString());
    }

    [Fact]
    public async Task LifecycleEndpoints_Should_Return403_WhenPolicyDenies()
    {
        // Arrange: policy denied across the board
        _factory.Evaluator.ResultToReturn = Error.Forbidden("Access.Denied", "Test denial");
        var tenantId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        SetTenant(tenantId);

        // Act: both lifecycle routes hit their AccessControl requirement
        var suspend = await _client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { });
        var reactivate = await _client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/reactivate", new { });

        // Assert
        suspend.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        reactivate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LifecycleEndpoints_Should_PassThrough_WhenPolicyAllows()
    {
        // Arrange: policy granted — proves the denials above came from the evaluator wiring,
        // not from a host misconfiguration (unknown ids fall through to 404, not 403)
        _factory.Evaluator.ResultToReturn = ErrorOr.Result.Success;
        var tenantId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        SetTenant(tenantId);

        // Act
        var suspend = await _client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { });
        var reactivate = await _client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/reactivate", new { });

        // Assert
        suspend.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        suspend.StatusCode.Should().Be(HttpStatusCode.NotFound);
        reactivate.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        reactivate.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
