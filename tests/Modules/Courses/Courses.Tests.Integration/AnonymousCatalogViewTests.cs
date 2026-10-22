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
using Xunit;

namespace Courses.Tests.Integration;

// #32 R1/D2 + R3/D4 — the anonymous browse surface, end to end: an unauthenticated request
// (X-Test-Anonymous opt-out) resolves its tenant ONLY from the X-TenantId header through the
// REAL HttpTenantProvider registered by the test host, and sees Published content only.
public class AnonymousCatalogViewTests : BaseIntegrationTest
{
    public AnonymousCatalogViewTests(ApiFactory factory) : base(factory)
    {
    }

    public record AnonymousViewResponse(
        Guid Id,
        string Title,
        string Status);

    private async Task<Guid> SeedSubject(Guid tenantId)
    {
        SetTenant(tenantId);
        var response = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "Computer Science", Description = "Computer Science" });
        var seedBody = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(because: "seed setup subject create: " + seedBody);
        var result = await response.Content.ReadFromJsonAsync<CreateSubjectResponse>();
        result!.Id.Should().NotBeEmpty(because: "seed setup subject id: " + seedBody);
        return result.Id;
    }

    private async Task<Guid> SeedPublishedCourse(Guid tenantId, Guid subjectId, string title)
    {
        SetTenant(tenantId);
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = title, Description = "public curriculum", SubjectId = subjectId });
        var createBody = await courseResp.Content.ReadAsStringAsync();
        courseResp.StatusCode.Should().Be(HttpStatusCode.Created, because: "seed setup course create: " + createBody);
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        var sectionResp = await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        sectionResp.EnsureSuccessStatusCode();
        var course = await (await Client.GetAsync($"/courses/{courseId}")).Content.ReadFromJsonAsync<CourseResponse>();
        var sectionId = course!.Sections.Single().Id;

        var videoId = Guid.NewGuid();
        var asset = VideoCourseAsset.Create(
            videoId, tenantId, ResourceArn.ForVideo(tenantId, videoId), "Video One", courseId).Value;
        asset.MarkAvailable();
        DbContext.CourseAssets.Add(asset);
        await DbContext.SaveChangesAsync();
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{videoId}/assign", new AssignAssetRequest
        {
            CourseId = courseId,
            AssetId = videoId,
            SectionId = sectionId,
            Title = "Video One"
        });

        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PostAsJsonAsync($"/courses/{courseId}/plans", new AddPlanRequest { Name = "Standard", PrincipalId = Guid.NewGuid() });
        var publishResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });
        publishResp.StatusCode.Should().Be(HttpStatusCode.NoContent, because: "seed setup: course must publish cleanly");
        return courseId;
    }

    private async Task<Guid> SeedDraftCourse(Guid tenantId, Guid subjectId, string title)
    {
        SetTenant(tenantId);
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = title, SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "Draft Section" });
        return courseId;
    }

    /// <summary>Sends a truly anonymous request: no identity, tenant from the header only.</summary>
    private async Task<HttpResponseMessage> SendAnonymousAsync(string path, Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("X-Test-Anonymous", "true");
        request.Headers.TryAddWithoutValidation("X-TenantId", tenantId.ToString());
        return await Client.SendAsync(request);
    }

    [Fact]
    public async Task AnonymousCatalog_Should_ReturnPublishedRows_ViaTenantHeader()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        var publishedId = await SeedPublishedCourse(tenantId, subjectId, "Anonymous Browse Course");
        await SeedDraftCourse(tenantId, subjectId, "Hidden Draft");

        var response = await SendAnonymousAsync("/courses/catalog", tenantId);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
        var catalog = await response.Content.ReadFromJsonAsync<PagedResult<CourseCatalogItemDto>>();
        catalog!.TotalCount.Should().Be(1, because: "only the Published course escapes to anonymous callers");
        catalog.Items.Should().ContainSingle(i => i.Id == publishedId);
    }

    [Fact]
    public async Task AnonymousView_Should_Return200_ForPublishedCourse()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        var courseId = await SeedPublishedCourse(tenantId, subjectId, "Anonymous View Course");

        var response = await SendAnonymousAsync($"/courses/{courseId}/view", tenantId);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
        var view = await response.Content.ReadFromJsonAsync<AnonymousViewResponse>();
        view!.Id.Should().Be(courseId);
        view.Status.Should().Be("Published");
    }

    [Fact]
    public async Task AnonymousView_Should_MaskDraftCourse_With404()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        var draftId = await SeedDraftCourse(tenantId, subjectId, "Anonymous Draft Course");

        var response = await SendAnonymousAsync($"/courses/{draftId}/view", tenantId);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, because: body);
    }
}
