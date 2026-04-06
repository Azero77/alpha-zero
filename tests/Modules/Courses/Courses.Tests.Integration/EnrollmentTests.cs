using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Presentation.Courses.AddItem;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Enrollements.Enroll;
using AlphaZero.Modules.Courses.Presentation.Enrollements.Get;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;

namespace Courses.Tests.Integration;

public class EnrollmentTests : BaseIntegrationTest
{
    public EnrollmentTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreatePublishedCourse(Guid tenantId)
    {
        SetTenant(tenantId);
        
        // Subject
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        // Course
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "C1", SubjectId = subjectId });
        var courseId = Guid.Parse(courseResp.Headers.Location!.ToString().Split('/').Last());

        // Content
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/lessons", new AddLessonRequest { Title = "L1", VideoId = Guid.NewGuid() });
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/lessons", new AddLessonRequest { Title = "L2", VideoId = Guid.NewGuid() });

        // Lifecycle
        await Client.PostAsync($"/courses/{courseId}/review", null);
        await Client.PostAsync($"/courses/{courseId}/approve", null);
        await Client.PostAsync($"/courses/{courseId}/publish", null);

        return courseId;
    }

    [Fact]
    public async Task Enroll_Should_CreateEnrollment_WhenCourseIsPublished()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        SetTenant(tenantId);

        // Act
        var response = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest 
        { 
            StudentId = studentId, 
            CourseId = courseId 
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var enrollmentId = Guid.Parse(response.Headers.Location!.ToString().Split('/').Last());

        var enrollment = await Client.GetFromJsonAsync<EnrollementResponse>($"/courses/enrollments/{enrollmentId}");
        enrollment.Should().NotBeNull();
        enrollment!.StudentId.Should().Be(studentId);
        enrollment.CourseId.Should().Be(courseId);
        enrollment.CompletionPercentage.Should().Be(0);
    }

    [Fact]
    public async Task CompleteItem_Should_UpdateProgress()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        SetTenant(tenantId);
        
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = Guid.Parse(enrollResp.Headers.Location!.ToString().Split('/').Last());

        // Act: Complete first item (BitIndex 0)
        var completeResp = await Client.PostAsJsonAsync($"/courses/enrollements/{enrollmentId}/complete", new { BitIndex = 0 });
        completeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: 1 of 2 items complete = 50%
        var enrollment = await Client.GetFromJsonAsync<EnrollementResponse>($"/courses/enrollments/{enrollmentId}");
        enrollment!.CompletionPercentage.Should().Be(50.0);
    }

    [Fact]
    public async Task Idempotency_Should_Fail_WhenEnrollingTwice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        SetTenant(tenantId);

        await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });

        // Act
        var response = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CompleteItem_Should_Fail_WhenIndexIsOutOfRange()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId); // Has 2 items
        var studentId = Guid.NewGuid();
        SetTenant(tenantId);
        
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = Guid.Parse(enrollResp.Headers.Location!.ToString().Split('/').Last());

        // Act: Try to complete bit index 5 (only 0 and 1 exist)
        var completeResp = await Client.PostAsJsonAsync($"/courses/enrollements/{enrollmentId}/complete", new { BitIndex = 5 });

        // Assert
        completeResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
