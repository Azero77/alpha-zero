using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateAdminCode;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.VoidCode;
using AlphaZero.Shared.Authorization;
using ErrorOr;
using FluentAssertions;
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

[Collection("LibrarySecurityIntegration")]
public class PermissionWiringTests : IClassFixture<SecurityApiFactory>
{
    private readonly SecurityApiFactory _factory;
    private readonly HttpClient _client;

    public PermissionWiringTests(SecurityApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, string persona, Guid tenantId, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-TenantId", tenantId.ToString());
        request.Headers.Add("X-Persona", persona);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }
        return request;
    }

    [Fact]
    public async Task VoidCode_Worker_Should_Return403Forbidden()
    {
        // Arrange: Mint an admin code first as Admin
        var tenantId = Guid.NewGuid();
        var courseArn = $"az:course:{tenantId}:course/void-worker-test";

        var mintReq = CreateRequest(HttpMethod.Post, "/library/admin/access-codes/generate-single", "Admin", tenantId,
            new GenerateAdminCodeRequest { TargetResourceArn = courseArn });
        var mintRes = await _client.SendAsync(mintReq);
        mintRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var rawCode = (await mintRes.Content.ReadFromJsonAsync<GenerateAdminCodeResponse>())!.PlainTextCode;

        // Act: Worker attempts to void the valid code
        var voidReq = CreateRequest(HttpMethod.Post, "/library/access-codes/void", "Worker", tenantId,
            new VoidCodeRequest { RawCode = rawCode, Reason = "Worker void attempt" });
        var voidRes = await _client.SendAsync(voidReq);

        // Assert: Worker has no sell/void permissions
        voidRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VoidCode_Accountant_Should_Return403Forbidden()
    {
        // Arrange: Mint an admin code first as Admin
        var tenantId = Guid.NewGuid();
        var courseArn = $"az:course:{tenantId}:course/void-accountant-test";

        var mintReq = CreateRequest(HttpMethod.Post, "/library/admin/access-codes/generate-single", "Admin", tenantId,
            new GenerateAdminCodeRequest { TargetResourceArn = courseArn });
        var mintRes = await _client.SendAsync(mintReq);
        mintRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var rawCode = (await mintRes.Content.ReadFromJsonAsync<GenerateAdminCodeResponse>())!.PlainTextCode;

        // Act: Accountant has library:Audit only, attempts to void the code
        var voidReq = CreateRequest(HttpMethod.Post, "/library/access-codes/void", "Accountant", tenantId,
            new VoidCodeRequest { RawCode = rawCode, Reason = "Accountant audit check" });
        var voidRes = await _client.SendAsync(voidReq);

        // Assert: Accountant cannot void codes (requires library:SellCodes)
        voidRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VoidCode_Manager_Should_SucceedWith204()
    {
        // Arrange: Mint an admin code first as Admin
        var tenantId = Guid.NewGuid();
        var courseArn = $"az:course:{tenantId}:course/void-sec-test";

        var mintReq = CreateRequest(HttpMethod.Post, "/library/admin/access-codes/generate-single", "Admin", tenantId,
            new GenerateAdminCodeRequest { TargetResourceArn = courseArn });
        var mintRes = await _client.SendAsync(mintReq);
        mintRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var rawCode = (await mintRes.Content.ReadFromJsonAsync<GenerateAdminCodeResponse>())!.PlainTextCode;

        // Act: Manager (has library:SellCodes) voids the code
        var voidReq = CreateRequest(HttpMethod.Post, "/library/access-codes/void", "Manager", tenantId,
            new VoidCodeRequest { RawCode = rawCode, Reason = "Damaged during handling" });
        var voidRes = await _client.SendAsync(voidReq);

        // Assert: Manager can void codes
        voidRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GenerateAdminCode_Worker_Should_Return403Forbidden()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseArn = $"az:course:{tenantId}:course/mint-worker-test";
        var request = CreateRequest(HttpMethod.Post, "/library/admin/access-codes/generate-single", "Worker", tenantId,
            new GenerateAdminCodeRequest { TargetResourceArn = courseArn });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Worker has no mint permissions
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GenerateAdminCode_Accountant_Should_Return403Forbidden()
    {
        // Arrange: Accountant has library:Audit only, must NOT be able to mint admin codes (requires library:GenerateCodes)
        var tenantId = Guid.NewGuid();
        var courseArn = $"az:course:{tenantId}:course/mint-accountant-test";
        var request = CreateRequest(HttpMethod.Post, "/library/admin/access-codes/generate-single", "Accountant", tenantId,
            new GenerateAdminCodeRequest { TargetResourceArn = courseArn });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Accountant cannot mint admin codes
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GenerateAdminCode_Admin_Should_SucceedWith201()
    {
        // Arrange: Admin has library:GenerateCodes
        var tenantId = Guid.NewGuid();
        var courseArn = $"az:course:{tenantId}:course/mint-admin-test";
        var request = CreateRequest(HttpMethod.Post, "/library/admin/access-codes/generate-single", "Admin", tenantId,
            new GenerateAdminCodeRequest { TargetResourceArn = courseArn });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Admin can generate admin codes
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<GenerateAdminCodeResponse>();
        body.Should().NotBeNull();
        body!.PlainTextCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ListLibraries_Accountant_Should_SucceedWith200_While_Worker_Returns403()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act 1: Worker has no audit read permission
        var workerReq = CreateRequest(HttpMethod.Get, "/library/libraries", "Worker", tenantId);
        var workerRes = await _client.SendAsync(workerReq);

        // Act 2: Accountant has library:Audit permission
        var accountantReq = CreateRequest(HttpMethod.Get, "/library/libraries", "Accountant", tenantId);
        var accountantRes = await _client.SendAsync(accountantReq);

        // Assert
        workerRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        accountantRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
