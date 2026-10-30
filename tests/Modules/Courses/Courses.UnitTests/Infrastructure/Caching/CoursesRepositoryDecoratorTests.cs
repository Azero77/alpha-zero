using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.ValueObjects;
using AlphaZero.Modules.Courses.Infrastructure.Caching;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.Tenats;
using FluentAssertions;
using NSubstitute;
using Xunit;
using SubjectAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Caching;

/// <summary>
/// spec-33 §2/§6 decorator contract:
///  * every write queues EXACTLY its eviction-matrix key set, proven by equality against the
///    CourseCacheKeys registry functions (no literal key or tag strings in this file);
///  * a subject write is additionally the ONLY EvictTag call site (the fan-out tag);
///  * read-through exists only on the overview DTO read (G11); aggregate GetById-shaped reads
///    delegate UNCACHED (G12); collection/expression reads bypass;
///  * an empty tenant, or a pool asset with CourseId == Guid.Empty, queues NOTHING and does not throw.
/// </summary>
public class CoursesRepositoryDecoratorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly CacheScope Scope = CacheScope.ForTenant(TenantId);

    // ── Course: writes queue the exact 8-key row ──

    [Fact]
    public void Course_Writes_QueueExactEvictCourseKeySet()
    {
        var course = NewCourse();
        var expected = CourseCacheKeys.EvictCourse(Scope, course.Id);
        expected.Should().HaveCount(8);

        var eviction = Substitute.For<IEvictionProvider>();
        var inner = Substitute.For<ICourseRepository>();
        var sut = new CachingCourseRepositoryDecorator(inner, eviction);

        sut.Add(course);
        sut.Update(course);
        sut.Remove(course);

        eviction.DidNotReceive().Evict(Arg.Any<CacheKey>());
        eviction.DidNotReceive().EvictTag(Arg.Any<CacheTag>());
        eviction.Received(3).Evict(Arg.Is<IEnumerable<CacheKey>>(keys => keys.SequenceEqual(expected)));

        inner.Received(1).Add(course);
        inner.Received(1).Update(course);
        inner.Received(1).Remove(course);
    }

    [Fact]
    public void Course_EmptyTenant_QueuesNothingAndDoesNotThrow()
    {
        var course = Course.Create(Guid.NewGuid(), Guid.Empty, "Tenantless Course", null, Guid.NewGuid()).Value;

        var eviction = Substitute.For<IEvictionProvider>();
        var sut = new CachingCourseRepositoryDecorator(Substitute.For<ICourseRepository>(), eviction);

        var write = () => sut.Update(course);
        write.Should().NotThrow();

        eviction.DidNotReceive().Evict(Arg.Any<CacheKey>());
        eviction.DidNotReceive().Evict(Arg.Any<IEnumerable<CacheKey>>());
        eviction.DidNotReceive().EvictTag(Arg.Any<CacheTag>());
    }

    // ── Course reads: G12 — aggregate GetById delegates UNCACHED ──

    [Fact]
    public async Task Course_Reads_DoNotEvict()
    {
        var eviction = Substitute.For<IEvictionProvider>();
        var inner = Substitute.For<ICourseRepository>();
        var sut = new CachingCourseRepositoryDecorator(inner, eviction);

        await sut.GetById(Guid.NewGuid());
        await sut.GetAll();
        sut.Query();
        sut.Query(Guid.NewGuid());
        await sut.IsAssetInUseAsync(Guid.NewGuid());

        eviction.DidNotReceive().Evict(Arg.Any<CacheKey>());
        eviction.DidNotReceive().Evict(Arg.Any<IEnumerable<CacheKey>>());
        eviction.DidNotReceive().EvictTag(Arg.Any<CacheTag>());
    }

    // ── Course overview writes + G11 read-through ──

    [Fact]
    public void CourseOverview_Writes_QueueExactEvictCourseOverviewKeySet()
    {
        var courseId = Guid.NewGuid();
        var overview = CourseOverview.Create(
            Guid.NewGuid(), TenantId, courseId, RichText.Create("{}").Value, null, null, null);
        var expected = CourseCacheKeys.EvictCourseOverview(Scope, courseId);
        expected.Should().HaveCount(6);

        var eviction = Substitute.For<IEvictionProvider>();
        var inner = Substitute.For<ICourseOverviewRepository>();
        var sut = new CachingCourseOverviewRepositoryDecorator(
            inner, eviction, Substitute.For<ICachingService>(), Substitute.For<ITenantProvider>());

        sut.Add(overview);
        sut.Update(overview);
        sut.Remove(overview);

        eviction.DidNotReceive().Evict(Arg.Any<CacheKey>());
        eviction.DidNotReceive().EvictTag(Arg.Any<CacheTag>());
        eviction.Received(3).Evict(Arg.Is<IEnumerable<CacheKey>>(keys => keys.SequenceEqual(expected)));

        inner.Received(1).Add(overview);
        inner.Received(1).Update(overview);
        inner.Received(1).Remove(overview);
    }

    [Fact]
    public async Task CourseOverview_ReadThrough_MissFillsOnce_HitServesWithoutInner()
    {
        var courseId = Guid.NewGuid();
        var fakeCache = new FakeCachingService();
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.GetTenant().Returns(TenantId);
        var inner = Substitute.For<ICourseOverviewRepository>();
        var dto = new CourseOverviewDto(Guid.NewGuid(), courseId, "Draft", "{}", null, null, null, "etag");
        inner.GetByCourseIdAsync(courseId, Arg.Any<CancellationToken>()).Returns(dto);
        var sut = new CachingCourseOverviewRepositoryDecorator(inner, Substitute.For<IEvictionProvider>(), fakeCache, tenantProvider);

        // G11: the read-through is keyed by the CourseOverviewEntity registry function.
        var expectedKey = CourseCacheKeys.CourseOverviewEntity(Scope, courseId);

        var miss = await sut.GetByCourseIdAsync(courseId);
        var hit = await sut.GetByCourseIdAsync(courseId);

        fakeCache.RequestedKeys.Should().Equal(expectedKey, expectedKey);
        fakeCache.Misses.Should().Be(1);
        await inner.Received(1).GetByCourseIdAsync(courseId, Arg.Any<CancellationToken>());
        miss.Should().Be(dto);
        hit.Should().Be(dto);
    }

    [Fact]
    public async Task CourseOverview_ReadThrough_EmptyTenant_BypassesCache()
    {
        var fakeCache = new FakeCachingService();
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.GetTenant().Returns((Guid?)null);
        var inner = Substitute.For<ICourseOverviewRepository>();
        var sut = new CachingCourseOverviewRepositoryDecorator(inner, Substitute.For<IEvictionProvider>(), fakeCache, tenantProvider);

        var courseId = Guid.NewGuid();
        var read = () => sut.GetByCourseIdAsync(courseId);

        await read.Should().NotThrowAsync();

        fakeCache.RequestedKeys.Should().BeEmpty();
        fakeCache.Misses.Should().Be(0);
        await inner.Received(1).GetByCourseIdAsync(courseId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CourseOverview_CollectionAndExpressionReads_Bypass()
    {
        var fakeCache = new FakeCachingService();
        var inner = Substitute.For<ICourseOverviewRepository>();
        var sut = new CachingCourseOverviewRepositoryDecorator(
            inner, Substitute.For<IEvictionProvider>(), fakeCache, Substitute.For<ITenantProvider>());

        await sut.GetById(Guid.NewGuid());
        await sut.GetAll();
        await sut.GetFirst(o => o.CourseId == Guid.NewGuid());
        await sut.Get(o => o.CourseId == Guid.NewGuid());
        sut.IgnoreQueryFilter();

        await inner.Received(1).GetById(Arg.Any<Guid>());
        await inner.Received(1).GetAll();
        fakeCache.RequestedKeys.Should().BeEmpty();
        fakeCache.Misses.Should().Be(0);
    }

    // ── Course assets: writes queue the asset row; pool assets queue nothing ──

    [Fact]
    public void CourseAsset_Writes_QueueExactEvictCourseAssetKeySet()
    {
        var courseId = Guid.NewGuid();
        var asset = NewVideoAsset(courseId);
        var expected = CourseCacheKeys.EvictCourseAsset(Scope, courseId, asset.Id);
        expected.Should().HaveCount(5);

        var eviction = Substitute.For<IEvictionProvider>();
        var inner = Substitute.For<IRepository<CourseAsset>>();
        var sut = new CachingCourseAssetRepositoryDecorator(inner, eviction);

        sut.Add(asset);
        sut.Update(asset);
        sut.Remove(asset);

        eviction.DidNotReceive().Evict(Arg.Any<CacheKey>());
        eviction.DidNotReceive().EvictTag(Arg.Any<CacheTag>());
        eviction.Received(3).Evict(Arg.Is<IEnumerable<CacheKey>>(keys => keys.SequenceEqual(expected)));
        inner.Received(1).Update(asset);
    }

    [Fact]
    public void CourseAsset_PoolAssetWithEmptyCourseId_QueuesNothingAndDoesNotThrow()
    {
        var asset = NewVideoAsset(Guid.Empty);

        var eviction = Substitute.For<IEvictionProvider>();
        var sut = new CachingCourseAssetRepositoryDecorator(Substitute.For<IRepository<CourseAsset>>(), eviction);

        var write = () => sut.Update(asset);
        write.Should().NotThrow();

        eviction.DidNotReceive().Evict(Arg.Any<CacheKey>());
        eviction.DidNotReceive().Evict(Arg.Any<IEnumerable<CacheKey>>());
        eviction.DidNotReceive().EvictTag(Arg.Any<CacheTag>());
    }

    [Fact]
    public void CourseAsset_EmptyTenant_QueuesNothingAndDoesNotThrow()
    {
        var asset = VideoCourseAsset.Create(
            Guid.NewGuid(), Guid.Empty,
            ResourceArn.ForVideo(Guid.Empty, Guid.NewGuid()),
            "Tenantless Video", Guid.NewGuid()).Value;

        var eviction = Substitute.For<IEvictionProvider>();
        var sut = new CachingCourseAssetRepositoryDecorator(Substitute.For<IRepository<CourseAsset>>(), eviction);

        var write = () => sut.Add(asset);
        write.Should().NotThrow();

        eviction.DidNotReceive().Evict(Arg.Any<CacheKey>());
        eviction.DidNotReceive().Evict(Arg.Any<IEnumerable<CacheKey>>());
        eviction.DidNotReceive().EvictTag(Arg.Any<CacheTag>());
    }

    // ── Subject: keys + the one fan-out tag ──

    [Fact]
    public void Subject_Writes_QueueExactEvictSubjectKeySet_PlusTheSingleFanOutTag()
    {
        var subject = SubjectAggregate.Create(Guid.NewGuid(), TenantId, "Math", null).Value;
        var expectedKeys = CourseCacheKeys.EvictSubject(Scope, subject.Id);
        expectedKeys.Should().HaveCount(2);
        var expectedTag = CourseCacheTags.CoursePublicViews(Scope);

        var eviction = Substitute.For<IEvictionProvider>();
        var inner = Substitute.For<ISubjectRepository>();
        var sut = new CachingSubjectRepositoryDecorator(inner, eviction);

        sut.Add(subject);
        sut.Update(subject);
        sut.Remove(subject);

        eviction.DidNotReceive().Evict(Arg.Any<CacheKey>());
        eviction.Received(3).Evict(Arg.Is<IEnumerable<CacheKey>>(keys => keys.SequenceEqual(expectedKeys)));
        // Fan-out: SubjectList + SubjectEntity keys, plus exactly EvictTag(courses:views) per
        // write — every cached view copy across the tenant dies with the rename.
        eviction.Received(3).EvictTag(expectedTag);
        eviction.Received(3).EvictTag(Arg.Any<CacheTag>());
    }

    [Fact]
    public void Subject_EmptyTenant_QueuesNothingAndDoesNotThrow()
    {
        var subject = SubjectAggregate.Create(Guid.NewGuid(), Guid.Empty, "Tenantless", null).Value;

        var eviction = Substitute.For<IEvictionProvider>();
        var sut = new CachingSubjectRepositoryDecorator(Substitute.For<ISubjectRepository>(), eviction);

        var write = () => sut.Remove(subject);
        write.Should().NotThrow();

        eviction.DidNotReceive().Evict(Arg.Any<CacheKey>());
        eviction.DidNotReceive().Evict(Arg.Any<IEnumerable<CacheKey>>());
        eviction.DidNotReceive().EvictTag(Arg.Any<CacheTag>());
    }

    // ── helpers ──

    private static Course NewCourse() =>
        Course.Create(Guid.NewGuid(), TenantId, "Cache Decorator Course", null, Guid.NewGuid()).Value;

    private static VideoCourseAsset NewVideoAsset(Guid courseId) =>
        VideoCourseAsset.Create(
            Guid.NewGuid(), TenantId,
            ResourceArn.ForVideo(TenantId, Guid.NewGuid()),
            "Decorator Video", courseId).Value;

    /// <summary>Real (non-substitute) caching stub: NSubstitute cannot substitute GetOrCreateAsync
    /// semantics (call-factory-on-miss) without behaving as a pass-through; the interceptor suite
    /// uses the same real-stub pattern for RemoveAsync/RemoveByTagAsync. Keys and hits are
    /// recorded so tests can assert key identity and read-through hit/miss behavior.</summary>
    private sealed class FakeCachingService : ICachingService
    {
        private readonly Dictionary<string, object?> _entries = [];

        public List<CacheKey> RequestedKeys { get; } = [];
        public int Misses { get; private set; }

        public async Task<T> GetOrCreateAsync<T>(
            CacheKey key,
            CachePolicy policy,
            IReadOnlyCollection<CacheTag> tags,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default)
        {
            RequestedKeys.Add(key);
            if (_entries.TryGetValue(key.Value, out var cached) && cached is { } hit && hit.GetType() == typeof(T))
                return (T)hit;

            Misses++;
            var value = await factory(cancellationToken);
            _entries[key.Value] = value;
            return value;
        }

        public Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key.Value);
            return Task.CompletedTask;
        }

        public Task SetAsync<T>(
            CacheKey key,
            T value,
            CachePolicy policy,
            IReadOnlyCollection<CacheTag> tags,
            CancellationToken cancellationToken = default)
        {
            _entries[key.Value] = value;
            return Task.CompletedTask;
        }

        public Task RemoveByTagAsync(CacheTag tag, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Evict(CacheKey key) { }

        public void Evict(IEnumerable<CacheKey> keys) { }

        public void Evict(CacheTag tag) { }

        public void Evict(IEnumerable<CacheTag> tags) { }
    }
}
