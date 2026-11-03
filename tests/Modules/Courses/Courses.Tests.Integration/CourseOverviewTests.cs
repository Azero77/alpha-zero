using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Overview;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

public class CourseOverviewTests : BaseIntegrationTest
{
    public CourseOverviewTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedSubject(Guid tenantId)
    {
        SetTenant(tenantId);
        var response = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest 
        { 
            Name = "Computer Science", 
            Description = "CS" 
        });
        var result = await response.Content.ReadFromJsonAsync<CreateSubjectResponse>();
        return result!.Id;
    }

    private async Task<Guid> SeedCourse(Guid tenantId, Guid subjectId)
    {
        SetTenant(tenantId);
        var response = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest 
        { 
            Title = "Intro to CS", 
            Description = "A course", 
            SubjectId = subjectId 
        });
        
        var result = await response.Content.ReadFromJsonAsync<CreateCourseResponse>();
        return result!.Id;
    }

    [Fact]
    public async Task UpsertCourseOverview_WithValidData_ReturnsOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        var courseId = await SeedCourse(tenantId, subjectId);

        var request = new UpsertCourseOverviewRequest
        {
            DescriptionContent = JsonDocument.Parse("{\"type\":\"doc\",\"content\":[]}").RootElement,
            TargetAudienceContent = JsonDocument.Parse("{\"type\":\"doc\"}").RootElement
        };

        SetTenant(tenantId);
        
        // Act
        var response = await Client.PutAsJsonAsync($"/courses/{courseId}/overview", request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        // Verify Get
        var getResponse = await Client.GetAsync($"/courses/{courseId}/overview");
        
        // Wait, for get, if the course is not published it requires courses.overview:Manage
        // Since we created it, we have permissions (tenant user).
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await getResponse.Content.ReadFromJsonAsync<CourseOverviewResponse>();
        dto.Should().NotBeNull();
        dto!.CourseId.Should().Be(courseId);
    }

    [Fact]
    public async Task UpsertCourseOverview_PersistsContent_AndEvictsCachedOverview()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        var courseId = await SeedCourse(tenantId, subjectId);

        // Warm the cached overview so eviction is observable through the ETag.
        var warm = await Client.GetAsync($"/courses/{courseId}/overview");
        warm.StatusCode.Should().Be(HttpStatusCode.OK);
        var warmTag = warm.Headers.ETag!.Tag;

        var marker = $"persisted-{Guid.NewGuid():N}";
        var request = new UpsertCourseOverviewRequest
        {
            DescriptionContent = JsonDocument.Parse(
                $"{{\"type\":\"doc\",\"content\":[{{\"type\":\"paragraph\",\"content\":[{{\"type\":\"text\",\"text\":\"{marker}\"}}]}}]}}").RootElement
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/courses/{courseId}/overview", request);

        // Assert: the command is an ICommand now, so the UnitOfWork persists it.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var persisted = await ExecuteDbContextAsync(async db =>
            await db.CourseOverviews.AsNoTracking().FirstAsync(o => o.CourseId == courseId));
        persisted.DescriptionContent.Value.Should().Contain(marker);

        // The overview mutation carries the per-course tag, so the warmed GET is refreshed.
        var fresh = await Client.GetAsync($"/courses/{courseId}/overview");
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);
        fresh.Headers.ETag!.Tag.Should().NotBe(warmTag);
        fresh.Headers.ETag!.Tag.Should().Be(persisted.ETag);
    }
}
