using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Audits;
using AlphaZero.Modules.Courses.Presentation.Courses.AccessTickets;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Security;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EnrollmentAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.Enrollment;

namespace Courses.Tests.Integration;

/// <summary>
/// Step 10 (issue #34 rework §3.2.4): Course access-ticket mint endpoint (stateful gate, lazy expiry).
/// Gated by `AccessControl("courses:View", ...)`, verifies enrollment state, marks lazy expiry (path b)
/// on elapsed windows with 409, rejects Suspended/Expired with 409, and mints stateless capability
/// tickets for Active enrollments.
/// </summary>
public class AccessTicketMintEndpointTests : BaseIntegrationTest
{
    public AccessTicketMintEndpointTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreateCourse(Guid tenantId)
    {
        SetTenant(tenantId);

        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Ticket Gate Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        return courseId;
    }

    [Fact]
    public async Task Mint_Should_Return200WithTicket_WhenEnrollmentActive_AndWindowValid()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreateCourse(tenantId);
        var studentId = Guid.NewGuid();

        SetTenant(tenantId);
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", studentId.ToString());

        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        // Set access window
        var enrollment = await DbContext.Enrollments.FirstAsync(e => e.Id == enrollmentId);
        var futureExpiresOn = DateTime.UtcNow.AddDays(14);
        var setExpiresProp = typeof(EnrollmentAggregate).GetProperty(nameof(EnrollmentAggregate.AccessExpiresOn));
        setExpiresProp!.SetValue(enrollment, futureExpiresOn);
        await DbContext.SaveChangesAsync();

        // Act: mint ticket
        var mintResp = await Client.PostAsJsonAsync($"/courses/{courseId}/access-tickets", new { });

        mintResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await mintResp.Content.ReadFromJsonAsync<IssueAccessTicketResponse>();
        body.Should().NotBeNull();
        body!.Ticket.Should().NotBeNullOrWhiteSpace();
        body.ExpiresInSeconds.Should().Be(7200);

        // Verify token payload
        var signer = Factory.Services.GetRequiredService<IAccessTicketSigner>();
        var valid = signer.TryVerifyTicket(body.Ticket, out var payload, out var error);
        valid.Should().BeTrue(error);
        payload.Should().NotBeNull();
        payload!.TenantId.Should().Be(tenantId);
        payload.UserId.Should().Be(studentId);
        payload.EnrollmentId.Should().Be(enrollmentId);
        payload.WindowEnd.Should().Be(new DateTimeOffset(futureExpiresOn, TimeSpan.Zero).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Mint_Should_Return200_ForUnlimitedEnrollment()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreateCourse(tenantId);
        var studentId = Guid.NewGuid();

        SetTenant(tenantId);
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", studentId.ToString());

        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        enrollResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var mintResp = await Client.PostAsJsonAsync($"/courses/{courseId}/access-tickets", new { });

        mintResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await mintResp.Content.ReadFromJsonAsync<IssueAccessTicketResponse>();
        body.Should().NotBeNull();
        body!.Ticket.Should().NotBeNullOrWhiteSpace();

        var signer = Factory.Services.GetRequiredService<IAccessTicketSigner>();
        signer.TryVerifyTicket(body.Ticket, out var payload, out _).Should().BeTrue();
        payload!.WindowEnd.Should().Be(DateTimeOffset.MaxValue.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Mint_Should_Return409_WhenEnrollmentSuspended()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreateCourse(tenantId);
        var studentId = Guid.NewGuid();

        SetTenant(tenantId);
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", studentId.ToString());

        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var enrollment = await DbContext.Enrollments.FirstAsync(e => e.Id == enrollmentId);
        enrollment.Suspend().IsError.Should().BeFalse();
        await DbContext.SaveChangesAsync();

        var mintResp = await Client.PostAsJsonAsync($"/courses/{courseId}/access-tickets", new { });

        mintResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Mint_Should_Return409_AndLazyMarkExpired_WhenAccessWindowElapsed()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreateCourse(tenantId);
        var studentId = Guid.NewGuid();

        SetTenant(tenantId);
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", studentId.ToString());

        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        // Set access window in the past
        var enrollment = await DbContext.Enrollments.FirstAsync(e => e.Id == enrollmentId);
        var pastExpiresOn = DateTime.UtcNow.AddMinutes(-30);
        var setExpiresProp = typeof(EnrollmentAggregate).GetProperty(nameof(EnrollmentAggregate.AccessExpiresOn));
        setExpiresProp!.SetValue(enrollment, pastExpiresOn);
        await DbContext.SaveChangesAsync();

        // Act: mint triggers path b lazy-mark
        var mintResp = await Client.PostAsJsonAsync($"/courses/{courseId}/access-tickets", new { });

        mintResp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Assert: enrollment flipped to Expired in DB
        var reloaded = await DbContext.Enrollments.AsNoTracking().FirstAsync(e => e.Id == enrollmentId);
        reloaded.Status.Should().Be(EnrollmentStatus.Expired);

        // Assert: audit entry was recorded
        var audit = await DbContext.EnrollmentAuditEntries
            .FirstOrDefaultAsync(a => a.SubjectId == enrollmentId && a.EventKind == EnrollmentAuditEventKind.Expired);
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task Mint_Should_Return409_WhenEnrollmentAlreadyExpired()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreateCourse(tenantId);
        var studentId = Guid.NewGuid();

        SetTenant(tenantId);
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", studentId.ToString());

        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var enrollment = await DbContext.Enrollments.FirstAsync(e => e.Id == enrollmentId);
        enrollment.MarkExpired().IsError.Should().BeFalse();
        await DbContext.SaveChangesAsync();

        var mintResp = await Client.PostAsJsonAsync($"/courses/{courseId}/access-tickets", new { });

        mintResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Mint_Should_Return404_WhenNoEnrollmentFound()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreateCourse(tenantId);
        var nonEnrolledStudent = Guid.NewGuid();

        SetTenant(tenantId);
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", nonEnrolledStudent.ToString());

        var mintResp = await Client.PostAsJsonAsync($"/courses/{courseId}/access-tickets", new { });

        mintResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
