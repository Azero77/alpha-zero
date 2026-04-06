using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Presentation.Courses.AddItem;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Reorder.Sections;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

public class CourseTests : BaseIntegrationTest
{
    public CourseTests(ApiFactory factory) : base(factory)
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

    [Fact]
    public async Task CreateCourse_Should_ReturnCreated_WhenRequestIsValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var request = new CreateCourseRequest { Title = "C# Basics", Description = "Learn C#", SubjectId = subjectId };

        // Act
        var response = await Client.PostAsJsonAsync("/courses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var courseIdStr = response.Headers.Location?.ToString().Split('/').Last();
        Guid.TryParse(courseIdStr, out var courseId).Should().BeTrue();

        var course = await DbContext.Courses.FindAsync(courseId);
        course.Should().NotBeNull();
        course!.Title.Should().Be("C# Basics");
        course.Status.ToString().Should().Be("Draft");
    }

    [Fact]
    public async Task CurriculumBuilding_Should_AddSectionsAndLessons()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var createCourseResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "C# Basics", SubjectId = subjectId });
        var courseId = Guid.Parse(createCourseResponse.Headers.Location!.ToString().Split('/').Last());

        // Act: Add Section
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "Introduction" });
        
        // Get course to find SectionId
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseResponse!.Sections.First().Id;

        // Act: Add Lesson
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/lessons", new AddLessonRequest 
        { 
            Title = "Hello World", 
            VideoId = Guid.NewGuid() 
        });

        // Assert
        var updatedCourseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        updatedCourseResponse!.Sections.Should().HaveCount(1);
        updatedCourseResponse.Sections.First().Items.Should().HaveCount(1);
        updatedCourseResponse.Sections.First().Items.First().Title.Should().Be("Hello World");
    }

    [Fact]
    public async Task ReorderSections_Should_UpdateOrders()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var createCourseResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Course", SubjectId = subjectId });
        var courseId = Guid.Parse(createCourseResponse.Headers.Location!.ToString().Split('/').Last());

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S2" });

        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var s1Id = courseResponse!.Sections.First(s => s.Title == "S1").Id;
        var s2Id = courseResponse.Sections.First(s => s.Title == "S2").Id;

        // Act: Reorder (S2 first)
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/reorder", new ReorderSectionsRequest 
        { 
            SectionIds = new List<Guid> { s2Id, s1Id } 
        });

        // Assert
        var finalCourseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        finalCourseResponse!.Sections[0].Id.Should().Be(s2Id);
        finalCourseResponse.Sections[1].Id.Should().Be(s1Id);
    }

    [Fact]
    public async Task ReorderItems_Should_UpdateOrdersWithinSection()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var createCourseResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Course", SubjectId = subjectId });
        var courseId = Guid.Parse(createCourseResponse.Headers.Location!.ToString().Split('/').Last());

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseResponse!.Sections.First().Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/lessons", new AddLessonRequest { Title = "L1", VideoId = Guid.NewGuid() });
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/lessons", new AddLessonRequest { Title = "L2", VideoId = Guid.NewGuid() });

        var sectionResponse = (await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}"))!.Sections.First();
        var l1Id = sectionResponse.Items.First(i => i.Title == "L1").Id;
        var l2Id = sectionResponse.Items.First(i => i.Title == "L2").Id;

        // Act
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/reorder", new { ItemIds = new List<Guid> { l2Id, l1Id } });

        // Assert
        var finalSectionResponse = (await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}"))!.Sections.First();
        finalSectionResponse.Items[0].Id.Should().Be(l2Id);
        finalSectionResponse.Items[1].Id.Should().Be(l1Id);
    }

    [Fact]
    public async Task CreateCourse_Should_Fail_WhenSubjectBelongsToDifferentTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        
        var subjectA = await SeedSubject(tenantA);
        
        SetTenant(tenantB);
        var request = new CreateCourseRequest { Title = "Illegal Course", SubjectId = subjectA };

        // Act
        var response = await Client.PostAsJsonAsync("/courses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // Subject not found in Tenant B's context
    }

    [Fact]
    public async Task RejectCourse_Should_Fail_WhenReasonIsMissing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var createResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "ToReject", SubjectId = subjectId });
        var courseId = Guid.Parse(createResp.Headers.Location!.ToString().Split('/').Last());

        // Act
        var response = await Client.PatchAsJsonAsync($"/courses/{courseId}/reject", new { Reason = "" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]

    public async Task CourseLifecycle_Should_TransitionCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var httpResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "LifeCycle", SubjectId = subjectId });
        var createCourseResponse = await httpResponse.Content.ReadFromJsonAsync<CreateCourseResponse>();
        var courseId = createCourseResponse!.Id;
        // Add content (required to submit)
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseResponse!.Sections.First().Id;
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/lessons", new AddLessonRequest { Title = "L1", VideoId = Guid.NewGuid() });

        // Act: Submit
        var submitResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        submitResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: Approve
        var approveResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        approveResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: Publish
        var publishResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });
        publishResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        var finalCourseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        finalCourseResponse!.Status.Should().Be("Published");
    }
}
