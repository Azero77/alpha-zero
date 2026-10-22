using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
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
            var course = await db.Courses.AsNoTracking().SingleAsync(c => c.Id == e.CourseId);
            return (e.CompletedOn, ProgressCalculation.CompletionPercentage(e.Progress.Bitmask, course.ActiveItemsBitmask));
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

    // --- Concurrent completions (#32/D3): the rowversion token + retry-once wave ---
    // Both call shapes converge deterministically no matter how the requests interleave:
    // the loser either loads fresh state (no duplicate milestone) or conflicts at
    // SaveChanges and retries against fresh loads — the persisted CompletedOn is stamped
    // exactly once either way.

    [Fact]
    public async Task ConcurrentCompletionOfSameLastItem_Should_StampCompletedOn_ExactlyOnce()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (_, enrollmentId) = await SeedEnrolledPublishedCourse(tenantId, studentId);
        SetTenant(tenantId);

        // First item out of the way — both parallel requests race for the LAST item
        await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });

        var taskA = Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 1 });
        var taskB = Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 1 });
        var (respA, respB) = (await taskA, await taskB);

        // The loser must NOT surface as an error: it either observes fresh state or retries
        respA.StatusCode.Should().Be(HttpStatusCode.NoContent);
        respB.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (after, percentage) = await LoadEnrollmentState(enrollmentId);
        after.Should().NotBeNull(because: "the completion transition happened exactly once");
        percentage.Should().Be(100.0);

        // A re-drive of the same bit never re-stamps it
        await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 1 });
        var (idempotent, _) = await LoadEnrollmentState(enrollmentId);
        idempotent.Should().Be(after, because: "the completion timestamp is stamped exactly once");
    }

    [Fact]
    public async Task ConcurrentCompletionOfDifferentFinalBits_Should_NotLoseABit()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (_, enrollmentId) = await SeedEnrolledPublishedCourse(tenantId, studentId);
        SetTenant(tenantId);

        // Two writers complete the two different final items at the same moment. Without
        // the rowversion token the last writer's mask overwrote the other bit — the course
        // silently never completed ("different final bits → no lost bit, event fires", D3).
        var taskA = Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });
        var taskB = Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 1 });
        var (respA, respB) = (await taskA, await taskB);

        respA.StatusCode.Should().Be(HttpStatusCode.NoContent);
        respB.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (after, percentage) = await LoadEnrollmentState(enrollmentId);
        after.Should().NotBeNull(because: "both bits are present, so the course completed and the event fired");
        percentage.Should().Be(100.0, because: "neither concurrent writer's bit may be lost");
    }
}
