using AlphaZero.Modules.Identity.Application.Auth.Queries.GenerateDocumentToken;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using Identity.Tests.Integration.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Identity.Tests.Integration;

public class GenerateDocumentTokenTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public GenerateDocumentTokenTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GenerateDocumentToken_Should_ReturnValidToken_When_Authorized()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var scope = "course/c123";

        // Setup test services: Fake tenant provider and fake policy evaluator
        var fakeTenantProvider = new FakeTenantProvider(tenantId);
        var fakeEvaluator = new FakePolicyEvaluatorService { ResultToReturn = Result.Success };

        using var testScope = _factory.Services.CreateScope();
        
        // We override the services for this specific test scope if possible, 
        // but MediatR handlers might resolve from the root container in integration tests 
        // if they are scoped. Let's construct the handler directly to isolate it properly 
        // with our fakes, or resolve from scope.
        // It's easier to just test the Handler directly for this integration test, 
        // resolving real dependencies except the faked ones.
        
        var config = testScope.ServiceProvider.GetRequiredService<IConfiguration>();
        var authContextFactory = testScope.ServiceProvider.GetRequiredService<IAuthorizationContextFactory>();
        var clock = testScope.ServiceProvider.GetRequiredService<IClock>();

        // Set config for test
        var configBuilder = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"DOCUMENT_HMAC_SECRET", "test-secret-key-12345"}
        });
        var testConfig = configBuilder.Build();

        var handler = new GenerateDocumentTokenQueryHandler(
            fakeTenantProvider,
            authContextFactory,
            fakeEvaluator,
            testConfig,
            clock);

        var query = new GenerateDocumentTokenQuery(scope);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Token.Should().NotBeNullOrEmpty();
        
        var tokenParts = result.Value.Token.Split('.');
        tokenParts.Length.Should().Be(2);

        var payloadBase64 = tokenParts[0];
        var signature = tokenParts[1];

        // Decode payload
        var base64 = payloadBase64.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        payload.Should().NotBeNull();
        payload!["path"].ToString().Should().Be($"/documents/{tenantId}/{scope}/");
        payload.ContainsKey("exp").Should().BeTrue();

        // Verify signature
        var keyBytes = Encoding.UTF8.GetBytes("test-secret-key-12345");
        var textBytes = Encoding.UTF8.GetBytes(payloadBase64);
        using var hmac = new HMACSHA256(keyBytes);
        var expectedSignature = Convert.ToHexString(hmac.ComputeHash(textBytes)).ToLowerInvariant();
        
        signature.Should().Be(expectedSignature);
    }

    [Fact]
    public async Task GenerateDocumentToken_Should_ReturnForbidden_When_NotAuthorized()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var scope = "course/c123";

        var fakeTenantProvider = new FakeTenantProvider(tenantId);
        var fakeEvaluator = new FakePolicyEvaluatorService { ResultToReturn = Error.Forbidden("Access.Denied", "Denied") };

        using var testScope = _factory.Services.CreateScope();
        
        var authContextFactory = testScope.ServiceProvider.GetRequiredService<IAuthorizationContextFactory>();
        var clock = testScope.ServiceProvider.GetRequiredService<IClock>();
        
        var configBuilder = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"DOCUMENT_HMAC_SECRET", "test-secret-key-12345"}
        });
        var testConfig = configBuilder.Build();

        var handler = new GenerateDocumentTokenQueryHandler(
            fakeTenantProvider,
            authContextFactory,
            fakeEvaluator,
            testConfig,
            clock);

        var query = new GenerateDocumentTokenQuery(scope);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    public class FakeTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public FakeTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid? GetTenant() => _tenantId;
    }

    public class FakePolicyEvaluatorService : IPolicyEvaluatorService
    {
        public ErrorOr<Success> ResultToReturn { get; set; } = Result.Success;
        public Task<ErrorOr<Success>> Authorize(AuthorizationContext context) => Task.FromResult(ResultToReturn);
    }
}
