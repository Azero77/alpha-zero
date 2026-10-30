using AlphaZero.Shared.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Courses.UnitTests.Infrastructure.Caching;

public class SharedCachingRegistrationTests
{
    [Fact]
    public void AddSharedInfrastructure_ConfiguresHybridCacheEntryDefaults()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        services.AddSharedInfrastructure(new ConfigurationBuilder().Build(), environment);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HybridCacheOptions>>().Value;

        options.DefaultEntryOptions!.Expiration.Should().Be(TimeSpan.FromMinutes(10));
        options.DefaultEntryOptions!.LocalCacheExpiration.Should().Be(TimeSpan.FromMinutes(5));
    }
}
