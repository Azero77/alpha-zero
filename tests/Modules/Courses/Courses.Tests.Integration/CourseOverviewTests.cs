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
        
        var result = await response.Content.ReadFromJsonAsync<Guid>();
        return result;
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

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify Get
        var getResponse = await Client.GetAsync($"/courses/{courseId}/overview");
        
        // Wait, for get, if the course is not published it requires courses.overview:Manage
        // Since we created it, we have permissions (tenant user).
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await getResponse.Content.ReadFromJsonAsync<CourseOverviewResponse>();
        dto.Should().NotBeNull();
        dto!.CourseId.Should().Be(courseId);
    }
}
