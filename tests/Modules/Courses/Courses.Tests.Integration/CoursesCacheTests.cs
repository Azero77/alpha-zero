using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourseCatalog;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;
using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetStudentDashboard;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
using AlphaZero.Modules.Courses.Domain.ValueObjects;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Items;
using AlphaZero.Modules.Courses.Presentation.Courses.Overview;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Dashboard;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Queries;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Courses.Tests.Integration;

/// <summary>
/// Issue #33 acceptance: cache effects proven via BEHAVIOR only (same response, freshness window,
/// tenant isolation) through the integration host — never through cache internals.
///
/// Stale-proof convention: a direct <see cref="BaseIntegrationTest.ExecuteDbContextAsync"/> write
/// deliberately bypasses the eviction-wired command pipeline; a read inside the TTL window must
/// still return the warmed response (shows the response was served from cache). A read after the
/// actual eviction-wired command must return the updated data (freshness — the acceptance bar).
/// </summary>
public class CoursesCacheTests : BaseIntegrationTest
{
    private static readonly JsonElement ItemContent =
        JsonDocument.Parse(@"[{""type"":""inline"",""tiptap"":{""type"":""doc""}}]").RootElement;

    public CoursesCacheTests(ApiFactory factory) : base(factory)
    {
    }

    // ── Catalog ──

    [Fact]
    public async Task Catalog_FirstReadFills_SecondReadServedFromCache_MutationRefreshes()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        var courseId = await CreateCourseByHttp("Cache Catalog Course", subjectId);

        // First read fills.
        var first = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses");
        first!.Items.Should().ContainSingle(c => c.Id == courseId);

        // Serve-from-cache proof: a direct DB write (no command, no eviction) stays invisible
        // behind the warmed response inside the TTL window.
        await ExecuteDbContextAsync(async db =>
        {
            db.Courses.Add(Course.Create(Guid.NewGuid(), tenantId, "Ghost Behind Cache Course", null, subjectId).Value);
            await db.SaveChangesAsync();
        });

        var stale = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses");
        stale!.Items.Should().HaveCount(first.Items.Count());
        stale.Items.Should().NotContain(c => c.Title == "Ghost Behind Cache Course");

