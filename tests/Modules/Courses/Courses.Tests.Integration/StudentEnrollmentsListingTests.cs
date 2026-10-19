using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Queries;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

public class StudentEnrollmentsListingTests : BaseIntegrationTest
{
    public StudentEnrollmentsListingTests(ApiFactory factory) : base(factory)
    {
    }

    // End-to-end enrollments require published courses; the listing's paging/status assertions are
    // cheaper to build over directly-seeded aggregate rows (same shape the enroll endpoint writes).
    private async Task SeedEnrollment(Guid tenantId, Guid studentId, Guid courseId, bool deactivate = false)
    {
        SetTenant(tenantId);
        var enrollment = Enrollment.Create(Guid.NewGuid(), tenantId, studentId, courseId).Value;
        if (deactivate)
        {
            // Completion requires the course's active mask; this seeded shape mirrors two allocated+active positions.
            var courseMask = new System.Collections.BitArray(2, true);
            enrollment.CompleteItem(0, courseMask);
            enrollment.Deactivate();
        }
        DbContext.Enrollments.Add(enrollment);
        await DbContext.SaveChangesAsync();
    }

    private async Task<(Guid CourseId, Guid SubjectId)> SeedSubjectAndCourse(Guid tenantId)
    {
        SetTenant(tenantId);
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = $"Course {Guid.NewGuid()}", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;
        return (courseId, subjectId);
    }

    [Fact]
    public async Task ListStudentEnrollments_Should_ReturnPagedListing_IncludingInactive()
    {
        // Arrange: one real end-to-end enrollment via the API (later deactivated), plus two seeded
        var tenantId = Guid.NewGuid();
        var (courseId, _) = await SeedSubjectAndCourse(tenantId);

        var studentId = Guid.NewGuid();
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var activeEnrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var (otherCourseId, _) = await SeedSubjectAndCourse(tenantId);
        await SeedEnrollment(tenantId, studentId, otherCourseId);
        var deactivatedCourseId = Guid.NewGuid();
        await SeedEnrollment(tenantId, studentId, deactivatedCourseId, deactivate: true);

        // Act
        var response = await Client.GetAsync($"/courses/enrollments?StudentId={studentId}&Page=1&PerPage=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<EnrollmentDto>>();
        page.Should().NotBeNull();
        page!.TotalCount.Should().Be(3);
        page.CurrentPage.Should().Be(1);
        page.PageSize.Should().Be(2);
        page.Items.Should().HaveCount(2);

        // Second page holds the remainder
        var page2 = await Client.GetFromJsonAsync<PagedResult<EnrollmentDto>>($"/courses/enrollments?StudentId={studentId}&Page=2&PerPage=2");
        page2!.Items.Should().HaveCount(1);

        // Statuses are visible in the listing: the deactivated seeded row must appear somewhere across pages
        var combined = page.Items.Concat(page2.Items).ToList();
        combined.Should().Contain(e => e.Id == activeEnrollmentId && e.Status == "Active");
        combined.Should().Contain(e => e.Status == "Inactive");
    }

    [Fact]
    public async Task ListStudentEnrollments_Should_ExposeStatusAndDatesAndScopeToTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (courseIdA, _) = await SeedSubjectAndCourse(tenantA);
        var (courseIdB, _) = await SeedSubjectAndCourse(tenantB);

        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();

        await SeedEnrollment(tenantA, studentA, courseIdA);
        await SeedEnrollment(tenantB, studentB, courseIdB);

        // Act: admin in tenant A inspects tenant A's student
        SetTenant(tenantA);
        var response = await Client.GetAsync($"/courses/enrollments?StudentId={studentA}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<EnrollmentDto>>();
        page!.TotalCount.Should().Be(1);
        var item = page.Items.Single();
        item.StudentId.Should().Be(studentA);
        item.CourseId.Should().Be(courseIdA);
        item.EnrolledOn.Should().BeBefore(DateTime.UtcNow);

        // Cross-tenant blind spot: tenant A admin querying tenant B's student sees nothing
        var crossTenant = await Client.GetAsync($"/courses/enrollments?StudentId={studentB}");
        var crossPage = await crossTenant.Content.ReadFromJsonAsync<PagedResult<EnrollmentDto>>();
        crossPage!.TotalCount.Should().Be(0);
        crossPage.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListStudentEnrollments_Should_Return400_WhenStudentIdMissing()
    {
        // Act
        var response = await Client.GetAsync("/courses/enrollments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListStudentEnrollments_Should_Return400_WhenPerPageIsOutOfRange()
    {
        // Act
        var response = await Client.GetAsync($"/courses/enrollments?StudentId={Guid.NewGuid()}&Page=0&PerPage=500");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
