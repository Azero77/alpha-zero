using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using ArchCourseStatus = AlphaZero.Modules.Courses.Domain.Aggregates.Courses.CourseStatus;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

public class CourseLifecycleTests : BaseIntegrationTest
{
    public CourseLifecycleTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<(Guid CourseId, Guid SectionId)> SeedCourseWithSection(Guid tenantId)
    {
        SetTenant(tenantId);
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Lifecycle Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        return (courseId, courseData!.Sections.First().Id);
    }

    // ——— Archive ————————————————————————————————————————————————————————————

    [Fact]
    public async Task ArchiveCourse_Should_Return204_And_PersistArchivedStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, _) = await SeedCourseWithSection(tenantId);

        // Act
        var response = await Client.PatchAsJsonAsync($"/courses/{courseId}/archive", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var course = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        course!.Status.Should().Be("Archived");

        await ExecuteDbContextAsync(async db =>
        {
            var persisted = await db.Courses.FirstAsync(c => c.Id == courseId);
            persisted.Status.Should().Be(ArchCourseStatus.Archived);
        });
    }

    [Fact]
    public async Task ArchiveCourse_Should_Return409_WhenAlreadyArchived()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, _) = await SeedCourseWithSection(tenantId);
        await Client.PatchAsJsonAsync($"/courses/{courseId}/archive", new { });

        // Act
        var second = await Client.PatchAsJsonAsync($"/courses/{courseId}/archive", new { });

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ArchiveCourse_Should_Return404_WhenCourseDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedCourseWithSection(tenantId);

        // Act
        var response = await Client.PatchAsJsonAsync($"/courses/{Guid.NewGuid()}/archive", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ——— Delete —————————————————————————————————————————————————————————————

    [Fact]
    public async Task DeleteCourse_Should_Return204_And_HideAllContent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, sectionId) = await SeedCourseWithSection(tenantId);

        // Act
        var response = await Client.DeleteAsync($"/courses/{courseId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Read path: course (and its sections) vanish from every view
        var getResponse = await Client.GetAsync($"/courses/{courseId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Persistence: the row (and its sections) survive with soft-delete markers
        await ExecuteDbContextAsync(async db =>
        {
            var course = await db.Courses.IgnoreQueryFilters().FirstAsync(c => c.Id == courseId);
            course.IsDeleted.Should().BeTrue();
            course.OnDeleted.Should().NotBeNull();

            var section = await db.CourseSections.IgnoreQueryFilters().FirstAsync(s => s.Id == sectionId);
            section.IsDeleted.Should().BeFalse(because: "only the course root is deleted; the global filter hides the whole tree from read paths");
        });
    }

    [Fact]
    public async Task DeleteCourse_Should_Return404_WhenCourseBelongsToAnotherTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (courseId, _) = await SeedCourseWithSection(tenantA);

        // Act
        SetTenant(tenantB);
        var response = await Client.DeleteAsync($"/courses/{courseId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Tenant A's course survives
        SetTenant(tenantA);
        var course = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        course!.Title.Should().Be("Lifecycle Course");
    }
}