        // Freshness: the eviction-wired create command makes the next read (still inside the
        // TTL window) return the new catalog state.
        var createResponse = await Client.PostAsJsonAsync("/courses",
            new CreateCourseRequest { Title = "Mutation Refresh Course", SubjectId = subjectId });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var fresh = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses");
        fresh!.Items.Should().Contain(c => c.Title == "Mutation Refresh Course");
        fresh.Items.Should().Contain(c => c.Id == courseId);
    }

    // ── Course tree view ──

    [Fact]
    public async Task CourseView_WarmsColdTree_IsServedFromCache_AndItemMutationRefreshes()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        var courseId = await CreateCourseByHttp("Cache Tree Course", subjectId);

        var sectionId = await AddSection(courseId);

        // First read fills: the tree has a section and no items.
        var first = await Client.GetFromJsonAsync<CourseSummaryTree>($"/courses/{courseId}");
        first!.Sections.Should().ContainSingle(s => s.Id == sectionId);
        first.Sections.Single().Items.Should().BeEmpty();

        // Serve-from-cache proof: aggregate-level direct write (bypasses commands/eviction).
        await ExecuteDbContextAsync(async db =>
        {
            var course = await db.Courses
                .Include(c => c.Sections)
                .FirstAsync(c => c.Id == courseId);
            var section = course.Sections.Single(s => s.Id == sectionId);
            var addItem = section.AddItem(Guid.NewGuid(), "Ghost Item Behind Cache", "Article", course.AllocateNextBitIndex());
            addItem.IsError.Should().BeFalse();
            await db.SaveChangesAsync();
        });

        var stale = await Client.GetFromJsonAsync<CourseSummaryTree>($"/courses/{courseId}");
        stale!.Sections.Single().Items.Should().BeEmpty();

        // Freshness: item creation through the eviction-wired command pipeline refreshes the tree.
        var itemResponse = await Client.PostAsJsonAsync(
            $"/courses/{courseId}/sections/{sectionId}/items",
            new CreateCourseSectionItemRequest { CourseId = courseId, SectionId = sectionId, Title = "Fresh Item", MainType = "Article", Content = ItemContent });
        itemResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var fresh = await Client.GetFromJsonAsync<CourseSummaryTree>($"/courses/{courseId}");
        fresh!.Sections.Single().Items.Should().ContainSingle(i => i.Title == "Fresh Item");
        // The ghost item is real committed data (direct write) — after eviction a cold refill
        // legitimately includes it; freshness means the mutation's data is visible.
    }

    // ── Course overview public shape ──
    //
    // NOTE (pre-existing defect observed on this base, reported on the owning issue — not
    // fixed here because it is outside #33's caching scope):
    //  * GetCourseOverviewAsync's LINQ projection (`o.DescriptionContent.Value` on a
    //    value-converted RichText property) materializes as null, so the response's
    //    descriptionContent is always null even when the DB row holds real content.
    // The cache still wraps this surface exactly as specified; the observable state here is the
    // ETag (a fresh GUID on every overview Update), which lets us prove both cache-serve and
    // tag-family eviction without depending on the broken field. Persistence of the overview
    // command itself is covered by CourseOverviewTests.
    [Fact]
    public async Task Overview_WarmsPublicShape_IsServedFromCache_AndCourseFamilyEvictionRefreshes()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        var courseId = await CreateCourseByHttp("Cache Overview Course", subjectId);

        // First read fills (the overview row is auto-seeded on course creation).
        var warm = await Client.GetAsync($"/courses/{courseId}/overview");
        warm.StatusCode.Should().Be(HttpStatusCode.OK);
        var warmTag = warm.Headers.ETag!.Tag;

        // Serve-from-cache proof: a direct DB write (new ETag in the row) stays invisible behind
        // the warmed response inside the TTL window.
        string? dbTag = null;
        await ExecuteDbContextAsync(async db =>
        {
            var row = await db.CourseOverviews.FirstAsync(o => o.CourseId == courseId);
            row.Update(RichText.Create(@"{""type"":""doc"",""content"":[]}").Value, null, null, null);
            await db.SaveChangesAsync();
            dbTag = row.ETag;
        });
        dbTag.Should().NotBe(warmTag);

        var stale = await Client.GetAsync($"/courses/{courseId}/overview");
        stale.Headers.ETag!.Tag.Should().Be(warmTag);

        // Freshness: a mutation in the course tag family (pricing plan) evicts the overview entry
        // too, so the next read (still inside the TTL window) reflects the changed DB row.
        var planResponse = await Client.PostAsJsonAsync($"/courses/{courseId}/plans",
            new AddPlanRequest { CourseId = courseId, Name = "Overview Eviction Plan", PrincipalId = Guid.NewGuid() });
        planResponse.EnsureSuccessStatusCode();

        var fresh = await Client.GetAsync($"/courses/{courseId}/overview");
        fresh.Headers.ETag!.Tag.Should().Be(dbTag);
    }

    // ── Readiness companion ──

    [Fact]
    public async Task Readiness_WarmsCompanion_IsServedFromCache_PlanMutationRefreshes()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        var courseId = await CreateCourseByHttp("Cache Readiness Course", subjectId);

        // First read fills; no pricing plan yet → the plan blocker is present.
        var first = await Client.GetFromJsonAsync<CourseReadiness>($"/courses/{courseId}/readiness");
        first!.BlockingReasons.Should().Contain("Course must have at least one active pricing plan.");

        // Serve-from-cache proof: direct plan write behind the cache's back.
        await ExecuteDbContextAsync(async db =>
        {
            db.CoursePlans.Add(CoursePlan.Create(courseId, "Ghost Plan Behind Cache", Guid.NewGuid()));
            await db.SaveChangesAsync();
        });

        var stale = await Client.GetFromJsonAsync<CourseReadiness>($"/courses/{courseId}/readiness");
        stale!.BlockingReasons.Should().Contain("Course must have at least one active pricing plan.");

        // Freshness: the eviction-wired AddPlan command refreshes the companion.
        var planResponse = await Client.PostAsJsonAsync($"/courses/{courseId}/plans",
            new AddPlanRequest { CourseId = courseId, Name = "Fresh Plan", PrincipalId = Guid.NewGuid() });
        planResponse.EnsureSuccessStatusCode();

        var fresh = await Client.GetFromJsonAsync<CourseReadiness>($"/courses/{courseId}/readiness");
        fresh!.BlockingReasons.Should().NotContain("Course must have at least one active pricing plan.");
    }

    // ── Tenant subject listing ──

    [Fact]
    public async Task Subjects_WarmListing_IsServedFromCache_CreateSubjectRefreshes()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var firstResponse = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "First Subject" });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // First read fills.
        var first = await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects");
        first!.Items.Should().ContainSingle(s => s.Name == "First Subject");

        // Serve-from-cache proof: direct subject write behind the cache's back.
        await ExecuteDbContextAsync(async db =>
        {
            db.Subjects.Add(Subject.Create(Guid.NewGuid(), tenantId, "Ghost Subject Behind Cache", null).Value);
            await db.SaveChangesAsync();
        });

        var stale = await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects");
        stale!.Items.Should().HaveCount(first.Items.Count());
        stale.Items.Should().NotContain(s => s.Name == "Ghost Subject Behind Cache");

        // Freshness: the eviction-wired CreateSubject command refreshes the listing.
        var createResponse = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "Fresh Subject" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var fresh = await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects");
        fresh!.Items.Should().Contain(s => s.Name == "Fresh Subject");
        fresh.Items.Should().Contain(s => s.Name == "First Subject");
    }

    // ── Default-shape cardinality: non-default requests bypass the cache ──

    [Fact]
    public async Task Catalog_NonDefaultPaging_BypassesCache_AndShowsDbStateImmediately()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        await CreateCourseByHttp("Default Shape Course", subjectId);

        var warm = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses");
        warm!.Items.Should().ContainSingle(c => c.Title == "Default Shape Course");

        await ExecuteDbContextAsync(async db =>
        {
            db.Courses.Add(Course.Create(Guid.NewGuid(), tenantId, "NonDefault Paging Ghost", null, subjectId).Value);
            await db.SaveChangesAsync();
        });

        // perPage=5 is not the cached default shape, so the read goes straight to the DB.
        var bypass = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses?perPage=5");
        bypass!.Items.Should().Contain(c => c.Title == "NonDefault Paging Ghost");

        // The default-shaped entry is untouched and still warm.
        var cached = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses");
        cached!.Items.Should().NotContain(c => c.Title == "NonDefault Paging Ghost");
    }

    // ── Subject dependencies: catalog and views embed the subject name ──

    [Fact]
    public async Task StudentCatalog_SearchBypassesCache_AndSubjectRenameRefreshesCatalogAndView()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        var courseId = await CreateCourseByHttp("Catalog Freshness Course", subjectId);
        await PublishCourseDirectly(courseId);

        // Warm both surfaces that embed the subject name.
        var catalogWarm = await Client.GetFromJsonAsync<PagedResult<CourseCatalogItemDto>>("/courses/catalog");
        catalogWarm!.Items.Should().ContainSingle(i => i.Id == courseId && i.SubjectName == "Cache Test Subject");
        var viewWarm = await Client.GetFromJsonAsync<CourseViewShim>($"/courses/{courseId}/view");
        viewWarm!.SubjectName.Should().Be("Cache Test Subject");

        // A caller-controlled search value never enters a key: the query is served cold.
        await ExecuteDbContextAsync(async db =>
        {
            var ghost = Course.Create(Guid.NewGuid(), tenantId, "Searchable Ghost", null, subjectId).Value;
            db.Courses.Add(ghost);
            db.Entry(ghost).Property(c => c.Status).CurrentValue = CourseStatus.Published;
            await db.SaveChangesAsync();
        });
        var searched = await Client.GetFromJsonAsync<PagedResult<CourseCatalogItemDto>>("/courses/catalog?search=Searchable");
        searched!.Items.Should().ContainSingle(i => i.Title == "Searchable Ghost");

        // Rename (spec-33 G4.1/G4.2): subject mutations queue SubjectList + SubjectEntity keys
        // and THE fan-out tag, so the subject listing and the tagged public course view refresh
        // immediately. The student catalog is deliberately NOT fan-out tagged; its staleness
        // after a subject rename is accepted (≤2-min TTL, rare operation) and is asserted below
        // as the served-from-cache proof.
        var rename = await Client.PutAsJsonAsync($"/courses/subjects/{subjectId}",
            new { Name = "Renamed Subject" });
        rename.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var catalogStale = await Client.GetFromJsonAsync<PagedResult<CourseCatalogItemDto>>("/courses/catalog");
        catalogStale!.Items.Should().ContainSingle(i => i.Id == courseId && i.SubjectName == "Cache Test Subject");

        var viewFresh = await Client.GetFromJsonAsync<CourseViewShim>($"/courses/{courseId}/view");
        viewFresh!.SubjectName.Should().Be("Renamed Subject");
    }

    [Fact]
    public async Task Subjects_WarmListing_DeleteRefreshesListing()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var create = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "Doomed Subject" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var subjectId = (await create.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var warm = await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects");
        warm!.Items.Should().ContainSingle(s => s.Id == subjectId && s.Name == "Doomed Subject");

        var delete = await Client.DeleteAsync($"/courses/subjects/{subjectId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The subject listing is evicted by the delete, so the next read is cold.
        var fresh = await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects");
        fresh!.Items.Should().NotContain(s => s.Id == subjectId);
    }

    // ── In-place reorder: change-tracker eviction without an explicit Update call ──

    [Fact]
    public async Task CourseView_ReorderEvictsTree_ColdRefillShowsNewOrder()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        var courseId = await CreateCourseByHttp("Reorder Cache Course", subjectId);

        var first = await AddSection(courseId, "First Section");
        var second = await AddSection(courseId, "Second Section");

        var warm = await Client.GetFromJsonAsync<CourseSummaryTree>($"/courses/{courseId}");
        warm!.Sections.Select(s => s.Id).Should().Equal(first, second);

        var reorder = await Client.PostAsJsonAsync($"/courses/{courseId}/sections/reorder",
            new { CourseId = courseId, SectionIds = new[] { second, first } });
        reorder.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The reorder handler calls Update(course) so the write-side decorator queues the
        // course family tag; the next read is a cold refill.
        var fresh = await Client.GetFromJsonAsync<CourseSummaryTree>($"/courses/{courseId}");
        fresh!.Sections.Select(s => s.Id).Should().Equal(second, first);
    }

    [Fact]
    public async Task CourseView_AddSection_EvictsWarmedTree()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        var courseId = await CreateCourseByHttp("Add Section Cache Course", subjectId);

        var warm = await Client.GetFromJsonAsync<CourseSummaryTree>($"/courses/{courseId}");
        warm!.Sections.Should().BeEmpty();

        var sectionId = await AddSection(courseId, "Fresh Section");

        // The AddSection handler now calls Update(course); without it the warmed empty tree
        // would be served for the rest of the TTL.
        var fresh = await Client.GetFromJsonAsync<CourseSummaryTree>($"/courses/{courseId}");
        fresh!.Sections.Should().ContainSingle(s => s.Id == sectionId);
    }

    [Fact]
    public async Task CourseView_PlanUpdateAndRemove_EvictWarmedView()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        var courseId = await CreateCourseByHttp("Plan Eviction Cache Course", subjectId);
        await PublishCourseDirectly(courseId);

        var principalId = Guid.NewGuid();
        var addPlan = await Client.PostAsJsonAsync($"/courses/{courseId}/plans",
            new AddPlanRequest { CourseId = courseId, Name = "Original Plan", PrincipalId = principalId });
        addPlan.EnsureSuccessStatusCode();

        Guid planId = default;
        await ExecuteDbContextAsync(async db =>
        {
            planId = await db.CoursePlans
                .Where(p => p.CourseId == courseId)
                .Select(p => p.Id)
                .FirstAsync();
        });

        var warm = await Client.GetFromJsonAsync<CourseViewShim>($"/courses/{courseId}/view");
        warm!.Plans.Should().ContainSingle(p => p.Name == "Original Plan");

        var update = await Client.PutAsJsonAsync($"/courses/{courseId}/plans/{planId}",
            new { CourseId = courseId, PlanId = planId, Name = "Renamed Plan", PrincipalId = principalId });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updated = await Client.GetFromJsonAsync<CourseViewShim>($"/courses/{courseId}/view");
        updated!.Plans.Should().ContainSingle(p => p.Name == "Renamed Plan");

        var remove = await Client.DeleteAsync($"/courses/{courseId}/plans/{planId}");
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var removed = await Client.GetFromJsonAsync<CourseViewShim>($"/courses/{courseId}/view");
        removed!.Plans.Should().BeEmpty();
    }

    // ── Consumer scope: ambient tenant restored from the integration event ──

    [Fact]
    public async Task AssetAvailable_FromTenantLessBusScope_UpdatesAssetAndRefreshesReadiness()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        var courseId = await CreateCourseByHttp("Consumer Asset Course", subjectId);
        var sectionId = await AddSection(courseId);

        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(tenantId, videoId);
        await ExecuteDbContextAsync(async db =>
        {
            var course = await db.Courses.Include(c => c.Sections).FirstAsync(c => c.Id == courseId);
            var section = course.Sections.Single(s => s.Id == sectionId);
            var resource = new VideoCurriculumResource(Guid.NewGuid(), 0, videoId);
            var addItem = section.AddItem(Guid.NewGuid(), "Consumer Video Item", ResourceType.Video,
                course.AllocateNextBitIndex(), [resource]);
            addItem.IsError.Should().BeFalse();
            db.CourseAssets.Add(VideoCourseAsset.Create(videoId, tenantId, videoArn, "Consumer Video", courseId).Value);
            await db.SaveChangesAsync();
        });

        // Warm readiness: the attached asset is still Pending (transcoding).
        var warm = await Client.GetFromJsonAsync<CourseReadiness>($"/courses/{courseId}/readiness");
        warm!.BlockingReasons.Should().Contain(r => r.Contains("still transcoding"));

        // Simulate the real consumer: no HTTP context and no static test tenant. Only the
        // consume filter's ambient tenant (from the event's TenantId) can resolve the asset.
        TestTenantProvider.StaticTenantId = null;

        using (var scope = Factory.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<MassTransit.IBus>();
            await bus.Publish(new VideoPublishedIntegrationEvent(videoId, tenantId, "streaming/consumer/master.m3u8"));
        }

        CourseAssetState? state = null;
        for (var i = 0; i < 100; i++)
        {
            state = await ExecuteDbContextAsync(async db => await db.CourseAssets
                .IgnoreQueryFilters()
                .Where(a => a.Id == videoId)
                .Select(a => (CourseAssetState?)a.State)
                .FirstOrDefaultAsync());
            if (state == CourseAssetState.Available) break;
            await Task.Delay(100);
        }

        TestTenantProvider.StaticTenantId = tenantId;

        state.Should().Be(CourseAssetState.Available);
        // The asset mutation evicted the per-course family, so the warmed readiness entry is gone.
        var fresh = await Client.GetFromJsonAsync<CourseReadiness>($"/courses/{courseId}/readiness");
        fresh!.BlockingReasons.Should().NotContain(r => r.Contains("still transcoding"));
    }

    // ── Tenant isolation inside the cache ──

    [Fact]
    public async Task CacheSurfaces_AreTenantIsolated_AcrossAllSevenCachedReads()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        SetTenant(tenantA);
        var subjectA = await CreateSubjectByHttp();
        var courseA = await CreateCourseByHttp("Tenant A Exclusive Course", subjectA);
        await PublishCourseDirectly(courseA);

        // Warm every cached surface under tenant A.
        var staffA = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses");
        staffA!.Items.Should().ContainSingle(c => c.Id == courseA);
        var studentA = await Client.GetFromJsonAsync<PagedResult<CourseCatalogItemDto>>("/courses/catalog");
        studentA!.Items.Should().ContainSingle(c => c.Id == courseA);
        var viewA = await Client.GetFromJsonAsync<CourseViewShim>($"/courses/{courseA}/view");
        viewA!.Id.Should().Be(courseA);
        var treeA = await Client.GetFromJsonAsync<CourseSummaryTree>($"/courses/{courseA}");
        treeA!.Id.Should().Be(courseA);
        (await Client.GetAsync($"/courses/{courseA}/overview")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetFromJsonAsync<CourseReadiness>($"/courses/{courseA}/readiness")).Should().NotBeNull();
        var subjectsA = await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects");
        subjectsA!.Items.Should().ContainSingle(s => s.Name == "Cache Test Subject");

        // Tenant B issues same-shaped requests: none may resolve into A's warmed entries.
        SetTenant(tenantB);
        (await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses"))!.Items.Should().BeEmpty();
        (await Client.GetFromJsonAsync<PagedResult<CourseCatalogItemDto>>("/courses/catalog"))!.Items.Should().BeEmpty();
        (await Client.GetAsync($"/courses/{courseA}/view")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/courses/{courseA}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/courses/{courseA}/overview")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/courses/{courseA}/readiness")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects"))!.Items.Should().BeEmpty();

        // B's empty entries are warmed now; repeating still yields nothing of A's.
        (await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses"))!.Items.Should().BeEmpty();

        // Back in A every warmed entry still resolves A's own data.
        SetTenant(tenantA);
        (await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses"))!.Items.Should().ContainSingle(c => c.Id == courseA);
        (await Client.GetFromJsonAsync<PagedResult<CourseCatalogItemDto>>("/courses/catalog"))!.Items.Should().ContainSingle(c => c.Id == courseA);
        (await Client.GetFromJsonAsync<CourseViewShim>($"/courses/{courseA}/view"))!.Id.Should().Be(courseA);
        (await Client.GetFromJsonAsync<CourseSummaryTree>($"/courses/{courseA}"))!.Id.Should().Be(courseA);
        (await Client.GetAsync($"/courses/{courseA}/overview")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetFromJsonAsync<CourseReadiness>($"/courses/{courseA}/readiness")).Should().NotBeNull();
        (await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects"))!.Items.Should().ContainSingle(s => s.Name == "Cache Test Subject");
    }

    // ── Explicit exclusion: enrollment surfaces stay DB-consistent (uncached) ──

    [Fact]
    public async Task Dashboard_StaysUncached_DirectEnrollmentChangeIsImmediatelyVisible()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var subjectId = await CreateSubjectByHttp();
        var courseId = await CreateCourseByHttp("Cache Exclusion Course", subjectId);

        var studentId = Guid.NewGuid();
        var enrollResponse = await Client.PostAsJsonAsync("/courses/enroll",
            new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        enrollResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var warm = await Client.GetFromJsonAsync<DashboardResponse>($"/courses/dashboard/{studentId}");
        warm!.Academies.Should().ContainKey(tenantId);
        warm.Academies[tenantId].Should().ContainSingle(e => e.Status == "Active");

        // Direct enrollment state change behind the (nonexistent) dashboard cache: the read must
        // reflect the DB state immediately — enrollment surfaces are never served from cache.
        await ExecuteDbContextAsync(async db =>
        {
            var enrollment = await db.Enrollments.FirstAsync(e => e.StudentId == studentId && e.CourseId == courseId);
            enrollment.Deactivate();
            await db.SaveChangesAsync();
        });

        var after = await Client.GetFromJsonAsync<DashboardResponse>($"/courses/dashboard/{studentId}");
        // The dashboard query filters Active enrollments; deactivation vanishes immediately —
        // there is no cache between the DB change and the response (write-path exclusion).
        after!.Academies.Should().BeEmpty();
    }

    // ── helpers ──

    private async Task<Guid> CreateSubjectByHttp()
    {
        var response = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "Cache Test Subject" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;
    }

    private async Task<Guid> CreateCourseByHttp(string title, Guid subjectId)
    {
        var response = await Client.PostAsJsonAsync("/courses",
            new CreateCourseRequest { Title = title, Description = null, SubjectId = subjectId });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;
    }

    private async Task<Guid> AddSection(Guid courseId, string title = "Cache Section")
    {
        var response = await Client.PostAsJsonAsync($"/courses/{courseId}/sections",
            new { Title = title });
        response.EnsureSuccessStatusCode();

        Guid sectionId = default;
        await ExecuteDbContextAsync(async db =>
        {
            sectionId = await db.Set<CourseSection>()
                .Where(s => s.CourseId == courseId && s.Title == title && !s.IsDeleted)
                .Select(s => s.Id)
                .FirstAsync();
        });
        return sectionId;
    }

    /// <summary>Direct DB write (no command): seeds a Published course before warming a cache.</summary>
    private async Task PublishCourseDirectly(Guid courseId)
    {
        await ExecuteDbContextAsync(async db =>
        {
            var course = await db.Courses.FirstAsync(c => c.Id == courseId);
            db.Entry(course).Property(c => c.Status).CurrentValue = CourseStatus.Published;
            await db.SaveChangesAsync();
        });
    }

    // Response-model shims (test-local, deserialization only)
    public record CourseSummaryTree(Guid Id, string Title, List<SectionShim> Sections);
    public record SectionShim(Guid Id, string Title, int Order, List<ItemShim> Items);
    public record ItemShim(Guid Id, string Title);
    public record CourseViewShim(Guid Id, string Title, string SubjectName, List<PlanShim> Plans);
    public record PlanShim(Guid Id, string Name);
}
