using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Pool;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Get;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Courses.Tests.Integration;

/// <summary>
/// Enrollment lifecycle endpoints (issue #34): suspend/reactivate roundtrips, idempotency,
/// invalid-transition domain errors at the HTTP boundary, cross-tenant denial.
/// </summary>
public class EnrollmentLifecycleTests : BaseIntegrationTest
{
    public EnrollmentLifecycleTests(ApiFactory factory) : base(factory)
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

        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Lifecycle Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;

        var v1 = await SeedAvailableVideoAsset(courseId, tenantId, "L1");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v1}/assign", new AssignAssetRequest
        {
            CourseId = courseId, AssetId = v1, SectionId = sectionId, Title = "L1"
        });

        var v2 = await SeedAvailableVideoAsset(courseId, tenantId, "L2");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v2}/assign", new AssignAssetRequest
        {
            CourseId = courseId, AssetId = v2, SectionId = sectionId, Title = "L2"
        });

        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });

        return courseId;
    }

    private async Task<Guid> EnrollAndCompleteAnItem(Guid tenantId, Guid courseId, Guid studentId, int bitIndex = 0)
    {
        SetTenant(tenantId);
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        enrollResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var complete = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = bitIndex });
        complete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return enrollmentId;
    }

    // ------------------------------------------------------------------
    // Suspend → reactivate roundtrip with full state preservation
    // ------------------------------------------------------------------

    [Fact]
    public async Task Suspend_And_Reactivate_Should_PreserveProgressAndStatus_Metadata()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        var enrollmentId = await EnrollAndCompleteAnItem(tenantId, courseId, studentId);

        // Act: suspend
        var suspend = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { Reason = "billing dispute" });
        suspend.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var suspended = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        suspended!.Status.Should().Be(EnrollmentStatus.Suspended.ToString());
        suspended.StudentId.Should().Be(studentId);
        suspended.CompletionPercentage.Should().BeGreaterThan(0); // earned progress survives suspension

        // Idempotent re-suspend (story #3)
        var reSuspend = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { Reason = "billing dispute" });
        reSuspend.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: reactivate (lift the suspension — story #4)
        var reactivate = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/reactivate", new { });
        reactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reactivated = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        reactivated!.Status.Should().Be(EnrollmentStatus.Active.ToString());
        reactivated.CompletionPercentage.Should().Be(suspended.CompletionPercentage); // progress intact — story #2

        // Idempotent re-reactivate (story #11)
        var reReactivate = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/reactivate", new { });
        reReactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reactivate_Should_ReviveDeactivatedEnrollment_KeepingProgress()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();

        SetTenant(tenantId);
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;
        var complete = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });
        complete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Arrange: an enrollment deactivated out-of-band (in this base there is no HTTP deactivate
        // route — the admin deactivate-on-id endpoint belongs to #29): put it Inactive directly seeded
        var enrollment = await DbContext.Enrollments.FirstAsync(e => e.Id == enrollmentId);
        enrollment.Deactivate().IsError.Should().BeFalse();
        await DbContext.SaveChangesAsync();

        // Act: resume payment resolved → reactivate (story #1)
        var reactivate = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/reactivate", new { });
        reactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        dto!.Status.Should().Be(EnrollmentStatus.Active.ToString());
        dto.CompletionPercentage.Should().BeGreaterThan(0);
    }

    // ------------------------------------------------------------------
    // Invalid transition — the domain error surfaces at the HTTP boundary (story #8)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Suspend_Should_Return409_WithDomainCode_WhenEnrollmentIsInactive()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();

        SetTenant(tenantId);
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var enrollment = await DbContext.Enrollments.FirstAsync(e => e.Id == enrollmentId);
        enrollment.Deactivate().IsError.Should().BeFalse();
        await DbContext.SaveChangesAsync();

        // Act: suspending an inactive enrollment is not a reachable transition in the matrix
        var response = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { Reason = "test" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Enrollment.InvalidTransition");
    }

    // ------------------------------------------------------------------
    // Cross-tenant denial — a tenant B operator cannot touch tenant A's enrollment
    // ------------------------------------------------------------------

    [Fact]
    public async Task Suspend_Should_Return404_WhenEnrollmentBelongsToAnotherTenant()
    {
        var tenantA = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantA);
        var studentId = Guid.NewGuid();
        var enrollmentId = await EnrollAndCompleteAnItem(tenantA, courseId, studentId);

        // Act: act from tenant B
        SetTenant(Guid.NewGuid());
        var response = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { Reason = "cross-tenant" });

        // Assert: existence is masked, not leaked
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
