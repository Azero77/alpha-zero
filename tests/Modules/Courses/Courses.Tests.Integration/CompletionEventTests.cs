using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Pool;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

// #32: course-completion milestone, exercised end to end through the complete-item API.
// The test host publishes integration events via in-memory bus (see ApiFactory), so
// bus delivery of CourseCompletedIntegrationEvent is asserted at the handler unit level
// (CourseCompletedDomainEventHandlerTests); integration coverage asserts completion state
// through the API and the persisted CompletedOn idempotency guard.
public class CompletionEventTests : BaseIntegrationTest
{
    public CompletionEventTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedAvailableVideoAsset(Guid courseId, Guid tenantId, string title)
    {
        var videoId = Guid.NewGuid();
        var asset = VideoCourseAsset.Create(videoId, tenantId, ResourceArn.ForVideo(tenantId, videoId), title, courseId).Value;
        asset.MarkAvailable();
        DbContext.CourseAssets.Add(asset);
        await DbContext.SaveChangesAsync();
        return videoId;
    }

    // Published course with 2 items — completing both flips the course to complete.
    private async Task<(Guid CourseId, Guid EnrollmentId)> SeedEnrolledPublishedCourse(Guid tenantId, Guid studentId)
    {
        SetTenant(tenantId);
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "C1", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;

        foreach (var title in new[] { "L1", "L2" })
        {
            var v = await SeedAvailableVideoAsset(courseId, tenantId, title);
            await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v}/assign", new AssignAssetRequest
            {
                CourseId = courseId,
                AssetId = v,
                SectionId = sectionId,
                Title = title
            });
        }

        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });

        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;
        return (courseId, enrollmentId);
    }

    private async Task<(DateTime? CompletedOn, double Percentage)> LoadEnrollmentState(Guid enrollmentId) =>
        await ExecuteDbContextAsync(async db =>
        {
            var e = await db.Enrollments.AsNoTracking().SingleAsync(x => x.Id == enrollmentId);
            return (e.CompletedOn, e.Progress.CompletionPercentage);
        });

    [Fact]
    public async Task CompletingAllItems_Should_PersistCompletedOn_ExactlyOnce()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (_, enrollmentId) = await SeedEnrolledPublishedCourse(tenantId, studentId);
        SetTenant(tenantId);

        // Act: complete item 0 — course not yet complete
        var first = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (before, percentageBefore) = await LoadEnrollmentState(enrollmentId);
        before.Should().BeNull(because: "course still has an incomplete item");
        percentageBefore.Should().Be(50.0);

        // The flip: completing the last active item completes the course
        var second = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 1 });
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (after, percentageAfter) = await LoadEnrollmentState(enrollmentId);
        after.Should().NotBeNull();
        percentageAfter.Should().Be(100.0);
        var completedAt = after!.Value;

        // Re-completion attempts never re-open or re-stamp the completion
        var retry = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 1 });
        retry.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var retry2 = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });
        retry2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (finalOn, _) = await LoadEnrollmentState(enrollmentId);
        finalOn.Should().Be(completedAt, because: "the completion timestamp is stamped exactly once");
    }

    [Fact]
    public async Task CompletingSomeItems_Should_NotSetCompletedOn()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (_, enrollmentId) = await SeedEnrolledPublishedCourse(tenantId, studentId);
        SetTenant(tenantId);

        var response = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (completedOn, percentage) = await LoadEnrollmentState(enrollmentId);
        completedOn.Should().BeNull();
        percentage.Should().Be(50.0);
    }
}
