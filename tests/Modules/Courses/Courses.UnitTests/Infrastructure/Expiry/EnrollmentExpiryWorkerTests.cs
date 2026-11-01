using AlphaZero.Modules.Courses.Infrastructure.Expiry;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Tenats;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Expiry;

public class EnrollmentExpiryWorkerTests(ITestOutputHelper output)
{
    public sealed record ExpiredRow(Guid Id, Guid TenantId, Guid StudentId, Guid CourseId);

    [Fact]
    public void Test_SqlQueryRaw_GeneratedSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=postgres;Password=test")
            .Options;
        var tenant = Substitute.For<ITenantProvider>();
        using var ctx = new AppDbContext(options, tenant);

        var query = ctx.Database.SqlQueryRaw<ExpiredRow>(
            """
            WITH updated AS (
                UPDATE "Courses"."Enrollements"
                SET "Status" = 'Expired'
                WHERE "Status" = 'Active'
                RETURNING "Id", "TenantId", "StudentId", "CourseId"
            )
            SELECT u."Id", u."TenantId", u."StudentId", u."CourseId" FROM updated u;
            """);

        var sql = query.ToQueryString();
        output.WriteLine("GENERATED SQL:\n" + sql);
        Assert.NotNull(sql);
    }
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
