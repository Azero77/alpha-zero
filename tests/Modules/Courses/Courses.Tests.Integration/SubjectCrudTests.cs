using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Modules.Courses.Presentation.Subjects.List;
using AlphaZero.Shared.Queries;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

public class SubjectCrudTests : BaseIntegrationTest
{
    public SubjectCrudTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<(Guid CourseId, Guid SubjectId)> SeedCourse(Guid tenantId)
    {
        SetTenant(tenantId);
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "Math" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;
        return (courseId, subjectId);
    }

    private async Task<Guid> SeedSubject(Guid tenantId, string name = "Physics")
    {
        SetTenant(tenantId);
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = name });
        return (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;
    }

    [Fact]
    public async Task UpdateSubject_Should_Return204_And_PersistNewName()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId, "Old Name");

        // Act
        var response = await Client.PutAsJsonAsync($"/courses/subjects/{subjectId}", new
        {
            Id = subjectId,
            Name = "Applied Physics",
            Description = "updated"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ExecuteDbContextAsync(async db =>
        {
            var subject = await db.Subjects.FirstAsync(s => s.Id == subjectId);
            subject.Name.Should().Be("Applied Physics");
            subject.Description.Should().Be("updated");
        });
    }

    [Fact]
    public async Task UpdateSubject_Should_Return400_WhenNameIsEmpty()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);

        // Act
        var response = await Client.PutAsJsonAsync($"/courses/subjects/{subjectId}", new
        {
            Id = subjectId,
            Name = ""
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateSubject_Should_Return404_WhenSubjectDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedSubject(tenantId);

        // Act
        var response = await Client.PutAsJsonAsync($"/courses/subjects/{Guid.NewGuid()}", new
        {
            Id = Guid.NewGuid(),
            Name = "Ghost Subject"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateSubject_Should_Return404_WhenSubjectBelongsToAnotherTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var subjectIdOfA = await SeedSubject(tenantA, "TenantA Subject");

        // Act
        SetTenant(tenantB);
        var response = await Client.PutAsJsonAsync($"/courses/subjects/{subjectIdOfA}", new
        {
            Id = subjectIdOfA,
            Name = "Hostile Rename"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Untouched
        SetTenant(tenantA);
        await ExecuteDbContextAsync(async db =>
        {
            var subject = await db.Subjects.AsNoTracking().FirstAsync(s => s.Id == subjectIdOfA);
            subject.Name.Should().Be("TenantA Subject");
        });
    }

    [Fact]
    public async Task DeleteSubject_Should_Return409_WhenLiveCourseReferencesSubject()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, subjectId) = await SeedCourse(tenantId);

        // Act
        var response = await Client.DeleteAsync($"/courses/subjects/{subjectId}");

        // Assert — refused, subject and course stay intact
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var course = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        course!.SubjectId.Should().Be(subjectId);

        await ExecuteDbContextAsync(async db =>
        {
            var subject = await db.Subjects.AsNoTracking().FirstAsync(s => s.Id == subjectId);
            subject.IsDeleted.Should().BeFalse();
        });
    }

    [Fact]
    public async Task DeleteSubject_Should_Return204_And_DisappearFromLists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId, "Latin");

        // Act
        var response = await Client.DeleteAsync($"/courses/subjects/{subjectId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Subject lists hide the retired subject; DB row stays with IsDeleted marker
        var listResp = await Client.GetAsync("/courses/subjects");
        var list = await listResp.Content.ReadFromJsonAsync<PagedResult<SubjectDto>>();
        list!.Items.Should().NotContain(s => s.Id == subjectId);

        await ExecuteDbContextAsync(async db =>
        {
            var subject = await db.Subjects.IgnoreQueryFilters().AsNoTracking().FirstAsync(s => s.Id == subjectId);
            subject.IsDeleted.Should().BeTrue();
            subject.OnDeleted.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task DeleteSubject_Should_Return404_WhenSubjectDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedSubject(tenantId);

        // Act
        var response = await Client.DeleteAsync($"/courses/subjects/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
