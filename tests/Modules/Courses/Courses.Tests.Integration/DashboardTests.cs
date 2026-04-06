using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Presentation.Courses.AddItem;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Enrollements.Dashboard;
using AlphaZero.Modules.Courses.Presentation.Enrollements.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;

namespace Courses.Tests.Integration;

public class DashboardTests : BaseIntegrationTest
{
    public DashboardTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreatePublishedCourse(Guid tenantId, string title)
    {
        SetTenant(tenantId);
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = title, SubjectId = subjectId });
        var courseId = Guid.Parse(courseResp.Headers.Location!.ToString().Split('/').Last());

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/lessons", new AddLessonRequest { Title = "L1", VideoId = Guid.NewGuid() });

        await Client.PostAsync($"/courses/{courseId}/review", null);
        await Client.PostAsync($"/courses/{courseId}/approve", null);
        await Client.PostAsync($"/courses/{courseId}/publish", null);

        return courseId;
    }

    [Fact]
    public async Task StudentDashboard_Should_GroupEnrollmentsByAcademy()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var academyA = Guid.NewGuid();
        var academyB = Guid.NewGuid();

        var courseA = await CreatePublishedCourse(academyA, "Academy A Course");
        var courseB = await CreatePublishedCourse(academyB, "Academy B Course");

        // Enroll Student in both (using their respective tenant contexts)
        SetTenant(academyA);
        await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseA });

        SetTenant(academyB);
        await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseB });

        // Act: Fetch Dashboard (Usually dashboard would be tenant-neutral or filter by user, 
        // but here the endpoint depends on StudentId and the provider might be bypassed for cross-tenant reading)
        // Let's see how the Query is implemented.
        var response = await Client.GetAsync($"/courses/dashboard/{studentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DashboardResponse>();
        
        result!.Academies.Should().HaveCount(2);
        result.Academies[academyA].Should().ContainSingle(e => e.CourseId == courseA);
        result.Academies[academyB].Should().ContainSingle(e => e.CourseId == courseB);
    }
}
