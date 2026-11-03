using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Courses.Pool;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Get;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;

namespace Courses.Tests.Integration;

public class EnrollmentTests : BaseIntegrationTest
{
    public EnrollmentTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedAvailableVideoAsset(Guid courseId, Guid tenantId, string title = "Test Video")
    {
        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, videoArn, title, courseId).Value;
        asset.MarkAvailable();

        DbContext.CourseAssets.Add(asset);
        await DbContext.SaveChangesAsync();
        return videoId;
    }

    private async Task<Guid> CreatePublishedCourse(Guid tenantId)
    {
        SetTenant(tenantId);
        
        // Subject
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        // Course
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "C1", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        // Content
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;

        var v1 = await SeedAvailableVideoAsset(courseId, tenantId, "L1");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v1}/assign", new AssignAssetRequest
        {
            CourseId = courseId,
            AssetId = v1,
            SectionId = sectionId,
            Title = "L1"
        });

        var v2 = await SeedAvailableVideoAsset(courseId, tenantId, "L2");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v2}/assign", new AssignAssetRequest
        {
            CourseId = courseId,
            AssetId = v2,
            SectionId = sectionId,
            Title = "L2"
        });

        // Lifecycle
        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PostAsJsonAsync($"/courses/{courseId}/plans", new AddPlanRequest { Name = "Standard", PrincipalId = Guid.NewGuid() });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });

        return courseId;
    }

    [Fact]
    public async Task Enroll_Should_CreateEnrollment_WhenCourseIsPublished()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        SetTenant(tenantId);

        // Act
        var response = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest 
        { 
            StudentId = studentId, 
            CourseId = courseId 
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var enrollmentId = (await response.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var enrollment = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        enrollment.Should().NotBeNull();
        enrollment!.StudentId.Should().Be(studentId);
        enrollment.CourseId.Should().Be(courseId);
        enrollment.CompletionPercentage.Should().Be(0);
    }

    [Fact]
    public async Task CompleteItem_Should_UpdateProgress()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        SetTenant(tenantId);
        
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        // Act: Complete first item (BitIndex 0)
        var completeResp = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });
        completeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: 1 of 2 items complete = 50%
        var enrollment = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        enrollment!.CompletionPercentage.Should().Be(50.0);
    }

    [Fact]
    public async Task Idempotency_Should_Fail_WhenEnrollingTwice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        SetTenant(tenantId);

        await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });

        // Act
        var response = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CompleteItem_PrimaryRoute_Should_UseCorrectSpelling_And_LegacyRouteMustNotRoute()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        SetTenant(tenantId);

        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        // Act: the correctly-spelled route is the primary (and only) surface
        var primaryResp = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });

        // Act: the misspelled legacy spelling must not route anywhere (#31: app is unpublished,
        // so the compat alias was retired — the rename is a hard cutover, not an alias rollout)
        var legacyRouteResp = await Client.PostAsJsonAsync($"/courses/enrollements/{enrollmentId}/complete", new { BitIndex = 1 });

        // Assert
        primaryResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        legacyRouteResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Assert: the primary route persists real state — 1 of 2 items = 50%
        var enrollment = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        enrollment!.CompletionPercentage.Should().Be(50.0);
    }

    [Fact]
    public async Task CompleteItem_Should_Fail_WhenIndexIsOutOfRange()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId); // Has 2 items
        var studentId = Guid.NewGuid();
        SetTenant(tenantId);
        
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        // Act: Try to complete bit index 5 (only 0 and 1 exist)
        var completeResp = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 5 });

        // Assert
        completeResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
