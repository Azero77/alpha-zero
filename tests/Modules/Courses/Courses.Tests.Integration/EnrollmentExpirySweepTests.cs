using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Audits;
using AlphaZero.Modules.Courses.Infrastructure.Expiry;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Courses.Presentation;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using Autofac;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EnrollmentAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.Enrollment;

namespace Courses.Tests.Integration;

/// <summary>
/// Step 11 (issue #34 rework §3.4(c)): Set-based sweep + worker hardening.
/// Replaces the legacy per-row aggregate scan with a single atomic set-based PostgreSQL UPDATE
/// with SQL-encoded guard (`Status='Active' AND "AccessExpiresOn" <= now`), inserts audit rows
/// in the same statement, bulk-publishes `EnrollmentExpiredIntegrationEvent`s, and is re-run idempotent.
/// </summary>
public class EnrollmentExpirySweepTests : BaseIntegrationTest
{
    public EnrollmentExpirySweepTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreateCourse(Guid tenantId, string title = "Sweep Course")
    {
        SetTenant(tenantId);

        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = $"Sub-{Guid.NewGuid():N}" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = title, SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        return courseId;
    }

    [Fact]
    public async Task SetBasedSweep_Should_MarkLargeBatchExpired_InsertAuditRows_AndPublishEvents_Idempotently()
    {
        // Arrange: seed multiple due enrollments across two tenants to verify cross-tenant sweep
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var courseA = await CreateCourse(tenantA, "Tenant A Course");
        var courseB = await CreateCourse(tenantB, "Tenant B Course");

        const int dueCountTenantA = 10;
        const int dueCountTenantB = 5;
        var dueEnrollmentIds = new List<Guid>();

        // Tenant A: 10 due enrollments
        SetTenant(tenantA);
        for (var i = 0; i < dueCountTenantA; i++)
        {
            var studentId = Guid.NewGuid();
            var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseA });
            var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;
            dueEnrollmentIds.Add(enrollmentId);
        }

        // Tenant B: 5 due enrollments
        SetTenant(tenantB);
        for (var i = 0; i < dueCountTenantB; i++)
        {
            var studentId = Guid.NewGuid();
            var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseB });
            var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;
            dueEnrollmentIds.Add(enrollmentId);
        }

        // Seed 2 non-due enrollments: one future access window, one unlimited (null)
        SetTenant(tenantA);
        var futureStudent = Guid.NewGuid();
        var futureResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = futureStudent, CourseId = courseA });
        var futureEnrollmentId = (await futureResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var unlimitedStudent = Guid.NewGuid();
        var unlimitedResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = unlimitedStudent, CourseId = courseA });
        var unlimitedEnrollmentId = (await unlimitedResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        // Set past AccessExpiresOn on due enrollments, and future on the future one
        var pastTime = DateTime.UtcNow.AddHours(-2);
        var futureTime = DateTime.UtcNow.AddDays(7);
        var setExpiresProp = typeof(EnrollmentAggregate).GetProperty(nameof(EnrollmentAggregate.AccessExpiresOn));

        var allEnrollments = await DbContext.Enrollments.IgnoreQueryFilters().ToListAsync();
        foreach (var e in allEnrollments)
        {
            if (dueEnrollmentIds.Contains(e.Id))
            {
                setExpiresProp!.SetValue(e, pastTime);
            }
            else if (e.Id == futureEnrollmentId)
            {
                setExpiresProp!.SetValue(e, futureTime);
            }
        }
        await DbContext.SaveChangesAsync();

        // Connect publish observer
        RecordingPublishObserver.Clear();
        var bus = Factory.Services.GetRequiredService<IBus>();
        RecordingPublishObserver.ConnectOnce(bus);

        // Act: Run set-based sweep pass
        var coursesModule = Factory.Services.GetRequiredService<CoursesModule>();
        using (var moduleScope = coursesModule.CreateScope())
        {
            var expiryService = moduleScope.Resolve<IEnrollmentExpiryService>();
            var expiredCount = await expiryService.ExpireDueEnrollmentsAsync(CancellationToken.None);

            // Assert: count of marked enrollments matches exactly
            expiredCount.Should().Be(dueEnrollmentIds.Count);
        }

        // Assert: all due enrollments are now Expired in the DB
        var reloadedDue = await DbContext.Enrollments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => dueEnrollmentIds.Contains(e.Id))
            .ToListAsync();
        reloadedDue.Should().HaveCount(dueEnrollmentIds.Count);
        reloadedDue.Should().OnlyContain(e => e.Status == EnrollmentStatus.Expired);

        // Assert: non-due enrollments are untouched (Active)
        var reloadedFuture = await DbContext.Enrollments.IgnoreQueryFilters().AsNoTracking().FirstAsync(e => e.Id == futureEnrollmentId);
        reloadedFuture.Status.Should().Be(EnrollmentStatus.Active);
        var reloadedUnlimited = await DbContext.Enrollments.IgnoreQueryFilters().AsNoTracking().FirstAsync(e => e.Id == unlimitedEnrollmentId);
        reloadedUnlimited.Status.Should().Be(EnrollmentStatus.Active);

        // Assert: audit rows were inserted set-based
        var auditRows = await DbContext.EnrollmentAuditEntries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => dueEnrollmentIds.Contains(a.SubjectId))
            .ToListAsync();
        auditRows.Should().HaveCount(dueEnrollmentIds.Count);
        auditRows.Should().OnlyContain(a => a.EventKind == EnrollmentAuditEventKind.Expired);
        auditRows.Should().OnlyContain(a => a.Metadata["From"] == "Active" && a.Metadata["To"] == "Expired");

        // Assert: integration events published for all expired enrollments
        var publishedEvents = RecordingPublishObserver.Messages
            .OfType<EnrollmentExpiredIntegrationEvent>()
            .ToList();
        publishedEvents.Select(e => e.EnrollmentId).Should().Contain(dueEnrollmentIds);

        // Act 2: Re-run the sweep immediately (idempotency check)
        RecordingPublishObserver.Clear();
        using (var moduleScope2 = coursesModule.CreateScope())
        {
            var expiryService2 = moduleScope2.Resolve<IEnrollmentExpiryService>();
            var secondPassCount = await expiryService2.ExpireDueEnrollmentsAsync(CancellationToken.None);

            // Assert: zero marks on second pass
            secondPassCount.Should().Be(0);
        }

        // No new events published on second pass
        RecordingPublishObserver.Messages
            .OfType<EnrollmentExpiredIntegrationEvent>()
            .Should().BeEmpty();
    }
}
