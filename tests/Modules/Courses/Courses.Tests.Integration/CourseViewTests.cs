using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Courses.Overview;
using AlphaZero.Modules.Courses.Presentation.Courses.Pool;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

// #32: GET /courses/{id}/view — three families (prospect/unenrolled, enrolled overlay,
// admin caller). Only external behavior asserted (status codes + response fields/values).
public class CourseViewTests : BaseIntegrationTest
{
    public CourseViewTests(ApiFactory factory) : base(factory)
    {
    }

    public record ViewPlanResponse(Guid Id, string Name);

    public record ViewResourceResponse(Guid CurriculumResourceId, string? CourseAssetId, string? ResourceArn, string Type, int Order, double DurationSeconds);

    public record ViewItemResponse(Guid Id, string Title, string Type, int Order, int BitIndex, List<ViewResourceResponse> Resources);

    public record ViewSectionResponse(Guid Id, string Title, int Order, List<ViewItemResponse> Items);

    public record ViewEnrollmentResponse(Guid EnrollmentId, string Status, double CompletionPercentage, List<Guid> CompletedItemIds);

    public record ViewResponse(
        Guid Id,
        string Title,
        string? Description,
        string Status,
        Guid SubjectId,
        string SubjectName,
        Guid? CoverImageDocumentId,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        int SectionCount,
        int ItemCount,
        double TotalDurationSeconds,
        List<ViewPlanResponse> Plans,
        List<ViewSectionResponse> Sections,
        ViewEnrollmentResponse? Enrollment);

