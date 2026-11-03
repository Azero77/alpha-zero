using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Application.Analytics.Queries.GetCourseAnalytics;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Pool;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Get;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using AlphaZero.Modules.Courses.Domain.Audits;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Application;
using Autofac;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Courses.Tests.Integration;

/// <summary>
/// Enrollment lifecycle endpoints (issue #34): suspend/reactivate roundtrips, idempotency,
/// invalid-transition domain errors at the HTTP boundary, cross-tenant denial.
/// </summary>
public class EnrollmentLifecycleEndpointTests : BaseIntegrationTest
{
    public EnrollmentLifecycleEndpointTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedAvailableVideoAsset(Guid courseId, Guid tenantId, string title = "Test Video")
    {
        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, videoArn, title, courseId).Value;
        asset.MarkAvailable();

        DbContext.CourseAssets.Add(asset);
        await DbContext.SaveChangesAsync();
        return videoId;
    }

    private async Task<Guid> CreatePublishedCourse(Guid tenantId)
    {
        SetTenant(tenantId);

        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Lifecycle Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;

        var v1 = await SeedAvailableVideoAsset(courseId, tenantId, "L1");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v1}/assign", new AssignAssetRequest
        {
            CourseId = courseId, AssetId = v1, SectionId = sectionId, Title = "L1"
        });

        var v2 = await SeedAvailableVideoAsset(courseId, tenantId, "L2");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v2}/assign", new AssignAssetRequest
        {
            CourseId = courseId, AssetId = v2, SectionId = sectionId, Title = "L2"
        });

        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });

        return courseId;
    }

    private async Task<Guid> EnrollAndCompleteAnItem(Guid tenantId, Guid courseId, Guid studentId, int bitIndex = 0)
    {
        SetTenant(tenantId);
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        enrollResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var complete = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = bitIndex });
        complete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return enrollmentId;
    }

    // ------------------------------------------------------------------
    // Suspend → reactivate roundtrip with full state preservation
    // ------------------------------------------------------------------

    [Fact]
    public async Task Suspend_And_Reactivate_Should_PreserveProgressAndStatus_Metadata()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        var enrollmentId = await EnrollAndCompleteAnItem(tenantId, courseId, studentId);

        // Act: suspend
        var suspend = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { Reason = "billing dispute" });
        suspend.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var suspended = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        suspended!.Status.Should().Be(EnrollmentStatus.Suspended.ToString());
        suspended.StudentId.Should().Be(studentId);
        suspended.CompletionPercentage.Should().BeGreaterThan(0); // earned progress survives suspension
        suspended.LastStatusChangedOn.Should().NotBeNull(); // story #9: last-transition who/when at the read boundary
        suspended.LastStatusChangedBy.Should().NotBeNull(); // the API actor fired this transition

        // Idempotent re-suspend (story #3)
        var reSuspend = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { Reason = "billing dispute" });
        reSuspend.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: reactivate (lift the suspension — story #4)
        var reactivate = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/reactivate", new { });
        reactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reactivated = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        reactivated!.Status.Should().Be(EnrollmentStatus.Active.ToString());
        reactivated.CompletionPercentage.Should().Be(suspended.CompletionPercentage); // progress intact — story #2

        // Idempotent re-reactivate (story #11)
        var reReactivate = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/reactivate", new { });
        reReactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reactivate_Should_ReviveDeactivatedEnrollment_KeepingProgress()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();

        SetTenant(tenantId);
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;
        var complete = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 });
        complete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Arrange: an enrollment deactivated out-of-band (in this base there is no HTTP deactivate
        // route — the admin deactivate-on-id endpoint belongs to #29): put it Inactive directly seeded
        var enrollment = await DbContext.Enrollments.FirstAsync(e => e.Id == enrollmentId);
        enrollment.Deactivate().IsError.Should().BeFalse();
        await DbContext.SaveChangesAsync();

        // Act: resume payment resolved → reactivate (story #1)
        var reactivate = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/reactivate", new { });
        reactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        dto!.Status.Should().Be(EnrollmentStatus.Active.ToString());
        dto.CompletionPercentage.Should().BeGreaterThan(0);
    }

    // ------------------------------------------------------------------
    // Invalid transition — the domain error surfaces at the HTTP boundary (story #8)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Suspend_Should_Return409_WithDomainCode_WhenEnrollmentIsInactive()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();

        SetTenant(tenantId);
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        var enrollment = await DbContext.Enrollments.FirstAsync(e => e.Id == enrollmentId);
        enrollment.Deactivate().IsError.Should().BeFalse();
        await DbContext.SaveChangesAsync();

        // Act: suspending an inactive enrollment is not a reachable transition in the matrix
        var response = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { Reason = "test" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Enrollment.InvalidTransition");
    }

    // ------------------------------------------------------------------
    // Audit trail (rework §3.1, step 1): every lifecycle transition stages exactly one
    // EnrollmentAuditEntries row, in the same save transaction as the aggregate mutation
    // (idempotent repeats stage nothing).
    // ------------------------------------------------------------------

    [Fact]
    public async Task Lifecycle_Transitions_Should_StageExactlyOneAuditRowPerTransition()
    {
        // Arrange: enroll → the only pre-transition state is Active
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();

        SetTenant(tenantId);
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        // Act 1: moderator suspends via HTTP (admin actor — story #10 envelope)
        var suspend = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { Reason = "billing dispute" });
        suspend.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Idempotent duplicate suspend (story #4): must NOT stage a second row
        var reSuspend = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { Reason = "billing dispute" });
        reSuspend.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: exactly one Suspended row, atomically with the aggregate save that moved it
        var suspendedRows = await AuditRowsFor(enrollmentId);
        suspendedRows.Where(r => r.EventKind == EnrollmentAuditEventKind.Suspended).Should().HaveCount(1);
        var suspendedRow = suspendedRows.Single(r => r.EventKind == EnrollmentAuditEventKind.Suspended);
        suspendedRow.SubjectId.Should().Be(enrollmentId);
        suspendedRow.TenantId.Should().Be(tenantId);
        suspendedRow.ActorId.Should().NotBeNull(); // the request's acting principal fired this
        suspendedRow.Metadata["From"].Should().Be(nameof(EnrollmentStatus.Active));
        suspendedRow.Metadata["To"].Should().Be(nameof(EnrollmentStatus.Suspended));
        suspendedRow.Metadata["cause"].Should().Be("billing dispute");

        // Act 2: reactivate
        var reactivate = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/reactivate", new { });
        reactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resumedRow = (await AuditRowsFor(enrollmentId)).Single(r => r.EventKind == EnrollmentAuditEventKind.Resumed);
        resumedRow.ActorId.Should().NotBeNull();
        resumedRow.Metadata["From"].Should().Be(nameof(EnrollmentStatus.Suspended));
        resumedRow.Metadata["To"].Should().Be(nameof(EnrollmentStatus.Active));

        // Act 3: deactivate outside a request (revocation-saga shape) — through the module's own
        // container so the domain event flows through the in-process audit writer, the same
        // UnitOfWork path a saga consumer takes. Nothing goes on the module bus for deactivation.
        var coursesModule = Factory.Services.GetRequiredService<AlphaZero.Modules.Courses.Presentation.CoursesModule>();
        using var moduleScope = coursesModule.CreateScope();
        var db = moduleScope.Resolve<AppDbContext>();
        var enrollment = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId);

        // The audit row is staged by the DomainEventHandler when the EnrollmentDeactivatedDomainEvent
        // is published — and ONLY the EnrollmentStatusTransitionService publishes it (the aggregate's
        // own Deactivate() is deliberately event-silent; saga-shaped production callers all go
        // through this service, which is exactly the path being exercised).
        var transitionService = moduleScope.Resolve<AlphaZero.Modules.Courses.Domain.Services.EnrollmentStatusTransitionService>();
        var deactivate = await transitionService.Deactivate(
            enrollment,
            new AlphaZero.Modules.Courses.Domain.Services.EnrollmentStatusTransitionService.EnrollmentChangingStatusMetadata(null),
            CancellationToken.None);
        deactivate.IsError.Should().BeFalse();

        var unitOfWork = moduleScope.Resolve<IUnitOfWork>();
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        var deactivatedRow = (await AuditRowsFor(enrollmentId)).Single(r => r.EventKind == EnrollmentAuditEventKind.Deactivated);
        deactivatedRow.ActorId.Should().BeNull(); // saga context — no acting principal
        deactivatedRow.Metadata["From"].Should().Be(nameof(EnrollmentStatus.Active));
        deactivatedRow.Metadata["To"].Should().Be(nameof(EnrollmentStatus.Inactive));
    }

    private async Task<IReadOnlyList<AlphaZero.Modules.Courses.Domain.Audits.EnrollmentAuditEntry>> AuditRowsFor(Guid enrollmentId)
    {
        IReadOnlyList<AlphaZero.Modules.Courses.Domain.Audits.EnrollmentAuditEntry> rows = null!;
        await ExecuteDbContextAsync(async db =>
        {
            // IgnoreQueryFilters: some callers verify audit rows while acting under a foreign
            // tenant (cross-tenant denial tests) — the EF tenant filter must not hide them.
            rows = await db.EnrollmentAuditEntries
                .IgnoreQueryFilters()
                .Where(r => r.SubjectId == enrollmentId)
                .OrderBy(r => r.OccurredOn)
                .ToListAsync();
        });
        return rows;
    }

    // ------------------------------------------------------------------
    // Story #10 in-process refusal (§3.5): a module dispatch with NO HTTP context and NO
    // transport claim must be refused BEFORE the aggregate is loaded — a nonexistent id
    // surfaces the guard error, not 404.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Lifecycle_Guard_Should_RefuseInProcessSends_WithoutTransportClaim()
    {
        var coursesModule = Factory.Services.GetRequiredService<AlphaZero.Modules.Courses.Presentation.CoursesModule>();

        var suspend = await coursesModule.Send(new AlphaZero.Modules.Courses.Application.Enrollments.Commands.Suspend.SuspendEnrollmentCommand(Guid.NewGuid(), "in-process"));
        suspend.IsError.Should().BeTrue();
        suspend.FirstError.Code.Should().Be("Enrollment.TransitionUnauthorized");

        var reactivate = await coursesModule.Send(new AlphaZero.Modules.Courses.Application.Enrollments.Commands.Reactivate.ReactivateEnrollmentCommand(Guid.NewGuid()));
        reactivate.IsError.Should().BeTrue();
        reactivate.FirstError.Code.Should().Be("Enrollment.TransitionUnauthorized");
    }

    // ------------------------------------------------------------------
    // Cross-tenant denial — a tenant B operator cannot touch tenant A's enrollment
    [Fact]
    public async Task Expiry_Should_ExcludeExpiredEnrollment_FromActiveCourseAnalytics()
    {
        // Arrange: full API flow so the running analytics counters carry the enrollment's
        // contribution (EnrollmentCreated + ItemCompleted integration events → analytics consumer)
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var studentId = Guid.NewGuid();
        var enrollmentId = await EnrollAndCompleteAnItem(tenantId, courseId, studentId);

        CourseAnalyticsDto? before = null;
        for (var i = 0; i < 30; i++)
        {
            var resp = await Client.GetAsync($"/courses/{courseId}/analytics");
            before = await resp.Content.ReadFromJsonAsync<CourseAnalyticsDto>();
            if (before!.TotalEnrollments > 0)
                break;
            await Task.Delay(100);
        }
        before!.TotalEnrollments.Should().Be(1); // the active enrollment counts (sanity)
        before.AverageCompletionPercentage.Should().BeGreaterThan(0);

        // stamp the access window as passed (short-TTL test clock: window just passed)
        await ExecuteDbContextAsync(async db =>
        {
            var enrollment = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId);
            typeof(Enrollment).GetProperty(nameof(Enrollment.AccessExpiresOn)) !.SetValue(
                enrollment, DateTime.UtcNow.AddSeconds(-5));
            await db.SaveChangesAsync();
        });

        var coursesModule = Factory.Services.GetRequiredService<AlphaZero.Modules.Courses.Presentation.CoursesModule>();
        using var moduleScope = coursesModule.CreateScope();
        var expiryService = moduleScope.Resolve<AlphaZero.Modules.Courses.Infrastructure.Expiry.IEnrollmentExpiryService>();

        // Act: one expiry scan pass (the hosted worker's tick — non-HTTP trigger)
        var expiredCount = await expiryService.ExpireDueEnrollmentsAsync(CancellationToken.None);

        // Assert (story #6): expired contribution is removed from the running analytics...
        expiredCount.Should().BeGreaterThan(0);
        var excluded = false;
        for (var i = 0; i < 30 && !excluded; i++)
        {
            var resp = await Client.GetAsync($"/courses/{courseId}/analytics");
            var after = await resp.Content.ReadFromJsonAsync<CourseAnalyticsDto>();
            excluded = after!.TotalEnrollments == 0 && after.AverageCompletionPercentage == 0;
            if (!excluded)
                await Task.Delay(100);
        }
        excluded.Should().BeTrue("the expired enrollment must stop counting in active-course analytics");

        // ...but retained in the roster with its earned progress
        var dto = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        dto!.Status.Should().Be(EnrollmentStatus.Expired.ToString());
        dto.CompletionPercentage.Should().BeGreaterThan(0);
    }

    // ------------------------------------------------------------------

    private async Task SeedPlanAccessWindowDirectly(Guid tenantId, Guid courseId, int days)
    {
        SetTenant(tenantId);
        // plan-owner sets the access window on the plan aggregate (story #5) — the db write goes
        // through the aggregate so the persisted column/model is exercised too
        await ExecuteDbContextAsync(async db =>
        {
            var course = await db.Courses.Include(c => c.Plans).FirstAsync(c => c.Id == courseId);
            course.AddPlan("Windowed", Guid.NewGuid(), days).IsError.Should().BeFalse();
            await db.SaveChangesAsync();
        });
    }

    private async Task<(Guid EnrollmentId, Guid StudentId)> SeedDueEnrollment(Guid tenantId, Guid courseId)
    {
        SetTenant(tenantId);
        var studentId = Guid.NewGuid();

        var enrollmentId = Guid.NewGuid();
        var enrollment = Enrollment.Create(
            enrollmentId, tenantId, studentId, courseId,
            accessExpiresOn: DateTime.UtcNow.AddSeconds(-5)).Value; // short-TTL test clock: window just passed
        var allActiveMask = new System.Collections.BitArray(1, true);
        enrollment.CompleteItem(0, allActiveMask).IsError.Should().BeFalse(); // earned progress must survive expiry

        await ExecuteDbContextAsync(async db =>
        {
            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();
        });

        return (enrollmentId, studentId);
    }

    [Fact]
    public async Task Expiry_Should_MarkDueEnrollmentExpired_AndPublishExpiryEvent()
    {
        // Arrange: plan-owner sets a 1-day access window on the course plan (story #5)
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        await SeedPlanAccessWindowDirectly(tenantId, courseId, days: 1);

        var (enrollmentId, _) = await SeedDueEnrollment(tenantId, courseId);

        RecordingPublishObserver.Clear();

        var bus = Factory.Services.GetRequiredService<MassTransit.IBus>();
        RecordingPublishObserver.ConnectOnce(bus);

        // Act: one expiry scan pass (the hosted worker's tick — non-HTTP trigger); resolved the
        // same way the in-host worker resolves it: through the module's own container
        var coursesModule = Factory.Services.GetRequiredService<AlphaZero.Modules.Courses.Presentation.CoursesModule>();
        using var moduleScope = coursesModule.CreateScope();
        var expiryService = moduleScope.Resolve<AlphaZero.Modules.Courses.Infrastructure.Expiry.IEnrollmentExpiryService>();
        var expiredCount = await expiryService.ExpireDueEnrollmentsAsync(CancellationToken.None);

        // Assert: external behavior — status readable as Expired through the read endpoint (story #6: retained in roster)
        expiredCount.Should().BeGreaterThan(0);
        var dto = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        dto!.Status.Should().Be(EnrollmentStatus.Expired.ToString());
        dto.CompletionPercentage.Should().BeGreaterThan(0); // not destroyed by expiry

        // and the expiry integration event went out on the module bus (story #7)
        for (var i = 0; i < 30 && !RecordingPublishObserver.Messages.OfType<AlphaZero.Modules.Courses.IntegrationEvents.EnrollmentExpiredIntegrationEvent>().Any(); i++)
            await Task.Delay(100);

        RecordingPublishObserver.Messages.OfType<AlphaZero.Modules.Courses.IntegrationEvents.EnrollmentExpiredIntegrationEvent>()
            .Should().Contain(e => e.EnrollmentId == enrollmentId && e.CourseId == courseId && e.TenantId == tenantId);
    }

    // ------------------------------------------------------------------
    // Cross-tenant denial — a tenant B operator cannot touch tenant A's enrollment
    // ------------------------------------------------------------------

    [Fact]
    public async Task Suspend_Should_Return404_WhenEnrollmentBelongsToAnotherTenant()
    {
        var tenantA = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantA);
        var studentId = Guid.NewGuid();
        var enrollmentId = await EnrollAndCompleteAnItem(tenantA, courseId, studentId);

        // Act: act from tenant B
        SetTenant(Guid.NewGuid());
        var response = await Client.PatchAsJsonAsync($"/courses/enrollments/{enrollmentId}/suspend", new { Reason = "cross-tenant" });

        // Assert: existence is masked, not leaked
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------
    // Step 8 (§3.4 path a): the PrincipalExpired lazy mark — duplicate events mark ONCE
    // (storm-safety), a cross-tenant dispatch resolves to nothing, and each mark ride the
    // §3.1 audit/integration-event flow.
    // ------------------------------------------------------------------

    [Fact]
    public async Task PrincipalExpired_Mark_Should_RunExactlyOnce_AcrossDuplicateDispatches_AndRejectCrossTenant()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);
        var (enrollmentId, studentId) = await SeedDueEnrollment(tenantId, courseId); // Active, window already elapsed

        var coursesModule = Factory.Services.GetRequiredService<AlphaZero.Modules.Courses.Presentation.CoursesModule>();
        SetTenant(tenantId);

        // Act: the same (UserId, CourseId) dispatched repeatedly — like duplicate PrincipalExpired
        // evaluations while marking is pending (§2 R2-Q5)
        var first = await coursesModule.Send(new AlphaZero.Modules.Courses.Application.Enrollments.Commands.MarkExpiredFromPrincipal.MarkEnrollmentExpiredFromPrincipalCommand(studentId, courseId));
        first.IsError.Should().BeFalse();

        var duplicate = await coursesModule.Send(new AlphaZero.Modules.Courses.Application.Enrollments.Commands.MarkExpiredFromPrincipal.MarkEnrollmentExpiredFromPrincipalCommand(studentId, courseId));
        duplicate.IsError.Should().BeFalse(); // cheap success — the storm-safety contract

        // ...and the SAME pair resolved from a FOREIGN tenant (a forged/stale event): masked,
        // not leaked, and it must not intercept the aggregate state.
        SetTenant(Guid.NewGuid());
        var crossTenant = await coursesModule.Send(new AlphaZero.Modules.Courses.Application.Enrollments.Commands.MarkExpiredFromPrincipal.MarkEnrollmentExpiredFromPrincipalCommand(studentId, courseId));
        crossTenant.IsError.Should().BeTrue("a cross-tenant (UserId, CourseId) must not resolve");
        crossTenant.FirstError!.Code.Should().Be("Enrollment.NotFound");

        // State: Expired; the mark ran once — exactly ONE Expired audit row written.
        AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.Enrollment? state = null!;
        await ExecuteDbContextAsync(async db =>
            state = await db.Enrollments.IgnoreQueryFilters().AsNoTracking().FirstAsync(e => e.Id == enrollmentId));
        state!.Status.Should().Be(AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.EnrollmentStatus.Expired);

        var rows = await AuditRowsFor(enrollmentId);
        rows.Count(r => r.EventKind == AlphaZero.Modules.Courses.Domain.Audits.EnrollmentAuditEventKind.Expired)
            .Should().Be(1, "duplicate PrincipalExpired evaluations must not write additional audit rows");
    }
}
