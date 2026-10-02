using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Modules.Tenants.Infrastructure.Consumers;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AlphaZero.Modules.Tenants.UnitTests.Infrastructure;

public class TenantBrandingUpdatedConsumerTests
{
    private readonly ILogger<TenantBrandingUpdatedConsumer> _logger = Substitute.For<ILogger<TenantBrandingUpdatedConsumer>>();

    [Fact]
    public async Task Handle_WhenCacheAvailable_ShouldInvalidateCacheEntries()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        var consumer = new TenantBrandingUpdatedConsumer(_logger, cache);
        var branding = TenantBranding.Create("#059669", "#F97316").Value;
        var domainEvent = new TenantBrandingUpdatedDomainEvent(Guid.NewGuid(), "Harvard", branding);

        // Act
        await consumer.Handle(domainEvent, CancellationToken.None);

        // Assert
        await cache.Received(1).RemoveAsync("tenant:subdomain:harvard", Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveByTagAsync("tenant-harvard", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCacheThrows_ShouldCatchGracefully()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        cache.When(c => c.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Cache service failure"));

        var consumer = new TenantBrandingUpdatedConsumer(_logger, cache);
        var branding = TenantBranding.Default;
        var domainEvent = new TenantBrandingUpdatedDomainEvent(Guid.NewGuid(), "mit", branding);

        // Act
        var act = () => consumer.Handle(domainEvent, CancellationToken.None);

        // Assert - should catch and log warning, not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenCacheIsNull_ShouldCompleteSuccessfully()
    {
        // Arrange
        var consumer = new TenantBrandingUpdatedConsumer(_logger, cache: null);
        var branding = TenantBranding.Default;
        var domainEvent = new TenantBrandingUpdatedDomainEvent(Guid.NewGuid(), "oxford", branding);

        // Act
        var act = () => consumer.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Consume_ShouldInvalidateCacheEntries()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        var consumer = new TenantBrandingUpdatedConsumer(_logger, cache);
        var branding = TenantBranding.Create("#2563EB", "#38BDF8").Value;
        var domainEvent = new TenantBrandingUpdatedDomainEvent(Guid.NewGuid(), "stanford", branding);

        var context = Substitute.For<ConsumeContext<TenantBrandingUpdatedDomainEvent>>();
        context.Message.Returns(domainEvent);
        context.CancellationToken.Returns(CancellationToken.None);

        // Act
        await consumer.Consume(context);

        // Assert
        await cache.Received(1).RemoveAsync("tenant:subdomain:stanford", Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveByTagAsync("tenant-stanford", Arg.Any<CancellationToken>());
    }
}
