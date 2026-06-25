using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Application.Analytics.Queries.GetCourseAnalytics;
using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using AlphaZero.Modules.Courses.Presentation.Courses.AddItem;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Enrollements.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Queries;
using Microsoft.Extensions.DependencyInjection;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;

namespace Courses.Tests.Integration;

public class CourseAnalyticsTests : BaseIntegrationTest
{
    public CourseAnalyticsTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreatePublishedCourse(Guid tenantId)
    {
        SetTenant(tenantId);
        
        // Subject
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        // Course
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "AnalyticsCourse", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        // Content
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/lessons", new AddLessonRequest { Title = "L1", VideoId = Guid.NewGuid() });
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/lessons", new AddLessonRequest { Title = "L2", VideoId = Guid.NewGuid() });

        // Lifecycle
        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PostAsJsonAsync($"/courses/{courseId}/plans", new AddPlanRequest { Name = "Standard", PrincipalId = Guid.NewGuid() });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });

        return courseId;
    }

    [Fact]
    public async Task GetCourseAnalytics_Should_ReturnCorrectStats_AfterEnrollmentAndCompletion()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();
        SetTenant(tenantId);

        // Act 1: First student enrolls
        var enroll1Resp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = student1Id, CourseId = courseId });
        var enrollment1Id = (await enroll1Resp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        // Wait a bit for MassTransit in-memory consumer to process the event
        CourseAnalyticsDto? analytics1 = null;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(200);
            var analytics1Resp = await Client.GetAsync($"/courses/{courseId}/analytics");
            if (analytics1Resp.IsSuccessStatusCode)
            {
                analytics1 = await analytics1Resp.Content.ReadFromJsonAsync<CourseAnalyticsDto>();
                break;
            }
        }

        // Assert 1: Analytics should reflect 1 enrollment, 0% avg
        analytics1.Should().NotBeNull();
        analytics1!.TotalEnrollments.Should().Be(1);
        analytics1.AverageCompletionPercentage.Should().Be(0);

        // Act 2: Second student enrolls and completes first item (BitIndex = 0)
        var enroll2Resp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = student2Id, CourseId = courseId });
        var enrollment2Id = (await enroll2Resp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        await Client.PostAsJsonAsync($"/courses/enrollements/{enrollment2Id}/complete", new { BitIndex = 0 });
        
        // Wait a bit for MassTransit
        CourseAnalyticsDto? analytics2 = null;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(200);
            var analytics2Resp = await Client.GetAsync($"/courses/{courseId}/analytics");
            if (analytics2Resp.IsSuccessStatusCode)
            {
                var temp = await analytics2Resp.Content.ReadFromJsonAsync<CourseAnalyticsDto>();
                if (temp != null && temp.TotalEnrollments == 2 && temp.ItemCompletionRates.Any())
                {
                    analytics2 = temp;
                    break;
                }
            }
        }

        // Assert 2: Analytics should reflect 2 enrollments, total completion sum is 50, avg = 25%.
        analytics2.Should().NotBeNull();
        analytics2!.TotalEnrollments.Should().Be(2);
        analytics2.AverageCompletionPercentage.Should().Be(25.0); // 1 student at 0%, 1 at 50%
        
        // Per-item stats
        analytics2.ItemCompletionRates.Should().ContainSingle(x => x.BitIndex == 0);
        var itemStat = analytics2.ItemCompletionRates.First(x => x.BitIndex == 0);
        itemStat.CompletedCount.Should().Be(1);
        itemStat.CompletionPercentage.Should().Be(50.0); // 1 out of 2 students completed it
    }

    [Fact]
    public async Task ListStudentProgress_Should_ReturnAllEnrolledStudents()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();
        SetTenant(tenantId);

        var enroll1Resp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = student1Id, CourseId = courseId });
        var enrollment1Id = (await enroll1Resp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var enroll2Resp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = student2Id, CourseId = courseId });
        var enrollment2Id = (await enroll2Resp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        await Client.PostAsJsonAsync($"/courses/enrollements/{enrollment1Id}/complete", new { BitIndex = 0 });

        // Wait a bit
        await Task.Delay(500);

        // Act
        var studentsResp = await Client.GetFromJsonAsync<PagedResult<EnrollmentDto>>($"/courses/{courseId}/students");

        // Assert
        studentsResp.Should().NotBeNull();
        studentsResp!.TotalCount.Should().Be(2);
        studentsResp.Items.Should().Contain(x => x.StudentId == student1Id && x.CompletionPercentage == 50.0);
        studentsResp.Items.Should().Contain(x => x.StudentId == student2Id && x.CompletionPercentage == 0);
    }
}
