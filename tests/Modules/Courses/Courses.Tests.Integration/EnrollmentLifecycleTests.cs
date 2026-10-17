using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Courses.Pool;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Dashboard;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Get;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

public class EnrollmentLifecycleTests : BaseIntegrationTest
{
    public EnrollmentLifecycleTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedAvailableVideoAsset(Guid tenantId, Guid courseId, string title = "Test Video")
    {
        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, videoArn, title, courseId).Value;
        asset.MarkAvailable();

        DbContext.CourseAssets.Add(asset);
        await DbContext.SaveChangesAsync();
        return videoId;
    }

    private async Task<Guid> CreatePublishedCourse(Guid tenantId, int itemCount = 2)
    {
        SetTenant(tenantId);
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Published Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;

        for (int i = 0; i < itemCount; i++)
        {
            var videoId = await SeedAvailableVideoAsset(tenantId, courseId, $"L{i + 1}");
            await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{videoId}/assign",
                new AssignAssetRequest { CourseId = courseId, AssetId = videoId, SectionId = sectionId, Title = $"L{i + 1}" });
        }

        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PostAsJsonAsync($"/courses/{courseId}/plans", new AddPlanRequest { Name = "Standard", PrincipalId = Guid.NewGuid() });
        var publishResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });
        publishResp.StatusCode.Should().Be(HttpStatusCode.NoContent, because: "test setup must yield a published course");

        return courseId;
    }

    private async Task<Guid> EnrollStudent(Guid studentId, Guid courseId)
    {
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        enrollResp.StatusCode.Should().Be(HttpStatusCode.Created, because: "test setup must yield an enrollment");
        return (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;
    }

    [Fact]
    public async Task Deactivate_Should_Return204_And_PersistInactiveStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseId = await CreatePublishedCourse(tenantId);
        var enrollmentId = await EnrollStudent(Guid.NewGuid(), courseId);

        // Act
        var response = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/deactivate", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var enrollment = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        enrollment!.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task Deactivate_Should_BeIdempotent_WhenAlreadyInactive()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseId = await CreatePublishedCourse(tenantId);
        var enrollmentId = await EnrollStudent(Guid.NewGuid(), courseId);

        var first = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/deactivate", new { });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: retry — must succeed, not error
        var second = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/deactivate", new { });

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var enrollment = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        enrollment!.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task Deactivate_Should_RetainCompletedProgress()
    {
        // Arrange: student completed 1 of 2 items then deactivated (support/refund flow)
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        var enrollmentId = await EnrollStudent(studentId, courseId);

        var completeResp = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });
        completeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/deactivate", new { });

        // Assert: inactive, progress retained for later reactivation
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var enrollment = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        enrollment!.Status.Should().Be("Inactive");
        enrollment.CompletionPercentage.Should().Be(50.0);

        await ExecuteDbContextAsync(async db =>
        {
            var persisted = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId);
            persisted.Status.Should().Be(AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.EnrollmentStatus.Inactive);
            persisted.Progress.IsComplete(0).Should().BeTrue();
            persisted.Progress.CompletionPercentage.Should().Be(50.0);
        });
    }

    [Fact]
    public async Task Deactivate_Should_Return404_WhenEnrollmentDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseId = await CreatePublishedCourse(tenantId);
        await EnrollStudent(Guid.NewGuid(), courseId);

        // Act
        var response = await Client.PostAsJsonAsync($"/courses/enrollments/{Guid.NewGuid()}/deactivate", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deactivate_Should_Return404_WhenEnrollmentBelongsToAnotherTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var courseIdOfA = await CreatePublishedCourse(tenantA);
        var enrollmentIdOfA = await EnrollStudent(Guid.NewGuid(), courseIdOfA);

        SetTenant(tenantB);
        var courseIdOfB = await CreatePublishedCourse(tenantB);
        await EnrollStudent(Guid.NewGuid(), courseIdOfB);

        // Act: tenant B operates on tenant A's enrollment
        var response = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentIdOfA}/deactivate", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Untouched
        SetTenant(tenantA);
        var enrollment = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentIdOfA}");
        enrollment!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ArchivedCourse_Should_KeepEnrollmentVisibleOnStudentDashboard()
    {
        // Arrange: in-progress enrollment, then the course gets archived (retired, not deleted)
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        await EnrollStudent(studentId, courseId);

        // Act
        var archiveResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/archive", new { });
        archiveResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: the student's in-progress work stays on the dashboard
        var dashboardResp = await Client.GetAsync($"/courses/dashboard/{studentId}");
        dashboardResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await dashboardResp.Content.ReadFromJsonAsync<DashboardResponse>();
        dashboard!.Academies.Should().ContainKey(tenantId);
        dashboard.Academies[tenantId].Should().ContainSingle(e => e.CourseId == courseId);
    }

    [Fact]
    public async Task GetEnrollment_Should_CompleteItem409_WhenDeactivated()
    {
        // Arrange — deactivation also blocks further completion (conflict path)
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseId = await CreatePublishedCourse(tenantId);
        var enrollmentId = await EnrollStudent(Guid.NewGuid(), courseId);
        await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/deactivate", new { });

        // Act
        var response = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
