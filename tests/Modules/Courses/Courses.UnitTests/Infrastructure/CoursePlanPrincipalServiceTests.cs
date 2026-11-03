using AlphaZero.Modules.Courses.Infrastructure.RequestResponseMessaging;
using AlphaZero.Modules.Identity.IntegrationEvents;
using FluentAssertions;
using MassTransit;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure;

public class CoursePlanPrincipalServiceTests : IDisposable
{
    private readonly IBusControl _moduleBus;
    private readonly CoursePlanPrincipalService _service;

    // Instance (not static) ids: the in-memory responder branches the success/failure scenarios
    // off the _failPolicy flag, which is set by the test body AFTER the constructor wired the bus.
    private readonly Guid CourseId = Guid.NewGuid();
    private readonly Guid PlanId = Guid.NewGuid();
    private readonly Guid TenantId = Guid.NewGuid();
    private readonly Guid ExpectedPrincipalId = Guid.NewGuid();

    /// <summary>Test body flips this to stage a failed EnsureCoursePlanPrincipal response.</summary>
    private bool _failPolicy;

    public CoursePlanPrincipalServiceTests()
    {
        // The service takes IBus (single 1-param constructor — spec-34 Autofac disambiguation) and
        // derives its request client from it. NSubstitute cannot intercept IBus.CreateRequestClient
        // (extension-only in MassTransit), so a REAL in-memory bus + responder endpoint is used —
        // the same transport path production plumbing executes.
        var busControl = Bus.Factory.CreateUsingInMemory(cfg =>
        {
            cfg.ReceiveEndpoint("ensure-course-plan-principal", e =>
                e.Handler<EnsureCoursePlanPrincipalRequest>(async ctx =>
                {
                    if (_failPolicy)
                        await ctx.RespondAsync(new CoursePlanPrincipalEnsureFailedResponse("Policy creation failed", "Principal.PolicyFailed"));
                    else
                        await ctx.RespondAsync(new CoursePlanPrincipalEnsuredResponse(ExpectedPrincipalId));
                }));
        });

        busControl.Start();
        _moduleBus = busControl;
        _service = new CoursePlanPrincipalService(_moduleBus);
    }

    public void Dispose() => _moduleBus.StopAsync().GetAwaiter().GetResult();

    [Fact]
    public async Task EnsurePlanPrincipalAsync_ShouldReturnPrincipalId_WhenSuccessResponseReceived()
    {
        var result = await _service.EnsurePlanPrincipalAsync(CourseId, PlanId, TenantId);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(ExpectedPrincipalId);
    }

    [Fact]
    public async Task EnsurePlanPrincipalAsync_ShouldReturnError_WhenFailureResponseReceived()
    {
        _failPolicy = true;

        var result = await _service.EnsurePlanPrincipalAsync(CourseId, PlanId, TenantId);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Principal.PolicyFailed");
        result.FirstError.Description.Should().Be("Policy creation failed");
    }
}
