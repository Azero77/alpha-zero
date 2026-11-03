using AlphaZero.Modules.Courses.Infrastructure.Analytics;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Analytics;

public class CourseAnalyticsRefreshWorkerTests
{
    [Fact]
    public void ResolveIntervalSeconds_Should_ReturnConfiguredValue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Analytics:RefreshIntervalSeconds"] = "43200"
            })
            .Build();

        var interval = CourseAnalyticsRefreshService.ResolveIntervalSeconds(config);

        interval.Should().Be(43200);
    }

    [Fact]
    public void ResolveIntervalSeconds_Should_ReturnDefault86400_WhenNotConfigured()
    {
        var config = new ConfigurationBuilder().Build();

        var interval = CourseAnalyticsRefreshService.ResolveIntervalSeconds(config);

        interval.Should().Be(86400);
    }

    [Fact]
    public void ResolveIntervalSeconds_Should_ReturnDefault86400_WhenInvalid()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Analytics:RefreshIntervalSeconds"] = "-100"
            })
            .Build();

        var interval = CourseAnalyticsRefreshService.ResolveIntervalSeconds(config);

        interval.Should().Be(86400);
    }
}
