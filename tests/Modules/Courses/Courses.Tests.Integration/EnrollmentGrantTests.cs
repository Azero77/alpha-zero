using System.Net;
using System.Net.Http.Json;
using Autofac;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.UpdatePlan;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Presentation;
using AlphaZero.Modules.Identity.Application.Principals.Commands.EnsureCoursePlanPrincipal;

namespace Courses.Tests.Integration;

/// <summary>
/// Step 9 (§3.2.1/§3.2.2): the per-(course, plan) principal lifecycle — windowed plans mint a
/// managed-policy principal via Identity on plan creation, windowed enrollments are granted it at
/// the course ARN, unlimited enrollments keep the shared student template (no inline policies
/// anywhere — §5 rule 1).
/// </summary>
public class EnrollmentGrantTests : BaseIntegrationTest
{
    public EnrollmentGrantTests(ApiFactory factory) : base(factory)
    {
    }

    private AlphaZero.Modules.Identity.Infrastructure.Persistance.AppDbContext IdentityDb()
    {
        var identityModule = Factory.Services.GetRequiredService<IdentityModule>();
        var scope = identityModule.CreateScope();
        return scope.Resolve<AlphaZero.Modules.Identity.Infrastructure.Persistance.AppDbContext>();
    }

    private async Task<Guid> CreateCourse(Guid tenantId)
    {
        SetTenant(tenantId);

        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Grant Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        return courseId;
    }

    private async Task SeedIdentityStudentAsync(Guid tenantId, Guid userId, string username)
    {
        var db = IdentityDb();
        if (!await db.TenantUsers.AnyAsync(u => u.Id == userId))
        {
            db.TenantUsers.Add(TenantUser.Hydrate(userId, tenantId, $"sub-{username}", username, mainDeviceId: null, lastMainDeviceSwitchDate: null));
            await db.SaveChangesAsync();
        }
    }

    /// <summary>…100000000002 is the pre-existing shared "student template" the enroll consumer assigns for unlimited enrollments (plan §3.2.2) — absent from Identity's seed file, so this test provisions it explicitly.</summary>
    private async Task SeedSharedStudentTemplatePrincipalAsync()
    {
        var templateId = Guid.Parse("00000000-0000-0000-0000-100000000002");
        var db = IdentityDb();
        if (!await db.Principals.AnyAsync(p => p.Id == templateId))
        {
            var policy = new ManagedPolicy(
                Guid.NewGuid(),
                $"StudentAccessFallback-{templateId:N}",
                new List<ManagedPolicyStatement>
                {
                    new("AllowStudentBase", new List<string> { "courses:View", "video:Stream" }, true, null),
                });

            db.Principals.Add(new AlphaZero.Modules.Identity.Infrastructure.Models.PrincipalDataModel
            {
                Id = templateId,
                Username = "student-template",
                PasswordHash = "system-role",
                Name = "StudentTemplate",
                PrincipalType = PrincipalType.Role,
                TenantId = Guid.Empty,
                InlinePolicies = new(),
                ManagedPolicies = { policy },
            });
            await db.ManagedPolicies.AddAsync(policy);
            await db.SaveChangesAsync();
        }
    }

    // ------------------------------------------------------------------
    // Windowed plan → per-(course, plan) principal + windowed grant branch
    // ------------------------------------------------------------------

    [Fact]
    public async Task WindowedPlan_Should_MintPerCoursePlanPrincipal_And_GrantItOnEnroll()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreateCourse(tenantId);

        SetTenant(tenantId);

        // Act 1: a windowed plan, WITHOUT a caller principal — the per-(course, plan) principal is
        // minted by Identity and stored on the plan (§3.2.1). Managed → managed, no inline policies.
        var addPlanResp = await Client.PostAsJsonAsync($"/courses/{courseId}/plans",
            new AddPlanRequest { CourseId = courseId, Name = "Limited", AccessWindowDays = 14 });
        addPlanResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var planId = (await addPlanResp.Content.ReadFromJsonAsync<AddPlanResponse>())!.PlanId;

        var facts = new CoursePlanPrincipalFacts(tenantId, courseId, planId);

        var minted = await WaitForAsync(async () =>
            await IdentityDb().Principals.FirstOrDefaultAsync(p => p.Id == facts.PrincipalId));
        minted.Should().NotBeNull("the windowed plan must mint its per-(course, plan) principal");
        minted!.Username.Should().Be(facts.Username);
        minted.TenantId.Should().Be(tenantId);
        minted.PrincipalType.Should().Be(PrincipalType.Role);

        // The managed policy carries courses:View/Read with the AccessTicketValid condition and
        // no resource list of its own (assignment-scoped at evaluation) — §3.2.1.
        var policyAssignment = await WaitForAsync(async () =>
            await IdentityDb().PrincipalPolicyAssignments
                .FirstOrDefaultAsync(a => a.PrincipalId == facts.PrincipalId));
        policyAssignment.Should().NotBeNull("the minted principal must hold exactly its managed policy");

        var policy = await IdentityDb().ManagedPolicies.FirstAsync(p => p.Id == facts.PolicyId);
        policy.Name.Should().Be(facts.PolicyName);
        var statement = policy.Statements.Should().ContainSingle().Subject;
        statement.Effect.Should().BeTrue();
        statement.Actions.Should().Contain(new[] { "courses:View", "courses:Read" });
        statement.Condition.Should().BeOfType<ConditionNode>()
            .Which.Operator.Should().Be(Operator.AccessTicketValid);
        minted.InlinePolicies.Should().BeEmpty("§5 rule 1: no inline policies for course access");

