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
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

public class SectionCrudTests : BaseIntegrationTest
{
    public SectionCrudTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<(Guid CourseId, List<Guid> SectionIds)> SeedCourseWithTwoSections(Guid tenantId)
    {
        SetTenant(tenantId);

        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Sections Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S2" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");

        return (courseId, courseData!.Sections.Select(s => s.Id).ToList());
    }

    [Fact]
    public async Task RenameSection_Should_Return204_And_PersistNewTitle()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, sectionIds) = await SeedCourseWithTwoSections(tenantId);

        // Act
        var response = await Client.PutAsJsonAsync($"/courses/{courseId}/sections/{sectionIds[0]}", new
        {
            CourseId = courseId,
            SectionId = sectionIds[0],
            Title = "Renamed Section"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var course = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        course!.Sections.Should().ContainSingle(s => s.Title == "Renamed Section");
    }

    [Fact]
    public async Task RenameSection_Should_Return400_WhenTitleIsEmpty()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, sectionIds) = await SeedCourseWithTwoSections(tenantId);

        // Act
        var response = await Client.PutAsJsonAsync($"/courses/{courseId}/sections/{sectionIds[0]}", new
        {
            CourseId = courseId,
            SectionId = sectionIds[0],
            Title = ""
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RenameSection_Should_Return404_WhenSectionIsNotInThatCourse()
    {
        // Arrange: two separate courses in the same tenant
        var tenantId = Guid.NewGuid();
        var (courseA, _) = await SeedCourseWithTwoSections(tenantId);
        var (courseB, sectionsB) = await SeedCourseWithTwoSections(tenantId);

        // Act: rename courseA's route with courseB's section id
        var response = await Client.PutAsJsonAsync($"/courses/{courseA}/sections/{sectionsB[0]}", new
        {
            CourseId = courseA,
            SectionId = sectionsB[0],
            Title = "Wrong Course"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _ = courseB;
    }

    [Fact]
    public async Task RenameSection_Should_Return404_WhenCourseBelongsToAnotherTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (courseIdA, sectionIdsA) = await SeedCourseWithTwoSections(tenantA);

        SetTenant(tenantB);
        var response = await Client.PutAsJsonAsync($"/courses/{courseIdA}/sections/{sectionIdsA[0]}", new
        {
            CourseId = courseIdA,
            SectionId = sectionIdsA[0],
            Title = "Hostile Rename"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Untouched
        SetTenant(tenantA);
        var course = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseIdA}");
        course!.Sections.Should().ContainSingle(s => s.Title == "S1");
    }

    [Fact]
    public async Task DeleteSection_Should_Return204_And_HideSection()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, sectionIds) = await SeedCourseWithTwoSections(tenantId);

        // Act
        var response = await Client.DeleteAsync($"/courses/{courseId}/sections/{sectionIds[0]}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Read path: section no longer visible
        var course = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        course!.Sections.Should().HaveCount(1);
        course.Sections.Should().ContainSingle(s => s.Title == "S2");

        // Persistence: soft-deleted row retained with IsDeleted marker
        await ExecuteDbContextAsync(async db =>
        {
            var section = await db.Set<CourseSection>()
                .IgnoreQueryFilters()
                .FirstAsync(s => s.Id == sectionIds[0]);
            section.IsDeleted.Should().BeTrue();
            section.OnDeleted.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task DeleteSection_Should_Return404_WhenSectionIsNotInThatCourse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, _) = await SeedCourseWithTwoSections(tenantId);

        // Act
        var response = await Client.DeleteAsync($"/courses/{courseId}/sections/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSection_Should_RecomputeEnrollmentProgress_AgainstRemainingActiveItems()
    {
        // Arrange: published course with 2 items in S1 and 1 item in S2 (3 active total)
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var (courseId, sectionIds) = await SeedCourseWithTwoSections(tenantId);

        var v1 = await SeedAvailableVideoAsset(tenantId, courseId, "L1");
        var v2 = await SeedAvailableVideoAsset(tenantId, courseId, "L2");
        var v3 = await SeedAvailableVideoAsset(tenantId, courseId, "L3");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v1}/assign",
            new AssignAssetRequest { CourseId = courseId, AssetId = v1, SectionId = sectionIds[0], Title = "L1" });
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v2}/assign",
            new AssignAssetRequest { CourseId = courseId, AssetId = v2, SectionId = sectionIds[0], Title = "L2" });
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v3}/assign",
            new AssignAssetRequest { CourseId = courseId, AssetId = v3, SectionId = sectionIds[1], Title = "L3" });

        // Publish (submit review → approve → plan → publish)
        var reviewResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        reviewResp.StatusCode.Should().Be(HttpStatusCode.NoContent, because: "test setup must yield an under-review course");
        var approveResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        approveResp.StatusCode.Should().Be(HttpStatusCode.NoContent, because: "test setup must yield an approved course");
        await Client.PostAsJsonAsync($"/courses/{courseId}/plans", new AddPlanRequest { Name = "Standard", PrincipalId = Guid.NewGuid() });
        var publishResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });
        publishResp.StatusCode.Should().Be(HttpStatusCode.NoContent, because: "test setup must yield a published course");

        // Student enrolls (progress denominator includes all 3 items) and completes bit index 2 (the S2 item)
        var studentId = Guid.NewGuid();
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var completeResp = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 2 });
        completeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var before = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        before!.CompletionPercentage.Should().BeApproximately(33.33, 0.01, because: "1 of 3 active items complete before the section delete");

        // Act: instructor deletes the section holding bit indexes 0 and 1
        var deleteResp = await Client.DeleteAsync($"/courses/{courseId}/sections/{sectionIds[0]}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: the percentage remarshals against the single remaining active item
        var after = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        after!.CompletionPercentage.Should().Be(100.0);
        after.Status.Should().Be("Active");

        // Earned completion bits of surviving items are never regressed: the S2 item is still complete in the DB
        await ExecuteDbContextAsync(async db =>
        {
            var persisted = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId);
            persisted.Progress.CompletionPercentage.Should().Be(100.0);
        });
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
}
