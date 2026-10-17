using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourseCatalog;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Courses.Pool;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Queries;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

// #32: GET /courses/catalog — permission-light student browse surface.
// Only external behavior asserted (status codes + response fields/values).
public class CatalogTests : BaseIntegrationTest
{
    public CatalogTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedSubject(Guid tenantId, string name = "Computer Science")
    {
        SetTenant(tenantId);
        var response = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = name, Description = name });
        var result = await response.Content.ReadFromJsonAsync<CreateSubjectResponse>();
        return result!.Id;
    }

    private async Task<Guid> SeedVideoCourseAsset(Guid courseId, Guid tenantId, string title, double durationSeconds)
    {
        var videoId = Guid.NewGuid();
        var asset = VideoCourseAsset.Create(
            videoId, tenantId, ResourceArn.ForVideo(tenantId, videoId), title, courseId).Value;
        asset.MarkAvailable();
        asset.UpdateStreamingInfo(TimeSpan.FromSeconds(durationSeconds), null, null);
        DbContext.CourseAssets.Add(asset);
        await DbContext.SaveChangesAsync();
        return videoId;
    }

    private async Task<Guid> SeedPublishedCourse(
        Guid tenantId,
        string title,
        Guid subjectId,
        string? description = null,
        string planName = "Standard",
        double videoSeconds = 600,
        int itemCount = 2)
    {
        SetTenant(tenantId);
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = title, Description = description, SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;

        for (var i = 0; i < itemCount; i++)
        {
            var videoId = await SeedVideoCourseAsset(courseId, tenantId, $"Lesson {i}", videoSeconds);
            await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{videoId}/assign", new AssignAssetRequest
            {
                CourseId = courseId,
                AssetId = videoId,
                SectionId = sectionId,
                Title = $"Lesson {i}"
            });
        }

        SetTenant(tenantId);
        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PostAsJsonAsync($"/courses/{courseId}/plans", new AddPlanRequest { Name = planName, PrincipalId = Guid.NewGuid() });
        var publishResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });
        publishResp.StatusCode.Should().Be(HttpStatusCode.NoContent, because: "seed setup: course must publish cleanly");
        return courseId;
    }

    private Task SetOverviewFields(Guid courseId, Guid? coverImageDocumentId, DateTime? createdAtUtc, DateTime? updatedAtUtc) =>
        ExecuteDbContextAsync(async db =>
        {
            var createdAt = createdAtUtc ?? DateTime.UtcNow;
            var updatedAt = updatedAtUtc ?? createdAt;
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Courses\".\"CourseOverviews\" SET \"CoverImageDocumentId\" = {0}, \"CreatedAtUtc\" = {1}, \"UpdatedAtUtc\" = {2} WHERE \"CourseId\" = {3}",
                coverImageDocumentId,
                createdAt,
                updatedAt,
                courseId);
        });

    private Task SoftDeleteCourse(Guid courseId) =>
        ExecuteDbContextAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Courses\".\"Courses\" SET \"IsDeleted\" = TRUE, \"OnDeleted\" = NOW() WHERE \"Id\" = {0}",
                courseId);
        });

    [Fact]
    public async Task Catalog_Should_RouteToCatalogEndpoint_NotAdminDetail()
    {
        // Route placement guard: /courses/catalog must not be captured by GET /courses/{Id}
        var response = await Client.GetAsync("/courses/catalog");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Catalog_Should_ReturnPublishedCourseWithRichSummary()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId, "Computer Science");
        var courseId = await SeedPublishedCourse(tenantId, "Intro to Algorithms", subjectId, description: "Learn algorithms and big-O analysis");
        var coverImageId = Guid.NewGuid();
        await SetOverviewFields(courseId, coverImageId, createdAtUtc: DateTime.UtcNow.AddDays(-5), updatedAtUtc: DateTime.UtcNow.AddDays(-1));
        SetTenant(tenantId);

        var response = await Client.GetAsync("/courses/catalog");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalog = await response.Content.ReadFromJsonAsync<PagedResult<CourseCatalogItemDto>>();
        catalog.Should().NotBeNull();
        catalog!.TotalCount.Should().Be(1);
        var item = catalog.Items.Should().ContainSingle(i => i.Id == courseId).Subject;

        item!.Title.Should().Be("Intro to Algorithms");
        item.Description.Should().Be("Learn algorithms and big-O analysis");
        item.Status.Should().Be("Published");
        item.SubjectId.Should().Be(subjectId);
        item.SubjectName.Should().Be("Computer Science"); // subject NAME, not the raw id
        item.CoverImageDocumentId.Should().Be(coverImageId);
        item.SectionCount.Should().Be(1);
        item.ItemCount.Should().Be(2);
        item.TotalDurationSeconds.Should().Be(1200.0); // 2 videos × 600s
        item.Plans.Should().ContainSingle(p => p.Name == "Standard");
        item.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(-1), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Catalog_Search_Should_MatchTitleAndDescription()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        var alphaId = await SeedPublishedCourse(tenantId, "Alpha Cooking Mastery", subjectId, description: "cooking fundamentals");
        await SeedPublishedCourse(tenantId, "Beta Piano Basics", subjectId);
        await SeedPublishedCourse(tenantId, "Gamma Course", subjectId, description: "the culinary arts live here");

        SetTenant(tenantId);

        // Title match
        var titleResponse = await Client.GetAsync("/courses/catalog?Search=alpha");
        titleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var titleCatalog = await titleResponse.Content.ReadFromJsonAsync<PagedResult<CourseCatalogItemDto>>();
        titleCatalog!.TotalCount.Should().Be(1);
        titleCatalog.Items.Single().Id.Should().Be(alphaId);

        // Description match (course whose description, not title, has the keyword)
        var descResponse = await Client.GetAsync("/courses/catalog?Search=culinary");
        var descCatalog = await descResponse.Content.ReadFromJsonAsync<PagedResult<CourseCatalogItemDto>>();
        descCatalog!.TotalCount.Should().Be(1);
        descCatalog.Items.Single().Title.Should().Be("Gamma Course");

        // No match
        var noneResponse = await Client.GetAsync("/courses/catalog?Search=nonexistent-keyword");
        var noneCatalog = await noneResponse.Content.ReadFromJsonAsync<PagedResult<CourseCatalogItemDto>>();
        noneCatalog!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Catalog_Should_FilterBySubject()
    {
        var tenantId = Guid.NewGuid();
        var mathSubjectId = await SeedSubject(tenantId, "Mathematics");
        var artSubjectId = await SeedSubject(tenantId, "Fine Arts");
        var mathCourseId = await SeedPublishedCourse(tenantId, "Calculus I", mathSubjectId);
        await SeedPublishedCourse(tenantId, "Watercolor Painting", artSubjectId);

        SetTenant(tenantId);
        var response = await Client.GetAsync($"/courses/catalog?SubjectId={mathSubjectId}");
        var body = await response.Content.ReadAsStringAsync();

        var catalog = await response.Content.ReadFromJsonAsync<PagedResult<CourseCatalogItemDto>>();
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
        catalog!.TotalCount.Should().Be(1);
        catalog.Items.Single().Id.Should().Be(mathCourseId);
    }

    [Fact]
    public async Task Catalog_Should_SortByTitleAndNewest()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        var zetaId = await SeedPublishedCourse(tenantId, "Zeta Course", subjectId);
        var alphaId = await SeedPublishedCourse(tenantId, "Alpha Course", subjectId);

        // Deterministic 'newest': Alpha's overview creation is pushed back in time
        await SetOverviewFields(alphaId, null, createdAtUtc: DateTime.UtcNow.AddDays(-30), updatedAtUtc: DateTime.UtcNow.AddDays(-30));
        await SetOverviewFields(zetaId, null, createdAtUtc: DateTime.UtcNow.AddDays(-5), updatedAtUtc: DateTime.UtcNow.AddDays(-5));

        SetTenant(tenantId);

        // Title sort (default for the admin listing; catalog's explicit sort keys are newest|title)
        var titleResponse = await Client.GetAsync("/courses/catalog?Sort=title");
        var titleCatalog = await titleResponse.Content.ReadFromJsonAsync<PagedResult<CourseCatalogItemDto>>();
        titleCatalog!.Items.Select(i => i.Title).Should().ContainInOrder("Alpha Course", "Zeta Course");

        // Newest sort (default for the catalog)
        var newestResponse = await Client.GetAsync("/courses/catalog");
        var newestCatalog = await newestResponse.Content.ReadFromJsonAsync<PagedResult<CourseCatalogItemDto>>();
        newestCatalog!.Items.Single(i => i.Id == zetaId).Should().NotBeNull();
        newestCatalog.Items.First().Id.Should().Be(zetaId, because: "'newest' puts the most recently created overview first");
    }

    [Fact]
    public async Task Catalog_Should_ExcludeDeletedAndUnpublishedCourses()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        var publishedId = await SeedPublishedCourse(tenantId, "Kept Course", subjectId);

        // A draft course: never published, must never appear in the student catalog
        SetTenant(tenantId);
        var draftResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Draft Course", SubjectId = subjectId });
        var draftId = (await draftResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        // A published-but-soft-deleted course must be excluded (visibility rules)
        var deletedId = await SeedPublishedCourse(tenantId, "Deleted Course", subjectId);
        await SoftDeleteCourse(deletedId);

        SetTenant(tenantId);
        var response = await Client.GetAsync("/courses/catalog");
        var catalog = await response.Content.ReadFromJsonAsync<PagedResult<CourseCatalogItemDto>>();

        catalog!.TotalCount.Should().Be(1);
        catalog.Items.Single().Id.Should().Be(publishedId);
        catalog.Items.Should().NotContain(i => i.Id == draftId);
        catalog.Items.Should().NotContain(i => i.Id == deletedId);
    }

    [Fact]
    public async Task Catalog_Should_IsolateTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var subjectA = await SeedSubject(tenantA, "A Subject");
        var subjectB = await SeedSubject(tenantB, "B Subject");
        await SeedPublishedCourse(tenantA, "Tenant A Course", subjectA);
        await SeedPublishedCourse(tenantB, "Tenant B Course", subjectB);

        SetTenant(tenantA);
        var response = await Client.GetAsync("/courses/catalog");
        var body = await response.Content.ReadAsStringAsync();
        var catalog = await response.Content.ReadFromJsonAsync<PagedResult<CourseCatalogItemDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
        catalog!.TotalCount.Should().Be(1);
        catalog.Items.Single().Title.Should().Be("Tenant A Course");
        catalog.Items.Should().NotContain(i => i.Title == "Tenant B Course");
    }
}
