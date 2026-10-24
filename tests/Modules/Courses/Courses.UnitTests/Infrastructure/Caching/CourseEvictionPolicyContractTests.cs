using AlphaZero.Modules.Courses.Infrastructure.Caching;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Tenats;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Caching;

/// <summary>
/// Guards the eviction policy's coverage contract: every entity type the Courses model persists
/// is either handled by <see cref="CourseEvictionPolicy"/> or explicitly listed as not feeding a
/// cached projection. A new cached surface or a new entity type must make a deliberate choice.
/// </summary>
public class CourseEvictionPolicyContractTests
{
    /// <summary>Entities that never feed a cached read: enrollment/saga/analytics/outbox rows.</summary>
    private static readonly HashSet<string> UncachedModelTypes =
    [
        nameof(AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.Enrollment),
        nameof(AlphaZero.Modules.Courses.Infrastructure.CourseAnalytics),
        "CourseRedemptionState",
        "CourseRevocationState",
        "InboxState",
        "OutboxMessage",
        "OutboxState",
        "OutboxMessageConsumer"
    ];

    [Fact]
    public void EveryPersistedEntity_IsHandledOrExplicitlyUncached()
    {
        using var context = CreateContext();
        var policy = new CourseEvictionPolicy();

        var uncovered = context.Model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned())
            .Select(entityType => entityType.ClrType)
            .Distinct()
            .Where(type => !policy.Handles(type) && !UncachedModelTypes.Contains(type.Name))
            .Select(type => type.FullName)
            .ToArray();

        uncovered.Should().BeEmpty(
            "every Courses entity must either evict cache tags or be listed as uncached; add the type to " +
            nameof(CourseEvictionPolicy) + " or to " + nameof(UncachedModelTypes) + ".");
    }

    [Theory]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.Course))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.CourseSection))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.CoursePlan))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.CurriculumItem))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.CourseOverview))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.VideoCourseAsset))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.DocumentCourseAsset))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.AssessmentCourseAsset))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.VideoCurriculumResource))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.DocumentCurriculumResource))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.ArticleCurriculumResource))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.AssessmentCurriculumResource))]
    [InlineData(typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject))]
    public void CacheFeedingEntityTypes_AreHandled(Type entityType)
    {
        new CourseEvictionPolicy().Handles(entityType).Should().BeTrue();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=cache_policy_contract;Username=test;Password=test")
            .Options;

        return new AppDbContext(options, Substitute.For<ITenantProvider>());
    }
}
