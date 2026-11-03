using AlphaZero.Modules.Courses.Application.Enrollments;
using AlphaZero.Shared.Authorization;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

/// <summary>
/// Story #10 actor gate (§3.5): exactly two actor kinds may fire enrollment lifecycle
/// transitions — the authorized Payments revocation saga (SystemServiceAuthorizationContext)
/// and a tenant-admin actor — and the gate must run BEFORE any aggregate load. Every other shape
/// (null context, unauthorized system service, authenticated context with no envelope/admin role) fails closed.
/// </summary>
public class EnrollmentTransitionActorGuardTests
{
    [Fact]
    public void EnsureCanFire_Should_RefuseRogueInProcessSend_WhenNoContext()
    {
        var result = EnrollmentTransitionActor.EnsureCanFire(null);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.TransitionUnauthorized");
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.Forbidden);
    }

    [Fact]
    public void EnsureCanFire_Should_AdmitAuthorizedPaymentsRevocationSaga()
    {
        var context = SystemServiceAuthorizationContext.Create(
            EnrollmentTransitionActor.RevocationSagaServiceName,
            EnrollmentPermissions.Deactivate);

        var result = EnrollmentTransitionActor.EnsureCanFire(context);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public void EnsureCanFire_Should_RefuseUnauthorizedSystemService()
    {
        var context = SystemServiceAuthorizationContext.Create(
            "SomeOther.UnauthorizedService",
            EnrollmentPermissions.Deactivate);

        var result = EnrollmentTransitionActor.EnsureCanFire(context);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.TransitionUnauthorized");
    }

    [Fact]
    public void EnsureCanFire_Should_RefuseAuthorizedSystemService_WithWrongPermission()
    {
        var context = SystemServiceAuthorizationContext.Create(
            EnrollmentTransitionActor.RevocationSagaServiceName,
            "courses:SomeOtherAction");

        var result = EnrollmentTransitionActor.EnsureCanFire(context);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.TransitionUnauthorized");
    }

    [Fact]
    public void EnsureCanFire_Should_AdmitAccessControlEnvelopedActor()
    {
        var context = new TenantUserAuthorizationContext
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Support",
            IdentityId = "idp-1",
            AuthenticationMethod = "TenantUser",
            RequiredPermission = "enrollments:Suspend",
            ResourcePath = "/tenants/abc/courses/x",
        };

        var result = EnrollmentTransitionActor.EnsureCanFire(context);

        result.IsError.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, null)] // no envelope at all
    [InlineData("enrollments:Suspend", null)]
    [InlineData(null, "resource-path")]
    public void EnsureCanFire_Should_RefuseAuthenticatedCalls_WithoutEnvelope(string? permission, string? resourcePath)
    {
        var context = new TenantUserAuthorizationContext
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Anyone",
            IdentityId = "idp-2",
            AuthenticationMethod = "TenantUser",
            RequiredPermission = permission,
            ResourcePath = resourcePath ?? string.Empty,
        };

        var result = EnrollmentTransitionActor.EnsureCanFire(context);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.TransitionUnauthorized");
    }
}
