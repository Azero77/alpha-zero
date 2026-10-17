using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
using AlphaZero.Shared.Queries;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

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
    // NOTE (pre-existing defects observed on this base, reported on the owning issues — not
    // fixed here because they are outside #33's caching scope):
    //  * GetCourseOverviewAsync's LINQ projection (`o.DescriptionContent.Value` on a
    //    value-converted RichText property) materializes as null, so the response's
    //    descriptionContent is always null even when the DB row holds real content.
    //  * UpsertCourseOverviewCommand is an IRequest (not ICommand), so the UnitOfWork behavior
    //    never saves it — overview edits are silently discarded.
    // The cache still wraps this surface exactly as specified; the observable state here is the
    // ETag (a fresh GUID on every overview Update), which lets us prove both cache-serve and
    // tag-family eviction without depending on the broken fields.
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

    // ── Tenant isolation inside the cache ──

    [Fact]
    public async Task CacheSurfaces_AreTenantIsolated_CrossTenantReadsReturnNothingOfTheOtherTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        SetTenant(tenantA);
        var subjectA = await CreateSubjectByHttp();
        var courseA = await CreateCourseByHttp("Tenant A Exclusive Course", subjectA);
        var warmA = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses");
        warmA!.Items.Should().ContainSingle(c => c.Id == courseA); // A warmed its catalog

        // Requesting the same-shaped catalog as tenant B (identical key shape minus the tenant
        // component) must NOT resolve into A's warmed entry.
        SetTenant(tenantB);
        var catalogB = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses");
        catalogB!.Items.Should().BeEmpty();
        catalogB.Items.Should().NotContain(c => c.Id == courseA);

        // B's warmed empty catalog read again — still nothing of A's.
        var catalogBRepeat = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses");
        catalogBRepeat!.Items.Should().BeEmpty();

        // Overview: B requesting A's course id (same tenant-shaped read) gets 404, never A's content.
        var overviewB = await Client.GetAsync($"/courses/{courseA}/overview");
        overviewB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Subjects follow the same isolation: B's listing has only B's data even after B's listing got cached.
        var subjectsB = await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects");
        subjectsB!.Items.Should().NotContain(s => s.Name == "Tenant A Subject");

        // Back in A: A's cached catalog still resolves A's own course.
        SetTenant(tenantA);
        var catalogARepeat = await Client.GetFromJsonAsync<PagedResult<CourseSummaryDto>>("/courses");
        catalogARepeat!.Items.Should().ContainSingle(c => c.Id == courseA);

        // A creating a subject refreshes only A's subject listing; B remains isolated.
        await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "Tenant A Subject" });
        var subjectsA = await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects");
        subjectsA!.Items.Should().Contain(s => s.Name == "Tenant A Subject");

        SetTenant(tenantB);
        var subjectsB2 = await Client.GetFromJsonAsync<PagedResult<SubjectDto>>("/courses/subjects");
        subjectsB2!.Items.Should().NotContain(s => s.Name == "Tenant A Subject");
        catalogB!.Items.Should().NotContain(c => c.Id == courseA);
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

    private static JsonElement c(string json) => JsonDocument.Parse(json).RootElement;

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

    private async Task<Guid> AddSection(Guid courseId)
    {
        var response = await Client.PostAsJsonAsync($"/courses/{courseId}/sections",
            new { Title = "Cache Section" });
        response.EnsureSuccessStatusCode();

        Guid sectionId = default;
        await ExecuteDbContextAsync(async db =>
        {
            sectionId = await db.Set<CourseSection>()
                .Where(s => s.CourseId == courseId && !s.IsDeleted)
                .Select(s => s.Id)
                .FirstAsync();
        });
        return sectionId;
    }

    // Response-model shims (test-local, deserialization only)
    public record CourseSummaryTree(Guid Id, string Title, List<SectionShim> Sections);
    public record SectionShim(Guid Id, string Title, List<ItemShim> Items);
    public record ItemShim(Guid Id, string Title);
}