        // Act 2: enroll under the windowed plan (window frozen at enroll, grant = per-plan principal)
        var studentId = Guid.NewGuid();
        await SeedIdentityStudentAsync(tenantId, studentId, "Grantee");

        SetTenant(tenantId);
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll",
            new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        enrollResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        // Assert: the enrollment carries the frozen window and the per-plan grant (assignment row)
        var enrollment = await DbContext.Enrollments.FindAsync(enrollmentId);
        enrollment!.AccessExpiresOn.Should().NotBeNull();
        (enrollment.AccessExpiresOn!.Value - enrollment.EnrolledOn).TotalDays.Should().BeInRange(13.9, 14.1); // frozen at enroll ("they bought 14 days")

        var expectedArn = $"az:course:{tenantId}:course/{courseId}";

        var grantRows = await WaitForAsync(async () =>
        {
            var rows = await IdentityDb().TenantPrincipalAssignments
                .Where(a => a.TenantUser.Id == studentId && a.TenantId == tenantId)
                .ToListAsync();
            return rows.Count > 0 ? rows : null;
        });
        grantRows.Should().NotBeNull();
        grantRows!.Should().OnlyContain(a => a.Resource.Value == expectedArn);
        grantRows.Should().OnlyContain(a => a.PrincipalId == facts.PrincipalId,
            "a windowed enrollment must be granted the per-(course, plan) principal, not the shared template");
    }

    // ------------------------------------------------------------------
    // Unlimited plan → unchanged: shared student template, no per-plan principal minted
    // ------------------------------------------------------------------

    [Fact]
    public async Task UnlimitedPlan_Should_KeepSharedTemplateGrant_AndNotMintPlanPrincipal()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreateCourse(tenantId);
        await SeedSharedStudentTemplatePrincipalAsync();

        SetTenant(tenantId);

        // Act 1: an unlimited plan WITH a caller principal (legacy shape, unchanged)
        var callerPrincipalId = Guid.NewGuid();
        var addPlanResp = await Client.PostAsJsonAsync($"/courses/{courseId}/plans",
            new AddPlanRequest { CourseId = courseId, Name = "Unlimited", PrincipalId = callerPrincipalId });
        addPlanResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var planId = (await addPlanResp.Content.ReadFromJsonAsync<AddPlanResponse>())!.PlanId;

        var facts = new CoursePlanPrincipalFacts(tenantId, courseId, planId);
        (await IdentityDb().Principals.FirstOrDefaultAsync(p => p.Id == facts.PrincipalId))
            .Should().BeNull("no per-(course, plan) principal may be minted for unlimited plans");

        // Act 2: enroll under the unlimited plan
        var studentId = Guid.NewGuid();
        await SeedIdentityStudentAsync(tenantId, studentId, "UnlimitedGrantee");

        SetTenant(tenantId);
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll",
            new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        enrollResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert: the shared student template is assigned at the course ARN (unchanged behavior)
        var expectedArn = $"az:course:{tenantId}:course/{courseId}";
        var templateId = Guid.Parse("00000000-0000-0000-0000-100000000002");

        var grantRows = await WaitForAsync(async () =>
        {
            var rows = await IdentityDb().TenantPrincipalAssignments
                .Where(a => a.TenantUser.Id == studentId && a.TenantId == tenantId)
                .ToListAsync();
            return rows.Count > 0 ? rows : null;
        });
        grantRows.Should().NotBeNull();
        grantRows!.Should().Contain(a => a.PrincipalId == templateId && a.Resource.Value == expectedArn,
            "unlimited enrollments keep the shared template grant (§3.2.2, unchanged)");
    }

    // ------------------------------------------------------------------
    // UpdatePlan: a legacy plan gains a window → future enrollments ride the minted principal
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdatePlan_Should_MintPrincipal_WhenLegacyPlanGainsWindow_AndGrantFutureEnrollments()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await CreateCourse(tenantId);

        SetTenant(tenantId);

        // Seed a PRINCIPAL-LESS plan at the aggregate level — the pre-step-9 legacy shape that
        // UpdatePlan mints for. The HTTP contract cannot create one directly (the AddPlan
        // validator requires a caller PrincipalId for unlimited plans, and windowed plans mint).
        var planId = Guid.NewGuid();
        await ExecuteDbContextAsync(async db =>
        {
            var course = await db.Courses.Include(c => c.Plans).FirstAsync(c => c.Id == courseId);
            course.AddPlan("Standard", Guid.Empty, planId: planId).IsError.Should().BeFalse();
            await db.SaveChangesAsync();
        });

        // Act: the plan gains a window (future-only edit per §3.2.5) — §3.2.1: UpdatePlan mints
        // ONLY for newly-windowed principal-less plans (validated by UpdatePlanCommandHandlerTests).
        SetTenant(tenantId);
        var updateResp = await Client.PutAsJsonAsync($"/courses/{courseId}/plans/{planId}",
            new UpdatePlanRequest { CourseId = courseId, PlanId = planId, Name = "Standard", AccessWindowDays = 7 });
        updateResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var facts = new CoursePlanPrincipalFacts(tenantId, courseId, planId);
        var minted = await WaitForAsync(async () =>
            await IdentityDb().Principals.FirstOrDefaultAsync(p => p.Id == facts.PrincipalId));
        minted.Should().NotBeNull("the newly-windowed plan must mint its per-(course, plan) principal");
    }

    private async Task<T?> WaitForAsync<T>(Func<Task<T?>> probe, int maxAttempts = 60) where T : class
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            var result = await probe();
            if (result is not null) return result;
            await Task.Delay(100);
        }

        return default;
    }
}
