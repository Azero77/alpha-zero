using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

public class CourseCrudTests : BaseIntegrationTest
{
    public CourseCrudTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<(Guid CourseId, Guid SubjectId)> SeedCourse(Guid tenantId, string title = "CRUD Course")
    {
        SetTenant(tenantId);
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = title, SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;
        return (courseId, subjectId);
    }

    [Fact]
    public async Task UpdateCourse_Should_Return204_And_PersistChanges()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, subjectId) = await SeedCourse(tenantId);
        var (_, newSubjectId) = await SeedCourse(tenantId, "SeedSource");

        // Act
        var response = await Client.PutAsJsonAsync($"/courses/{courseId}", new
        {
            CourseId = courseId,
            Title = "Renamed Course",
            Description = "Updated description",
            SubjectId = newSubjectId
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var course = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        course.Should().NotBeNull();
        course!.Title.Should().Be("Renamed Course");
        course.SubjectId.Should().Be(newSubjectId);

        // Brief text persisted in DB with subject reassignment
        await ExecuteDbContextAsync(async db =>
        {
            var persisted = await db.Courses.FirstAsync(c => c.Id == courseId);
            persisted.Title.Should().Be("Renamed Course");
            persisted.Description.Should().Be("Updated description");
            persisted.SubjectId.Should().Be(newSubjectId);
            persisted.SubjectId.Should().NotBe(subjectId);
        });
    }

    [Fact]
    public async Task UpdateCourse_Should_Return400_WhenTitleIsEmpty()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, subjectId) = await SeedCourse(tenantId);

        // Act
        var response = await Client.PutAsJsonAsync($"/courses/{courseId}", new
        {
            CourseId = courseId,
            Title = "",
            SubjectId = subjectId
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateCourse_Should_Return404_WhenCourseDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (_, subjectId) = await SeedCourse(tenantId);

        // Act
        var response = await Client.PutAsJsonAsync($"/courses/{Guid.NewGuid()}", new
        {
            CourseId = Guid.NewGuid(),
            Title = "Ghost Course",
            SubjectId = subjectId
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCourse_Should_Return404_WhenCourseBelongsToAnotherTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (courseIdOfA, subjectIdOfB) = await SeedCourse(tenantA);
        var (_, subjectIdB) = await SeedCourse(tenantB);

        // Act: tenant B tries to "fix" tenant A's course
        SetTenant(tenantB);
        var response = await Client.PutAsJsonAsync($"/courses/{courseIdOfA}", new
        {
            CourseId = courseIdOfA,
            Title = "Hostile Update",
            SubjectId = subjectIdB
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Tenant A's course must be untouched
        SetTenant(tenantA);
        var course = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseIdOfA}");
        course!.Title.Should().Be("CRUD Course");
    }

    [Fact]
    public async Task UpdateCourse_Should_Return404_WhenSubjectDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, _) = await SeedCourse(tenantId);

        // Act
        var response = await Client.PutAsJsonAsync($"/courses/{courseId}", new
        {
            CourseId = courseId,
            Title = "Renamed Course",
            SubjectId = Guid.NewGuid()
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