    private async Task<Guid> SeedSubject(Guid tenantId, string name = "Computer Science")
    {
        SetTenant(tenantId);
        var response = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = name, Description = name });
        var result = await response.Content.ReadFromJsonAsync<CreateSubjectResponse>();
        return result!.Id;
    }

    private async Task<Guid> SeedVideoAsset(Guid courseId, Guid tenantId, string title, double durationSeconds)
    {
        var videoId = Guid.NewGuid();
        var asset = VideoCourseAsset.Create(videoId, tenantId, ResourceArn.ForVideo(tenantId, videoId), title, courseId).Value;
        asset.MarkAvailable();
        asset.UpdateStreamingInfo(TimeSpan.FromSeconds(durationSeconds), null, null);
        DbContext.CourseAssets.Add(asset);
        await DbContext.SaveChangesAsync();
        return videoId;
    }

    private async Task<Guid> SeedPublishedCourse(Guid tenantId, string title, Guid subjectId, string? description = null, string planName = "Standard")
    {
        SetTenant(tenantId);
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = title, Description = description, SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "Section One" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;

        var v1 = await SeedVideoAsset(courseId, tenantId, "Video One", 300);
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v1}/assign", new AssignAssetRequest { CourseId = courseId, AssetId = v1, SectionId = sectionId, Title = "Video One" });
        var v2 = await SeedVideoAsset(courseId, tenantId, "Video Two", 900);
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v2}/assign", new AssignAssetRequest { CourseId = courseId, AssetId = v2, SectionId = sectionId, Title = "Video Two" });

        SetTenant(tenantId);
        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PostAsJsonAsync($"/courses/{courseId}/plans", new AddPlanRequest { Name = planName, PrincipalId = Guid.NewGuid() });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });
        return courseId;
    }

    private async Task<(Guid CourseId, Guid SubjectId, Guid EnrollmentId, Guid StudentId, Guid ItemOneId, Guid ItemTwoId)> SeedEnrolledCourse(Guid tenantId, Guid studentId)
    {
        var subjectId = await SeedSubject(tenantId);
        var courseId = await SeedPublishedCourse(tenantId, "Full Platform Course", subjectId, description: "the canonical view fixture");
        SetTenant(tenantId);

        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var itemOneId = courseData!.Sections[0].Items[0].Id;
        var itemTwoId = courseData!.Sections[0].Items[1].Id;

        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        // complete the first item (bit 0)
        await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });

        return (courseId, subjectId, enrollmentId, studentId, itemOneId, itemTwoId);
    }

    [Fact]
    public async Task ProspectView_Should_ReturnCompletePublicShape_WithoutEnrollment()
    {
        // Arrange — a caller that is NOT enrolled (every test-host request is authenticated,
        // so the overlay lookup simply finds no enrollment → the prospect/public shape)
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        var courseId = await SeedPublishedCourse(tenantId, "Public Course", subjectId, description: "open to anyone");
        var coverImageId = Guid.NewGuid();
        await ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Courses\".\"CourseOverviews\" SET \"CoverImageDocumentId\" = {0} WHERE \"CourseId\" = {1}", coverImageId, courseId));

        SetTenant(tenantId);

        // Act
        var response = await Client.GetAsync($"/courses/{courseId}/view");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
        var view = await response.Content.ReadFromJsonAsync<ViewResponse>();
        view.Should().NotBeNull();
        view!.Id.Should().Be(courseId);
        view.Title.Should().Be("Public Course");
        view.Description.Should().Be("open to anyone");
        view.Status.Should().Be("Published");
        view.SubjectId.Should().Be(subjectId);
        view.SubjectName.Should().Be("Computer Science");
        view.CoverImageDocumentId.Should().Be(coverImageId);
        view.SectionCount.Should().Be(1);
        view.ItemCount.Should().Be(2);
        view.TotalDurationSeconds.Should().Be(1200.0);
        view.Plans.Should().ContainSingle(p => p.Name == "Standard");
        view.Enrollment.Should().BeNull(because: "not enrolled — same shape, no overlay");

        // curriculum tree + REAL resource identity: true ARN, course-asset id, duration...
        view.Sections.Should().ContainSingle(because: body);
        var items = view.Sections.Single().Items;
        items.Should().HaveCount(2, because: body);
        items.SelectMany(i => i.Resources).Should().OnlyContain(r => r != null && !string.IsNullOrEmpty(r.ResourceArn), because: body);
        items.SelectMany(i => i.Resources).Should().OnlyContain(r => r != null && r.CourseAssetId != null && Guid.Parse(r.CourseAssetId) != Guid.Empty, because: body);
        items[0].Resources[0].DurationSeconds.Should().Be(300.0);

        // ...and NO legacy metadata bucket anywhere in the response
        body.Should().NotContain("\"metadata\"", because: "the free-form metadata bucket is dropped from the view shape");
    }

    [Fact]
    public async Task EnrolledStudentView_Should_OverlayOwnEnrollmentProgress()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (courseId, _, enrollmentId, _, itemOneId, itemTwoId) = await SeedEnrolledCourse(tenantId, studentId);

        SetTenant(tenantId);
        var view = await SendViewRequestAsync(courseId, studentId);
        var body = view.Body;

        view.Response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
        view.View!.Enrollment.Should().NotBeNull();
        view.View!.Enrollment!.EnrollmentId.Should().Be(enrollmentId);
        view.View!.Enrollment.Status.Should().Be("Active");
        view.View!.Enrollment.CompletionPercentage.Should().Be(50.0);
        view.View!.Enrollment.CompletedItemIds.Should().ContainSingle(id => id == itemOneId);
        view.View!.Enrollment.CompletedItemIds.Should().NotContain(itemTwoId);
    }

    [Fact]
    public async Task AdminCallerView_Should_ShowSameShape_WithNoEnrollment()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (courseId, _, _, _, _, _) = await SeedEnrolledCourse(tenantId, studentId);

        // A supporter/admin user (policy evaluation is bypassed in the test host): not
        // enrolled in the course — same public shape, and NEVER the student's overlay
        SetTenant(tenantId);
        var adminId = Guid.NewGuid();
        var (response, view, body) = await SendViewRequestAsync(courseId, adminId);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
        view!.Enrollment.Should().BeNull(because: "an admin's overlay simply shows no enrollment — never someone else's");
        view.SubjectName.Should().Be("Computer Science");
        view.SectionCount.Should().Be(1);
    }

    private async Task<(HttpResponseMessage Response, ViewResponse? View, string Body)> SendViewRequestAsync(Guid courseId, Guid callerUserId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/courses/{courseId}/view");
        request.Headers.Add("X-Test-User-Id", callerUserId.ToString());
        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var view = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ViewResponse>()
            : null;
        return (response, view, body);
    }

    [Fact]
    public async Task View_Should_IsolateTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var subjectA = await SeedSubject(tenantA, "A");
        var courseId = await SeedPublishedCourse(tenantA, "Tenant A View Course", subjectA);

        SetTenant(tenantB);
        var response = await Client.GetAsync($"/courses/{courseId}/view");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, because: body);
    }

    [Fact]
    public async Task View_Should_NotExposeSoftDeletedCourse()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        var courseId = await SeedPublishedCourse(tenantId, "Deleted View Course", subjectId);
        await ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Courses\".\"Courses\" SET \"IsDeleted\" = TRUE, \"OnDeleted\" = NOW() WHERE \"Id\" = {0}", courseId));

        SetTenant(tenantId);
        var response = await Client.GetAsync($"/courses/{courseId}/view");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, because: body);
    }
}
