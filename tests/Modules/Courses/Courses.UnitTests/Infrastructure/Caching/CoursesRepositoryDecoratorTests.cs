using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.ValueObjects;
using AlphaZero.Modules.Courses.Infrastructure.Caching;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Repositores;
using FluentAssertions;
using NSubstitute;
using Xunit;
using SubjectAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Caching;

/// <summary>
/// Verifies the write-side decorators emit exactly the tag families the cached read surfaces
/// carry, and that reads pass through without touching the eviction queue.
/// </summary>
public class CoursesRepositoryDecoratorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // ── Course ──

    [Fact]
    public void Course_Writes_EvictCourseAndCatalogTags()
    {
        var course = NewCourse();
        var inner = Substitute.For<ICourseRepository>();
        var eviction = Substitute.For<IEvictionProvider>();
        var sut = new CachingCourseRepositoryDecorator(inner, eviction);

        sut.Add(course);
        sut.Update(course);
        sut.Remove(course);

        eviction.Received(3).EvictTag(CourseTag(course.Id));
        eviction.Received(3).EvictTag(CatalogTag());
        inner.Received(1).Add(course);
        inner.Received(1).Update(course);
        inner.Received(1).Remove(course);
    }

    [Fact]
    public async Task Course_Reads_DoNotEvict()
    {
        var inner = Substitute.For<ICourseRepository>();
        var eviction = Substitute.For<IEvictionProvider>();
        var sut = new CachingCourseRepositoryDecorator(inner, eviction);

        await sut.GetById(Guid.NewGuid());
        await sut.GetAll();
        sut.Query();
        sut.Query(Guid.NewGuid());
        await sut.IsAssetInUseAsync(Guid.NewGuid());

        eviction.DidNotReceive().Evict(Arg.Any<CacheKey>());
        eviction.DidNotReceive().EvictTag(Arg.Any<CacheTag>());
    }

    // ── Course overview ──

    [Fact]
    public void CourseOverview_Writes_EvictCourseTagOnly()
    {
        var courseId = Guid.NewGuid();
        var overview = CourseOverview.Create(
            Guid.NewGuid(), TenantId, courseId, RichText.Create("{}").Value, null, null, null);
        var inner = Substitute.For<ICourseOverviewRepository>();
        var eviction = Substitute.For<IEvictionProvider>();
        var sut = new CachingCourseOverviewRepositoryDecorator(inner, eviction);

        sut.Add(overview);
        sut.Update(overview);
        sut.Remove(overview);

        eviction.Received(3).EvictTag(CourseTag(courseId));
        inner.Received(1).Add(overview);
        inner.Received(1).Update(overview);
        inner.Received(1).Remove(overview);
    }

    // ── Course assets ──

    [Fact]
    public void CourseAsset_Writes_EvictPerCourseTag()
    {
        var courseId = Guid.NewGuid();
        var asset = NewVideoAsset(courseId);
        var inner = Substitute.For<IRepository<CourseAsset>>();
        var eviction = Substitute.For<IEvictionProvider>();
        var sut = new CachingCourseAssetRepositoryDecorator(inner, eviction);

        sut.Add(asset);
        sut.Update(asset);
        sut.Remove(asset);

        eviction.Received(3).EvictTag(CourseTag(courseId));
        inner.Received(1).Update(asset);
    }

    [Fact]
    public void CourseAsset_WithoutCourse_EvictsTenantWideAssetFamily()
    {
        var asset = NewVideoAsset(Guid.Empty);
        var inner = Substitute.For<IRepository<CourseAsset>>();
        var eviction = Substitute.For<IEvictionProvider>();
        var sut = new CachingCourseAssetRepositoryDecorator(inner, eviction);

        sut.Update(asset);

        eviction.Received(1).EvictTag(AssetsTag());
    }

    // ── Subject ──

    [Fact]
    public void Subject_Writes_EvictSubjectCatalogAndViewFamilies()
    {
        var subject = SubjectAggregate.Create(Guid.NewGuid(), TenantId, "Math", null).Value;
        var inner = Substitute.For<ISubjectRepository>();
        var eviction = Substitute.For<IEvictionProvider>();
        var sut = new CachingSubjectRepositoryDecorator(inner, eviction);

        sut.Add(subject);
        sut.Update(subject);
        sut.Remove(subject);

        eviction.Received(3).EvictTag(SubjectsTag());
        eviction.Received(3).EvictTag(CatalogTag());
        eviction.Received(3).EvictTag(ViewsTag());
        inner.Received(1).Update(subject);
    }

    // ── helpers ──

    private static Course NewCourse() =>
        Course.Create(Guid.NewGuid(), TenantId, "Cache Decorator Course", null, Guid.NewGuid()).Value;

    private static VideoCourseAsset NewVideoAsset(Guid courseId) =>
        VideoCourseAsset.Create(
            Guid.NewGuid(), TenantId,
            ResourceArn.ForVideo(TenantId, Guid.NewGuid()),
            "Decorator Video", courseId).Value;

    private static CacheTag CourseTag(Guid courseId) =>
        CacheScope.ForTenant(TenantId).Tag("courses:course", courseId.ToString("N"));

    private static CacheTag CatalogTag() =>
        CacheScope.ForTenant(TenantId).Tag("courses:catalog");

    private static CacheTag AssetsTag() =>
        CacheScope.ForTenant(TenantId).Tag("courses:assets");

    private static CacheTag SubjectsTag() =>
        CacheScope.ForTenant(TenantId).Tag("courses:subjects");

    private static CacheTag ViewsTag() =>
        CacheScope.ForTenant(TenantId).Tag("courses:views");
}
