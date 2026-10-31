using AlphaZero.Modules.Courses.Application.Enrollments;
using AlphaZero.Shared.Authorization;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

/// <summary>
/// Story #10 actor gate (§3.5): exactly two actor kinds may fire enrollment lifecycle
/// transitions — the transport-claimed revocation saga and an AccessControl'enveloped
/// tenant-admin actor — and the gate must run BEFORE any aggregate load. Every other shape
/// (null context with no claim, authenticated context with no envelope) fails closed.
/// </summary>
public class EnrollmentTransitionActorGuardTests
{
    [Fact]
    public void EnsureCanFire_Should_RefuseRogueInProcessSend_WhenNoContextAndNoClaim()
    {
        // The saga-claim state can only leak into this test from another test that set it and
        // failed to dispose; start clean by wrapping explicitly.
        using (EnrollmentTransitionAutomation.ClaimSagaTransport())
        { }
        EnrollmentTransitionAutomation.HasTransportClaim().Should().BeFalse();

        var result = EnrollmentTransitionActor.EnsureCanFire(null);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.TransitionUnauthorized");
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.Forbidden);
    }

    [Fact]
    public async Task EnsureCanFire_Should_AdmitTransportClaimedDispatch_ThenStopLeaking()
    {
        // Simulate the saga shape, INCLUDING the async boundary: the consumer sets the claim,
        // dispatches (coursesModule.Send creates a fresh scope + awaits below), and disposes.
        Task<bool> LaterDispatch() => Task.FromResult(EnrollmentTransitionAutomation.HasTransportClaim());

        bool claimedInside;
        using (EnrollmentTransitionAutomation.ClaimSagaTransport())
        {
            claimedInside = await LaterDispatch();
        }

        claimedInside.Should().BeTrue("AsyncLocal flows into the awaited pipeline");
        EnrollmentTransitionAutomation.HasTransportClaim().Should().BeFalse("the claim must not leak past its using block");

        var after = EnrollmentTransitionActor.EnsureCanFire(null);
        after.IsError.Should().BeTrue();
        after.FirstError.Code.Should().Be("Enrollment.TransitionUnauthorized");
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
