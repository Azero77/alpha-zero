using AlphaZero.Modules.Courses.Infrastructure.Expiry;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Expiry;

public class EnrollmentExpiryWorkerTests
{
    [Fact]
    public void ResolveIntervalSeconds_Should_ReturnConfiguredValue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Enrollment:ExpiryIntervalSeconds"] = "1800"
            })
            .Build();

        var interval = EnrollmentExpiryService.ResolveIntervalSeconds(config);

        interval.Should().Be(1800);
    }

    [Fact]
    public void ResolveIntervalSeconds_Should_ReturnDefault3600_WhenNotConfigured()
    {
        var config = new ConfigurationBuilder().Build();

        var interval = EnrollmentExpiryService.ResolveIntervalSeconds(config);

        interval.Should().Be(3600);
    }

    [Fact]
    public void ResolveIntervalSeconds_Should_ReturnDefault3600_WhenInvalid()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Enrollment:ExpiryIntervalSeconds"] = "-50"
            })
            .Build();

        var interval = EnrollmentExpiryService.ResolveIntervalSeconds(config);

        interval.Should().Be(3600);
    }
}
