using AlphaZero.Modules.Identity.Application;
using AlphaZero.Modules.Identity.Application.Principals.Commands.EnsureCoursePlanPrincipal;
using AlphaZero.Modules.Identity.Infrastructure.Consumers;
using AlphaZero.Modules.Identity.IntegrationEvents;
using ErrorOr;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Identity.UnitTests.Infrastructure.Consumers;

public class EnsureCoursePlanPrincipalRequestHandlerTests
{
    private readonly IIdentityModule _identityModule;
    private readonly ILogger<EnsureCoursePlanPrincipalRequestHandler> _logger;
    private readonly EnsureCoursePlanPrincipalRequestHandler _handler;

    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ExpectedPrincipalId = Guid.NewGuid();

    public EnsureCoursePlanPrincipalRequestHandlerTests()
    {
        _identityModule = Substitute.For<IIdentityModule>();
        _logger = Substitute.For<ILogger<EnsureCoursePlanPrincipalRequestHandler>>();
        _handler = new EnsureCoursePlanPrincipalRequestHandler(_identityModule, _logger);
    }

    [Fact]
    public async Task Consume_Should_SendEnsureCoursePlanPrincipalCommand_And_RespondWithSuccess_WhenSuccessful()
    {
        // Arrange
        var context = Substitute.For<ConsumeContext<EnsureCoursePlanPrincipalRequest>>();
        context.Message.Returns(new EnsureCoursePlanPrincipalRequest(CourseId, PlanId, TenantId));
        context.CancellationToken.Returns(CancellationToken.None);

        _identityModule.Send(
            Arg.Is<EnsureCoursePlanPrincipalCommand>(c => c.CourseId == CourseId && c.PlanId == PlanId && c.TenantId == TenantId),
            Arg.Any<CancellationToken>())
            .Returns(ExpectedPrincipalId);

        // Act
        await _handler.Consume(context);

        // Assert
        await _identityModule.Received(1).Send(
            Arg.Is<EnsureCoursePlanPrincipalCommand>(c => c.CourseId == CourseId && c.PlanId == PlanId && c.TenantId == TenantId),
            Arg.Any<CancellationToken>());

        await context.Received(1).RespondAsync(
            Arg.Is<CoursePlanPrincipalEnsuredResponse>(r => r.PrincipalId == ExpectedPrincipalId));
    }

    [Fact]
    public async Task Consume_Should_RespondWithFailure_WhenCommandReturnsError()
    {
        // Arrange
        var context = Substitute.For<ConsumeContext<EnsureCoursePlanPrincipalRequest>>();
        context.Message.Returns(new EnsureCoursePlanPrincipalRequest(CourseId, PlanId, TenantId));
        context.CancellationToken.Returns(CancellationToken.None);

        _identityModule.Send(
            Arg.Any<EnsureCoursePlanPrincipalCommand>(),
            Arg.Any<CancellationToken>())
            .Returns(Error.Failure("Principal.CreateFailed", "Failed to create principal"));

        // Act
        await _handler.Consume(context);

        // Assert
        await context.Received(1).RespondAsync(
            Arg.Is<CoursePlanPrincipalEnsureFailedResponse>(r =>
                r.Reason == "Failed to create principal" &&
                r.Code == "Principal.CreateFailed"));
    }
}
