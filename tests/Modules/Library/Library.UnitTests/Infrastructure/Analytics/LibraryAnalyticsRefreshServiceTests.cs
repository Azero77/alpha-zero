using AlphaZero.Modules.Library.Infrastructure.Analytics;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AlphaZero.Modules.Library.UnitTests.Infrastructure.Analytics;

public class LibraryAnalyticsRefreshServiceTests
{
    [Fact]
    public void ResolveIntervalSeconds_Should_ReturnConfiguredValue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LibraryAnalytics:RefreshIntervalSeconds"] = "43200"
            })
            .Build();

        var interval = LibraryAnalyticsRefreshService.ResolveIntervalSeconds(config);

        interval.Should().Be(43200);
    }

    [Fact]
    public void ResolveIntervalSeconds_Should_ReturnDefault86400_WhenNotConfigured()
    {
        var config = new ConfigurationBuilder().Build();

        var interval = LibraryAnalyticsRefreshService.ResolveIntervalSeconds(config);

        interval.Should().Be(86400);
    }

    [Fact]
    public void ResolveIntervalSeconds_Should_ReturnDefault86400_WhenInvalid()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LibraryAnalytics:RefreshIntervalSeconds"] = "-100"
            })
            .Build();

        var interval = LibraryAnalyticsRefreshService.ResolveIntervalSeconds(config);

        interval.Should().Be(86400);
    }
}
