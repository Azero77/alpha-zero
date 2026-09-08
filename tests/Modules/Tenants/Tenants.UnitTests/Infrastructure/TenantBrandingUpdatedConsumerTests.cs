using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Modules.Tenants.Infrastructure.Consumers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NSubstitute;
using System.Net;
using Xunit;

namespace AlphaZero.Modules.Tenants.UnitTests.Infrastructure;

public class TenantBrandingUpdatedConsumerTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
    private readonly ILogger<TenantBrandingUpdatedConsumer> _logger = Substitute.For<ILogger<TenantBrandingUpdatedConsumer>>();

    [Fact]
    public async Task Handle_ShouldSendRevalidationWebhook()
    {
        // Arrange
        _configuration["Frontend:BaseUrl"].Returns("http://localhost:3000");
        _configuration["Frontend:RevalidationSecret"].Returns("test-secret");

        var handlerMock = new Mock<HttpMessageHandler>();
        HttpRequestMessage? capturedRequest = null;

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactory.CreateClient("NextJsRevalidation").Returns(httpClient);

        var consumer = new TenantBrandingUpdatedConsumer(
            _httpClientFactory,
            _configuration,
            _logger,
            cache: null);

        var branding = TenantBranding.Create("#059669", "#F97316").Value;
        var domainEvent = new TenantBrandingUpdatedDomainEvent(Guid.NewGuid(), "harvard", branding);

        // Act
        await consumer.Handle(domainEvent, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Contain("tag=tenant-harvard");
        capturedRequest.RequestUri!.ToString().Should().Contain("secret=test-secret");
        capturedRequest.Headers.GetValues("x-revalidate-secret").Should().Contain("test-secret");
    }

    [Fact]
    public async Task Handle_WhenNetworkFails_ShouldCatchGracefully()
    {
        // Arrange
        _configuration["Frontend:BaseUrl"].Returns("http://localhost:3000");
        _configuration["Frontend:RevalidationSecret"].Returns("test-secret");

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network down"));

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactory.CreateClient("NextJsRevalidation").Returns(httpClient);

        var consumer = new TenantBrandingUpdatedConsumer(
            _httpClientFactory,
            _configuration,
            _logger,
            cache: null);

        var branding = TenantBranding.Default;
        var domainEvent = new TenantBrandingUpdatedDomainEvent(Guid.NewGuid(), "mit", branding);

        // Act
        var act = () => consumer.Handle(domainEvent, CancellationToken.None);

        // Assert - does not throw
        await act.Should().NotThrowAsync();
    }
}
